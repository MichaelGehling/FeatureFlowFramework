﻿using FeatureFlowFramework.DataFlows;
using FeatureFlowFramework.Helper;
using FeatureFlowFramework.Logging;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.IO;
using System.Runtime.Caching;
using System.Text;
using System.Threading.Tasks;

namespace FeatureFlowFramework.DataStorage
{
    public class TextFileStorage : IStorageReaderWriter
    {
        public class Config : Configuration
        {
            public bool useCategoryFolder = true;
            public string basePath = "";
            public string fileSuffix = "";
            public bool allowSubscription = true;
            public TimeSpan subscriptionSamplingTime = 5.Seconds();
            public string configUri;
            public TimeSpan timeout = TimeSpan.Zero;
            public TimeSpan duplicateFileEventSuppressionTime = 100.Milliseconds();
            public bool updateCacheForSubscription = true;
            public bool updateCacheOnRead = true;
            public bool updateCacheOnWrite = true;
            public TimeSpan cacheSlidingExpiration = 10.Minutes();
            public int cacheSizeInMb = 10;
            public override string Uri => configUri ?? base.Uri;
        }

        private Config config;
        private readonly string category;
        private DirectoryInfo rootDir;
        private CacheItemPolicy cacheItemPolicy;
        private readonly MemoryCache cache;
        private HashSet<string> fileSet = new HashSet<string>();
        private Dictionary<string, Subscription> subscriptions = new Dictionary<string, Subscription>();
        public string Category => category;

        private bool fileSystemObservationActive = false;
        private FileSystemObserver fileObserver;
        private ProcessingEndpoint<FileSystemObserver.ChangeNotification> fileChangeProcessor;
        private DuplicateMessageSuppressor duplicateMessageSuppressor;

        public TextFileStorage(string category, string configUri, Config config = default)
        {
            this.category = category;
            this.config = config ?? new Config();
            config = this.config;
            if (config.configUri == null) config.configUri += config.Uri + "_" + category;
            if (config.ConfigCategory != category) config.TryUpdateFromStorage(false);
            config.configUri = configUri ?? config.configUri;

            string basePath = config.basePath;
            if (config.useCategoryFolder) basePath = Path.Combine(config.basePath, category);
            rootDir = new DirectoryInfo(basePath);

            lock (fileSet)
            {
                fileSet = CreateNewFileSet();
            }
            if (config.updateCacheForSubscription || config.updateCacheOnRead || config.updateCacheOnWrite)
            {
                NameValueCollection cacheConfig = new NameValueCollection();
                cacheConfig.Add("cacheMemoryLimitMegabytes", config.cacheSizeInMb.ToString());
                cache = new MemoryCache("FileStorageCache", cacheConfig);
                cacheItemPolicy = new CacheItemPolicy();
                cacheItemPolicy.SlidingExpiration = config.cacheSlidingExpiration;
            }
        }

        private void ActivateFileSystemObservation(bool activate)
        {
            if (activate && !fileSystemObservationActive)
            {
                fileObserver = new FileSystemObserver(rootDir.FullName, "*" + config.fileSuffix, true);
                duplicateMessageSuppressor = new DuplicateMessageSuppressor(config.duplicateFileEventSuppressionTime, (m1, m2) =>
                {
                    if (m1 is FileSystemObserver.ChangeNotification notification1 &&
                        m2 is FileSystemObserver.ChangeNotification notification2 &&
                        notification1.changeType == WatcherChangeTypes.Changed &&
                        notification2.changeType == WatcherChangeTypes.Created &&
                        notification1.path == notification2.path)
                    {
                        return true;
                    }
                    else return m1.Equals(m2);
                });
                fileChangeProcessor = new ProcessingEndpoint<FileSystemObserver.ChangeNotification>(ProcessChangeNotification);
                fileObserver.ConnectTo(duplicateMessageSuppressor).ConnectTo(fileChangeProcessor);
                fileSystemObservationActive = true;
            }
            else if (!activate && fileSystemObservationActive)
            {
                fileObserver.Dispose();
                fileObserver = null;
                duplicateMessageSuppressor = null;
                fileChangeProcessor = null;
                fileSystemObservationActive = false;
            }
        }

