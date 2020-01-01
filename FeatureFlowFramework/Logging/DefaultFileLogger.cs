﻿using FeatureFlowFramework.DataFlows;
using FeatureFlowFramework.DataStorage;
using FeatureFlowFramework.Helper;
using FeatureFlowFramework.Workflows;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Threading;
using System.Threading.Tasks;

namespace FeatureFlowFramework.Logging
{
    public class DefaultFileLogger : Workflow<DefaultFileLogger.StateMachine>, IDataFlowSink
    {
        public class StateMachine : StateMachine<DefaultFileLogger>
        {
            protected override void Init()
            {
                var starting = State("Starting");
                var logging = State("Logging");

                starting.Build()
                    .Step("Load Configuration")
                        .Do(c => c.config.TryUpdateFromStorage(true))
                    .Step("If NewFileOnStartup is configured force archiving.")
                        .If(c => c.config.newFileOnStartup)
                            .Do(c => c.ArchiveCurrentLogfile())
                    .Step("Start logging")
                        .Goto(logging);

                logging.Build("Logging")
                    .Step("Check for config update")
                        .Do(c => c.config.TryUpdateFromStorage(true))
                    .Step("Wait until receiving logMessages")
                        .WaitFor(c => c.receiver)
                    .Step("Write all logMessages from receiver to file")
                        .Do(c => c.WriteToLogFile())
                        .CatchAndDo((c, e) => Log.ERROR($"{c.Name}: Writing to log file failed.", e.ToString()))
                    .Step("Do archiving if file limit is exceeded")
                        .If(c => c.config.logFileSizeLimitInMB * 1024 * 1024 <= c.GetLogFileSize())
                            .Do(c => c.ArchiveCurrentLogfile())
                        .CatchAndDo((c, e) => Log.ERROR($"{c.Name}: Moving log file to archive failed.", e.ToString()))
                    .Step("Delay before next writing, if configured")
                        .If(c => c.config.delayAfterWritingInMs > 0)
                            .Wait(c => c.config.delayAfterWritingInMs.Milliseconds())
                    .Step("Loop logging state")
                        .Loop();
            }
        }

        public class Config : Configuration
        {
            public string logFilePath = "log.txt";
            public bool newFileOnStartup = true;

            public string archiveFilePath = "logArchive.zip";
            public int logFilesArchiveLimitInMB = 100;
            public float logFileSizeLimitInMB = 5;
            public CompressionLevel compressionLevel = CompressionLevel.Fastest;
            public int delayAfterWritingInMs = 0;
            public Loglevel logFileLoglevel = Loglevel.TRACE;
            public string logFileLogFormat = "";
        }

        public Config config = new Config();

        private QueueReceiver<object> receiver = new QueueReceiver<object>(100000);

        public void Post<M>(in M message)
        {
            ((IDataFlowSink)receiver).Post(message);
        }

        private static readonly Comparer<ZipArchiveEntry> nameComparer = Comparer<ZipArchiveEntry>.Create((f1, f2) => f2.Name.CompareTo(f1.Name));

        private float GetLogFileSize()
        {
            var logFileInfo = new FileInfo(config.logFilePath);
            return logFileInfo.Length;
        }

        private async Task WriteToLogFile()
        {
            if (receiver.IsEmpty) return;

            bool updateCreationTime = false;
            var logFileInfo = new FileInfo(config.logFilePath);
            if (!logFileInfo.Exists) updateCreationTime = true;

            using (FileStream stream = File.Open(config.logFilePath, FileMode.Append, FileAccess.Write, FileShare.ReadWrite))
            using (StreamWriter writer = new StreamWriter(stream))
            {
                if (updateCreationTime) logFileInfo.CreationTime = AppTime.Now;
                if (receiver.IsFull) await writer.WriteLineAsync(new LogMessage(Loglevel.WARNING, "LOGGING QUEUE OVERFLOW: Some log messages might be lost!").Print(config.logFileLogFormat));

                var messages = receiver.ReceiveAll();
                foreach (var msg in messages)
                {
                    if (msg is LogMessage logMsg)
                    {
                        if (logMsg.level <= config.logFileLoglevel)
                        {
                            await writer.WriteLineAsync(logMsg.Print(config.logFileLogFormat));
                        }
                    }
                    else await writer.WriteLineAsync(msg.ToString());
                }
            }
        }

        // TODO: Make async
        private void ArchiveCurrentLogfile()
        {
            var logFileInfo = new FileInfo(config.logFilePath);
            logFileInfo.Refresh();
            if (!logFileInfo.Exists) return;

            // Make thread temporarily foreground to avoid the risk of corrupting the archive if the app is closed while archiving.
            var wasThreadBackground = Thread.CurrentThread.IsBackground;
            Thread.CurrentThread.IsBackground = false;
            try
            {
                using (var fileStream = new FileStream(config.archiveFilePath, FileMode.OpenOrCreate))
                {
                    using (var archive = new ZipArchive(fileStream, ZipArchiveMode.Update, true))
                    {
                        archive.CreateEntryFromFile(config.logFilePath, logFileInfo.CreationTime.ToString("yyyy-MM-dd_HH-mm-ss") + " until " + logFileInfo.LastWriteTime.ToString("yyyy-MM-dd_HH-mm-ss") + ".txt", config.compressionLevel);
                    }

                    using (var archive = new ZipArchive(fileStream, ZipArchiveMode.Update, true))
                    {
                        List<ZipArchiveEntry> entries = new List<ZipArchiveEntry>(archive.Entries);
                        entries.Sort(nameComparer);
                        long overallLength = 0;
                        foreach (var entry in entries)
                        {
                            if (overallLength > config.logFilesArchiveLimitInMB * 1024 * 1024)
                            {
                                entry.Delete();
                            }
                            else overallLength += entry.CompressedLength;
                        }
                    }
                }
                logFileInfo.Delete();
            }
            catch (Exception e)
            {
                Log.ERROR("Archiving log file failed", e.ToString());
            }
            Thread.CurrentThread.IsBackground = wasThreadBackground;
        }

        public Task PostAsync<M>(M message)
        {
            return ((IDataFlowSink)receiver).PostAsync(message);
        }
    }
}