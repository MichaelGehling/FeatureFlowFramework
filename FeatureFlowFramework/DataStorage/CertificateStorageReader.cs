﻿using FeatureFlowFramework.DataFlows;
using FeatureFlowFramework.Helper;
using FeatureFlowFramework.Logging;
using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography.X509Certificates;
using System.Threading.Tasks;

namespace FeatureFlowFramework.DataStorage
{
    /*
     * TODO:
     * - Support certificate store (X509Store, see https://docs.microsoft.com/en-us/dotnet/api/system.security.cryptography.x509certificates.x509certificate2?view=netframework-4.8)
     * - How to handle passwords: Encode in Uri? Define dictionary in config for each Uri? Do not support at all?
     * - Subscription for updated certificates?
     * - StorageWriter for updating certificates?
     * */

    public class CertificateStorageReader : IStorageReader
    {
        public class Config : Configuration
        {
            public bool useCategoryFolder = true;
            public string basePath = "";
            public string fileSuffix = ".pfx";
            public string configUri;
            public override string Uri => configUri ?? base.Uri;
        }

        private Config config;
        private readonly string category;
        public string Category => category;

        public CertificateStorageReader(string category, Config config = null)
        {
            this.category = category;
            this.config = config ?? new Config();
        }

        protected virtual string BuildFilePath(string uri)
        {
            if(config.useCategoryFolder) return Path.Combine(config.basePath, category, $"{uri}{config.fileSuffix}");
            else return Path.Combine(config.basePath, $"{uri}{config.fileSuffix}");
        }

        public bool TryRead<T>(string uri, out T data)
        {
            var type = typeof(T);
            data = default;
            try
            {
                string filePath = BuildFilePath(uri);
                var x509 = new X509Certificate2(filePath);
                if(x509 is T x509T)
                {
                    data = x509T;
                    return true;
                }
            }
            catch(Exception e)
            {
                Log.WARNING($"Certificate {uri} could not be loaded", e.ToString());
            }

            return false;
        }

        public async Task<AsyncOutResult<bool, T>> TryReadAsync<T>(string uri)
        {
            T data = default;
            await Task.Run(() => TryRead<T>(uri, out data));
            return new AsyncOutResult<bool, T>(true, data);
        }

        public bool TryListUris(out string[] uris, string pattern = null)
        {
            return TryListUrisAsync(pattern).Result.Out(out uris);
        }

        public async Task<AsyncOutResult<bool, string[]>> TryListUrisAsync(string pattern = null)
        {
            try
            {
                string path = config.useCategoryFolder ? Path.Combine(config.basePath, category) : config.basePath;
                DirectoryInfo dir = new DirectoryInfo(path);
                if(!dir.Exists) return new AsyncOutResult<bool, string[]>(true, Array.Empty<string>());

                List<string> uris = new List<string>();
                var files = await dir.GetFilesAsync($"*{config.fileSuffix}", SearchOption.AllDirectories);
                int basePathLength = dir.FullName.Length + 1;
                foreach(var file in files)
                {
                    string uri = file.FullName.Substring(basePathLength, file.FullName.Length - basePathLength - config.fileSuffix.Length);
                    if(pattern == null || uri.MatchesWildcard(pattern))
                    {
                        uris.Add(uri);
                    }
                }
                return new AsyncOutResult<bool, string[]>(true, uris.ToArray());
            }
            catch(Exception e)
            {
                Log.ERROR("Reading files to retreive Uris failed!", e.ToString());
                return new AsyncOutResult<bool, string[]>(false, null);
            }
        }

        public bool TryRead(string uri, Stream targetStream)
        {
            return false;
        }

        public Task<bool> TryReadAsync(string uri, Stream targetStream)
        {
            return Task.FromResult(false);
        }

        public bool TrySubscribeForChangeNotifications(string uriPattern, IDataFlowSink notificationSink)
        {
            Log.WARNING(this, "Subscription is currently not supported!");
            return false;
        }
    }
}