        private void ProcessChangeNotification(FileSystemObserver.ChangeNotification notification)
        {
            if (subscriptions.Count == 0)
            {
                ActivateFileSystemObservation(false);
            }

            if (notification.changeType.HasFlag(WatcherChangeTypes.Deleted))
            {
                ProcessChangeNotification_Delete(notification);
            }
            else
            {
                DirectoryInfo directoryInfo = new DirectoryInfo(notification.path);

                if (!directoryInfo.Attributes.HasFlag(FileAttributes.Directory))
                {
                    ProcessChangeNotification_Directory(notification);
                }
                else
                {
                    ProcessChangeNotification_File(notification, directoryInfo);
                }
            }
        }

        private void ProcessChangeNotification_File(FileSystemObserver.ChangeNotification notification, DirectoryInfo directoryInfo)
        {
            if (notification.changeType.HasFlag(WatcherChangeTypes.Created))
            {
                var addedFileInfos = directoryInfo.GetFiles("*" + config.fileSuffix, SearchOption.AllDirectories);
                foreach (var fileInfo in addedFileInfos)
                {
                    lock (fileSet)
                    {
                        fileSet.Add(fileInfo.FullName);
                    }
                    string uri = FilePathToUri(fileInfo.FullName);
                    NotifySubscriptions(uri, UpdateEvent.Created);
                }
            }
            else if (notification.changeType.HasFlag(WatcherChangeTypes.Renamed))
            {
                UpdateOnRemovedDir();

                var addedFileInfos = directoryInfo.GetFiles("*" + config.fileSuffix, SearchOption.AllDirectories);
                foreach (var fileInfo in addedFileInfos)
                {
                    lock (fileSet)
                    {
                        fileSet.Add(fileInfo.FullName);
                    }
                    string uri = FilePathToUri(fileInfo.FullName);
                    NotifySubscriptions(uri, UpdateEvent.Created);
                }
            }
        }

        private void ProcessChangeNotification_Directory(FileSystemObserver.ChangeNotification notification)
        {
            if (notification.changeType.HasFlag(WatcherChangeTypes.Changed))
            {
                string uri = FilePathToUri(notification.path);
                cache?.Remove(uri);
                NotifySubscriptions(uri, UpdateEvent.Updated);
            }
            else if (notification.changeType.HasFlag(WatcherChangeTypes.Created))
            {
                lock (fileSet)
                {
                    fileSet.Add(notification.path);
                }
                string uri = FilePathToUri(notification.path);
                NotifySubscriptions(uri, UpdateEvent.Created);
            }
            else if (notification.changeType.HasFlag(WatcherChangeTypes.Renamed))
            {
                lock (fileSet)
                {
                    fileSet.Remove(notification.oldPath);
                    fileSet.Add(notification.path);
                }
                string oldUri = FilePathToUri(notification.oldPath);
                cache?.Remove(oldUri);
                NotifySubscriptions(oldUri, UpdateEvent.Removed);
                string newUri = FilePathToUri(notification.path);
                NotifySubscriptions(newUri, UpdateEvent.Created);
            }
        }

        private void ProcessChangeNotification_Delete(FileSystemObserver.ChangeNotification notification)
        {
            bool removed;
            lock (fileSet)
            {
                removed = fileSet.Remove(notification.path);
            }
            if (removed)
            {
                string uri = FilePathToUri(notification.path);
                cache?.Remove(uri);
                NotifySubscriptions(uri, UpdateEvent.Removed);
            }
            else
            {
                UpdateOnRemovedDir();
            }
        }

        private void UpdateOnRemovedDir()
        {
            var newFileSet = CreateNewFileSet();
            lock (fileSet)
            {
                fileSet.ExceptWith(newFileSet);
                foreach (var fileName in fileSet)
                {
                    string uri = FilePathToUri(fileName);
                    cache?.Remove(uri);
                    NotifySubscriptions(uri, UpdateEvent.Removed);
                }
                fileSet = newFileSet;
            }
        }

        private void UpdateCacheForSubscriptions(ChangeNotification changeNotification)
        {
            if (changeNotification.updateEvent == UpdateEvent.Removed) return;

            if (config.updateCacheForSubscription)
            {
                string filePath = UriToFilePath(changeNotification.uri);
                FileInfo fileInfo = new FileInfo(filePath);
                if (fileInfo.Exists)
                {
                    try
                    {
                        using (var stream = fileInfo.OpenText())
                        {
                            if (config.timeout > TimeSpan.Zero) stream.BaseStream.ReadTimeout = config.timeout.TotalMilliseconds.ToIntTruncated();
                            var fileContent = stream.ReadToEnd();
                            cache?.Add(changeNotification.uri, fileContent, cacheItemPolicy);
                        }
                    }
                    catch (Exception e)
                    {
                        cache?.Remove(changeNotification.uri);
                        Log.WARNING($"Failed reading file {filePath} to cache. Cache entry was invalidated", e.ToString());
                    }
                }
            }
        }

        private HashSet<string> CreateNewFileSet()
        {
            if (!rootDir.RefreshAnd().Exists) return new HashSet<string>();

            var fileInfos = rootDir.GetFiles("*" + config.fileSuffix, SearchOption.AllDirectories);
            var newFileSet = new HashSet<string>();
            foreach (var info in fileInfos)
            {
                newFileSet.Add(info.FullName);
            }
            return newFileSet;
        }

        private void NotifySubscriptions(string uri, UpdateEvent updateEvent, bool updateCache = true)
        {
            var subscriptions = this.subscriptions;
            List<Subscription> toBeRemoved = null;
            foreach (var subscription in subscriptions.Values)
            {
                if (subscription.sender.CountConnectedSinks == 0)
                {
                    if (toBeRemoved == null) toBeRemoved = new List<Subscription>();
                    toBeRemoved.Add(subscription);
                }
                else
                {
                    if (uri.MatchesWildcard(subscription.uriPattern))
                    {
                        var changeNotification = new ChangeNotification(this.category, uri, updateEvent, AppTime.Now);
                        if (updateCache)
                        {
                            UpdateCacheForSubscriptions(changeNotification);
                            updateCache = false;
                        }
                        subscription.sender.Send(changeNotification);
                    }
                }
            }
        }

        private readonly struct Subscription
        {
            public readonly string uriPattern;
            public readonly Sender sender;

            public Subscription(string uriPattern, Sender sender)
            {
                this.uriPattern = uriPattern;
                this.sender = sender;
            }
        }

        public bool TrySubscribeForChangeNotifications(string uriPattern, IDataFlowSink notificationSink)
        {
            if (!subscriptions.TryGetValue(uriPattern, out Subscription subscription))
            {
                ActivateFileSystemObservation(true);
                lock (subscriptions)
                {
                    var newSubscriptions = new Dictionary<string, Subscription>(subscriptions);
                    subscription = new Subscription(uriPattern, new Sender());
                    newSubscriptions.Add(uriPattern, subscription);
                    subscriptions = newSubscriptions;
                }
            }
            subscription.sender.ConnectTo(notificationSink);
            return true;
        }

        protected virtual string FilePathToUri(string filePath)
        {
            if (!filePath.StartsWith(rootDir.FullName) || !filePath.EndsWith(config.fileSuffix)) return null;

            int basePathLength = rootDir.FullName.Length + 1;
            var rawUri = filePath.Substring(basePathLength, filePath.Length - basePathLength - config.fileSuffix.Length);
            return rawUri.Replace('\\', '/');
        }

        protected virtual string UriToFilePath(string uri)
        {
            return Path.Combine(rootDir.FullName, $"{uri}{config.fileSuffix}");
        }

        protected virtual bool TryDeserialize<T>(string str, out T data)
        {
            data = default;

            if (str is T strObj)
            {
                data = strObj;
                return true;
            }
            else
            {
                try
                {
                    data = str.FromJson<T>();
                    return true;
                }
                catch
                {
                    data = default;
                    return false;
                }
            }
        }

        protected virtual bool TrySerialize<T>(T data, out string str)
        {
            if (data is string strData)
            {
                str = strData;
                return true;
            }
            else
            {
                try
                {
                    str = data.ToJson();
                    return true;
                }
                catch (Exception e)
                {
                    Log.ERROR("Failed serializing persiting object", e.ToString());
                    str = default;
                    return false;
                }
            }
        }

        public bool TryListUris(out string[] uris, string pattern = null)
        {
            return TryListUrisAsync(pattern).Result.Out(out uris);
        }

        public async Task<AsyncOutResult<bool, string[]>> TryListUrisAsync(string pattern = null)
        {
            try
            {
                if (this.fileSystemObservationActive)
                {
                    lock (fileSet)
                    {
                        if (fileSet.Count == 0) return new AsyncOutResult<bool, string[]>(true, Array.Empty<string>());

                        var uris = new List<string>();
                        foreach (var fileName in fileSet)
                        {
                            string uri = FilePathToUri(fileName);
                            if (pattern == null || uri.MatchesWildcard(pattern))
                            {
                                uris.Add(uri);
                            }
                        }
                        return new AsyncOutResult<bool, string[]>(true, uris.ToArray());
                    }
                }
                else
                {
                    if (!rootDir.RefreshAnd().Exists) return new AsyncOutResult<bool, string[]>(true, Array.Empty<string>());

                    var uris = new List<string>();
                    var files = await rootDir.GetFilesAsync($"*{config.fileSuffix}", SearchOption.AllDirectories);
                    foreach (var file in files)
                    {
                        string uri = FilePathToUri(file.FullName);
                        if (pattern == null || uri.MatchesWildcard(pattern))
                        {
                            uris.Add(uri);
                        }
                    }
                    return new AsyncOutResult<bool, string[]>(true, uris.ToArray());
                }
            }
            catch (Exception e)
            {
                Log.ERROR("Reading files to retreive Uris failed!", e.ToString());
                return new AsyncOutResult<bool, string[]>(false, null);
            }
        }

        public bool Exists(string uri)
        {
            if(cache?.Contains(uri) ?? false) return true;

            string filePath = UriToFilePath(uri);
            return File.Exists(filePath);
        }

        public bool TryRead<T>(string uri, out T data)
        {
            try
            {
                if (cache?.Get(uri) is string cacheString)
                {
                    return TryDeserialize(cacheString, out data);
                }

                string filePath = UriToFilePath(uri);
                FileInfo fileInfo = new FileInfo(filePath);
                if (fileInfo.Exists)
                {
                    using (var stream = fileInfo.OpenText())
                    {
                        if (config.timeout > TimeSpan.Zero) stream.BaseStream.ReadTimeout = config.timeout.TotalMilliseconds.ToIntTruncated();
                        var fileContent = stream.ReadToEnd();

                        if (config.updateCacheOnRead)
                        {
                            cache?.Add(uri, fileContent, cacheItemPolicy);
                        }

                        return TryDeserialize(fileContent, out data);
                    }
                }

                data = default;
                return false;
            }
            catch
            {
                data = default;
                return false;
            }
        }

        public async Task<AsyncOutResult<bool, T>> TryReadAsync<T>(string uri)
        {
            try
            {
                T data = default;
                bool success = false;

                if (cache?.Get(uri) is string cacheString)
                {
                    success = TryDeserialize(cacheString, out data);
                    return new AsyncOutResult<bool, T>(success, data);
                }

                string filePath = UriToFilePath(uri);
                FileInfo fileInfo = new FileInfo(filePath);
                if (fileInfo.Exists)
                {
                    using (var stream = fileInfo.OpenText())
                    {
                        if (config.timeout > TimeSpan.Zero) stream.BaseStream.ReadTimeout = config.timeout.TotalMilliseconds.ToIntTruncated();
                        var fileContent = await stream.ReadToEndAsync();

                        if (config.updateCacheOnRead)
                        {
                            cache?.Add(uri, fileContent, cacheItemPolicy);
                        }

                        success = TryDeserialize(fileContent, out data);
                    }
                }

                return new AsyncOutResult<bool, T>(success, data);
            }
            catch
            {
                return new AsyncOutResult<bool, T>(false, default);
            }
        }

        public bool TryRead(string uri, Stream targetStream)
        {
            try
            {
                if (cache?.Get(uri) is string cacheString)
                {
                    var stream = new MemoryStream(Encoding.UTF8.GetBytes(cacheString));
                    stream.CopyTo(targetStream);
                    return true;
                }

                string filePath = UriToFilePath(uri);
                FileInfo fileInfo = new FileInfo(filePath);
                if (fileInfo.Exists)
                {
                    using (var stream = fileInfo.OpenRead())
                    {
                        if (config.timeout > TimeSpan.Zero) stream.ReadTimeout = config.timeout.TotalMilliseconds.ToIntTruncated();

                        if (config.updateCacheOnRead)
                        {
                            using (var memoryStream = new MemoryStream())
                            using (var textReader = new StreamReader(memoryStream))
                            {
                                stream.CopyTo(memoryStream);
                                memoryStream.Position = 0;
                                memoryStream.CopyTo(targetStream);
                                memoryStream.Position = 0;
                                string fileContent = textReader.ReadToEnd();
                                cache?.Add(uri, fileContent, cacheItemPolicy);
                            }
                        }
                        else
                        {
                            stream.CopyTo(targetStream);
                        }

                        return true;
                    }
                }
                return false;
            }
            catch
            {
                return false;
            }
        }

        public async Task<bool> TryReadAsync(string uri, Stream targetStream)
        {
            try
            {
                if (cache?.Get(uri) is string cacheString)
                {
                    var stream = new MemoryStream(Encoding.UTF8.GetBytes(cacheString));
                    await stream.CopyToAsync(targetStream);
                    return true;
                }

                string filePath = UriToFilePath(uri);
                FileInfo fileInfo = new FileInfo(filePath);
                if (fileInfo.Exists)
                {
                    using (var stream = fileInfo.OpenRead())
                    {
                        if (config.timeout > TimeSpan.Zero) stream.ReadTimeout = config.timeout.TotalMilliseconds.ToIntTruncated();

                        if (config.updateCacheOnRead)
                        {
                            using (var memoryStream = new MemoryStream())
                            using (var textReader = new StreamReader(memoryStream))
                            {
                                await stream.CopyToAsync(memoryStream);
                                memoryStream.Position = 0;
                                await memoryStream.CopyToAsync(targetStream);
                                memoryStream.Position = 0;
                                string fileContent = await textReader.ReadToEndAsync();
                                cache?.Add(uri, fileContent, cacheItemPolicy);
                            }
                        }
                        else
                        {
                            await stream.CopyToAsync(targetStream);
                        }

                        return true;
                    }
                }
                return false;
            }
            catch
            {
                return false;
            }
        }

        public bool TryWrite<T>(string uri, T data)
        {
            try
            {
                string filePath = UriToFilePath(uri);
                FileInfo fileInfo = new FileInfo(filePath);
                fileInfo.Directory.Create();

                if (TrySerialize(data, out string fileContent))
                {
                    if (config.updateCacheOnWrite || (config.updateCacheForSubscription && fileSystemObservationActive))
                    {
                        cache?.Add(uri, fileContent, cacheItemPolicy);
                        if (fileSystemObservationActive)
                        {
                            UpdateEvent updateEvent = fileInfo.Exists ? UpdateEvent.Updated : UpdateEvent.Created;
                            NotifySubscriptions(uri, updateEvent, false);
                            if (updateEvent == UpdateEvent.Created) duplicateMessageSuppressor?.AddSuppressor(new FileSystemObserver.ChangeNotification(WatcherChangeTypes.Created, fileInfo.FullName, fileInfo.FullName));
                            else duplicateMessageSuppressor?.AddSuppressor(new FileSystemObserver.ChangeNotification(WatcherChangeTypes.Changed, fileInfo.FullName, fileInfo.FullName));
                        }
                    }

                    using (var stream = fileInfo.CreateText())
                    {
                        if (config.timeout > TimeSpan.Zero) stream.BaseStream.WriteTimeout = config.timeout.TotalMilliseconds.ToIntTruncated();
                        stream.Write(fileContent);
                        return true;
                    }
                }
                else return false;
            }
            catch (Exception e)
            {
                Log.ERROR($"Failed writing file for uri {uri}!", e.ToString());
                return false;
            }
        }

        public async Task<bool> TryWriteAsync<T>(string uri, T data)
        {
            try
            {
                string filePath = UriToFilePath(uri);
                FileInfo fileInfo = new FileInfo(filePath);
                fileInfo.Directory.Create();

                if (TrySerialize(data, out string fileContent))
                {
                    if (config.updateCacheOnWrite || (config.updateCacheForSubscription && fileSystemObservationActive))
                    {
                        cache?.Add(uri, fileContent, cacheItemPolicy);
                        if (fileSystemObservationActive)
                        {
                            UpdateEvent updateEvent = fileInfo.Exists ? UpdateEvent.Updated : UpdateEvent.Created;
                            NotifySubscriptions(uri, updateEvent, false);
                            if (updateEvent == UpdateEvent.Created) duplicateMessageSuppressor?.AddSuppressor(new FileSystemObserver.ChangeNotification(WatcherChangeTypes.Created, fileInfo.FullName, fileInfo.FullName));
                            else duplicateMessageSuppressor?.AddSuppressor(new FileSystemObserver.ChangeNotification(WatcherChangeTypes.Changed, fileInfo.FullName, fileInfo.FullName));
                        }
                    }

                    using (var stream = fileInfo.CreateText())
                    {
                        if (config.timeout > TimeSpan.Zero) stream.BaseStream.WriteTimeout = config.timeout.TotalMilliseconds.ToIntTruncated();
                        await stream.WriteAsync(fileContent);
                        return true;
                    }
                }
                else return false;
            }
            catch (Exception e)
            {
                Log.ERROR($"Failed writing file for uri {uri}!", e.ToString());
                return false;
            }
        }

        public bool TryWrite(string uri, Stream sourceStream)
        {
            bool disposeStream = false;
            try
            {
                string filePath = UriToFilePath(uri);
                FileInfo fileInfo = new FileInfo(filePath);
                fileInfo.Directory.Create();

                if (config.updateCacheOnWrite || (config.updateCacheForSubscription && fileSystemObservationActive))
                {
                    if (!sourceStream.CanSeek)
                    {
                        MemoryStream memoryStream = new MemoryStream();
                        sourceStream.CopyTo(memoryStream);
                        sourceStream = memoryStream;
                        sourceStream.Position = 0;
                        disposeStream = true;
                    }
                    string fileContent;
                    var origPosition = sourceStream.Position;
                    // Without using, otherwise the streamreader would dispose the stream already here!
                    var textReader = new StreamReader(sourceStream);
                    fileContent = textReader.ReadToEnd();
                    sourceStream.Position = origPosition;
                    cache?.Add(uri, fileContent, cacheItemPolicy);

                    if (fileSystemObservationActive)
                    {
                        UpdateEvent updateEvent = fileInfo.Exists ? UpdateEvent.Updated : UpdateEvent.Created;
                        NotifySubscriptions(uri, updateEvent, false);
                        if (updateEvent == UpdateEvent.Created) duplicateMessageSuppressor?.AddSuppressor(new FileSystemObserver.ChangeNotification(WatcherChangeTypes.Created, fileInfo.FullName, fileInfo.FullName));
                        else duplicateMessageSuppressor?.AddSuppressor(new FileSystemObserver.ChangeNotification(WatcherChangeTypes.Changed, fileInfo.FullName, fileInfo.FullName));
                    }
                }

                using (var stream = fileInfo.OpenWrite())
                {
                    if (config.timeout > TimeSpan.Zero) stream.WriteTimeout = config.timeout.TotalMilliseconds.ToIntTruncated();
                    stream.SetLength(0);
                    sourceStream.CopyTo(stream);
                    return true;
                }
            }
            catch (Exception e)
            {
                Log.ERROR($"Failed writing file for uri {uri}!", e.ToString());
                return false;
            }
            finally
            {
                if (disposeStream) sourceStream.Dispose();
            }
        }

        public async Task<bool> TryWriteAsync(string uri, Stream sourceStream)
        {
            bool disposeStream = false;
            try
            {
                string filePath = UriToFilePath(uri);
                FileInfo fileInfo = new FileInfo(filePath);
                fileInfo.Directory.Create();

                if (config.updateCacheOnWrite || (config.updateCacheForSubscription && fileSystemObservationActive))
                {
                    if (!sourceStream.CanSeek)
                    {
                        MemoryStream memoryStream = new MemoryStream();
                        await sourceStream.CopyToAsync(memoryStream);
                        sourceStream = memoryStream;
                        sourceStream.Position = 0;
                        disposeStream = true;
                    }
                    string fileContent;
                    var origPosition = sourceStream.Position;
                    // Without using, otherwise the streamreader would dispose the stream already here!
                    var textReader = new StreamReader(sourceStream);
                    fileContent = await textReader.ReadToEndAsync();
                    sourceStream.Position = origPosition;
                    cache?.Add(uri, fileContent, cacheItemPolicy);

                    if (fileSystemObservationActive)
                    {
                        UpdateEvent updateEvent = fileInfo.Exists ? UpdateEvent.Updated : UpdateEvent.Created;
                        NotifySubscriptions(uri, updateEvent, false);
                        if (updateEvent == UpdateEvent.Created) duplicateMessageSuppressor?.AddSuppressor(new FileSystemObserver.ChangeNotification(WatcherChangeTypes.Created, fileInfo.FullName, fileInfo.FullName));
                        else duplicateMessageSuppressor?.AddSuppressor(new FileSystemObserver.ChangeNotification(WatcherChangeTypes.Changed, fileInfo.FullName, fileInfo.FullName));
                    }
                }

                using (var stream = fileInfo.OpenWrite())
                {
                    if (config.timeout > TimeSpan.Zero) stream.WriteTimeout = config.timeout.TotalMilliseconds.ToIntTruncated();
                    stream.SetLength(0);
                    await sourceStream.CopyToAsync(stream);
                    return true;
                }
            }
            catch (Exception e)
            {
                Log.ERROR($"Failed writing file for uri {uri}!", e.ToString());
                return false;
            }
            finally
            {
                if (disposeStream) sourceStream.Dispose();
            }
        }

        public bool TryAppend<T>(string uri, T data)
        {
            try
            {
                string filePath = UriToFilePath(uri);
                FileInfo fileInfo = new FileInfo(filePath);
                fileInfo.Directory.Create();

                if (TrySerialize(data, out string fileContent))
                {
                    using (var stream = fileInfo.AppendText())
                    {
                        if (config.timeout > TimeSpan.Zero) stream.BaseStream.WriteTimeout = config.timeout.TotalMilliseconds.ToIntTruncated();
                        stream.Write(fileContent);
                        return true;
                    }
                }
                else return false;
            }
            catch (Exception e)
            {
                Log.ERROR($"Failed writing file for uri {uri}!", e.ToString());
                return false;
            }
        }

        public async Task<bool> TryAppendAsync<T>(string uri, T data)
        {
            try
            {
                string filePath = UriToFilePath(uri);
                FileInfo fileInfo = new FileInfo(filePath);
                fileInfo.Directory.Create();

                if (TrySerialize(data, out string fileContent))
                {
                    using (var stream = fileInfo.AppendText())
                    {
                        if (config.timeout > TimeSpan.Zero) stream.BaseStream.WriteTimeout = config.timeout.TotalMilliseconds.ToIntTruncated();
                        await stream.WriteAsync(fileContent);
                        return true;
                    }
                }
                else return false;
            }
            catch (Exception e)
            {
                Log.ERROR($"Failed writing file for uri {uri}!", e.ToString());
                return false;
            }
        }

        public bool TryAppend(string uri, Stream sourceStream)
        {
            try
            {
                string filePath = UriToFilePath(uri);
                FileInfo fileInfo = new FileInfo(filePath);
                fileInfo.Directory.Create();

                using (var stream = fileInfo.OpenWrite())
                {
                    if (config.timeout > TimeSpan.Zero) stream.WriteTimeout = config.timeout.TotalMilliseconds.ToIntTruncated();
                    stream.Seek(0, SeekOrigin.End);
                    sourceStream.CopyTo(stream);
                    return true;
                }
            }
            catch (Exception e)
            {
                Log.ERROR($"Failed writing file for uri {uri}!", e.ToString());
                return false;
            }
        }

        public async Task<bool> TryAppendAsync(string uri, Stream sourceStream)
        {
            try
            {
                string filePath = UriToFilePath(uri);
                FileInfo fileInfo = new FileInfo(filePath);
                fileInfo.Directory.Create();

                using (var stream = fileInfo.OpenWrite())
                {
                    if (config.timeout > TimeSpan.Zero) stream.WriteTimeout = config.timeout.TotalMilliseconds.ToIntTruncated();
                    stream.Seek(0, SeekOrigin.End);
                    await sourceStream.CopyToAsync(stream);
                    return true;
                }
            }
            catch (Exception e)
            {
                Log.ERROR($"Failed writing file for uri {uri}!", e.ToString());
                return false;
            }
        }

        public bool TryDelete(string uri)
        {
            string filePath = UriToFilePath(uri);
            FileInfo fileInfo = new FileInfo(filePath);

            try
            {
                cache?.Remove(uri);
                if (fileInfo.Exists) fileInfo.Delete();
                return true;
            }
            catch (Exception e)
            {
                Log.ERROR($"Failed on deleting file at {fileInfo.ToString()}", e.ToString());
                return false;
            }
        }

        public Task<bool> TryDeleteAsync(string uri)
        {
            return Task.FromResult(TryDelete(uri));
        }

        private class FileSubscriptionStatus
        {
            public FileInfo fileInfo;
            public FileInfoStatus fileInfoStatus;

            public FileSubscriptionStatus(string filePath)
            {
                fileInfo = new FileInfo(filePath);
                fileInfoStatus = fileInfo.GetFileInfoStatus();
            }

            public bool Changed
            {
                get
                {
                    bool changed = fileInfo.ChangedSince(fileInfoStatus);
                    if (changed) fileInfoStatus = fileInfo.GetFileInfoStatus();
                    return changed;
                }
            }
        }
    }
}