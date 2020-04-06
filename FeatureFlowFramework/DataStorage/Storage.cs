﻿using FeatureFlowFramework.Helper;
using System;
using System.Collections.Generic;

namespace FeatureFlowFramework.DataStorage
{
    public static class Storage
    {
        static Storage()
        {
            var configStorage = new TextFileStorage("config", "configStorage", new TextFileStorage.Config() { fileSuffix = ".json" });
            RegisterReaderWriter(configStorage);

            var certificateStorage = new CertificateStorageReader("certificate");
            RegisterReader(certificateStorage);
        }

        private static Dictionary<string, IStorageReader> categoryToReader = new Dictionary<string, IStorageReader>();
        static FeatureLock categoryToReaderLock = new FeatureLock();
        private static Dictionary<string, IStorageWriter> categoryToWriter = new Dictionary<string, IStorageWriter>();
        static FeatureLock categoryToWriterLock = new FeatureLock();
        private static Func<string, IStorageReader> createDefaultReader = category => new TextFileStorage(category, "defaultStorage");
        private static Func<string, IStorageWriter> createDefaultWriter = category => new TextFileStorage(category, "defaultStorage");
        public static Func<string, IStorageReader> DefaultReaderFactory { set => createDefaultReader = value; }
        public static Func<string, IStorageWriter> DefaultWriterFactory { set => createDefaultWriter = value; }

        public static IStorageReader GetReader(string category)
        {
            using(categoryToReaderLock.ForWriting())
            {
                if(categoryToReader.TryGetValue(category, out IStorageReader reader)) return reader;
                else
                {
                    if(!(categoryToWriter.TryGetValue(category, out IStorageWriter writer) && writer is IStorageReader newReader))
                    {
                        newReader = createDefaultReader(category);
                    }
                    categoryToReader[category] = newReader;
                    return newReader;
                };
            }
        }

        public static IStorageWriter GetWriter(string category)
        {
            using (categoryToWriterLock.ForWriting())
            {
                if(categoryToWriter.TryGetValue(category, out IStorageWriter writer)) return writer;
                else
                {
                    if(!(categoryToReader.TryGetValue(category, out IStorageReader reader) && reader is IStorageWriter newWriter))
                    {
                        newWriter = createDefaultWriter(category);
                    }
                    categoryToWriter[category] = newWriter;
                    return newWriter;
                };
            }
        }

        public static void RegisterReader(IStorageReader reader)
        {
            using (categoryToReaderLock.ForWriting())
            {
                categoryToReader[reader.Category] = reader;
            }
        }

        public static void RegisterWriter(IStorageWriter writer)
        {
            using (categoryToWriterLock.ForWriting())
            {
                categoryToWriter[writer.Category] = writer;
            }
        }

        public static void RegisterReaderWriter(IStorageReaderWriter readerWriter)
        {
            RegisterReader(readerWriter);
            RegisterWriter(readerWriter);
        }

        public static bool HasCategoryReader(string category)
        {
            using (categoryToReaderLock.ForReading())
            {
                if(categoryToReader.ContainsKey(category)) return true;
                else if(HasCategoryWriter(category) && GetWriter(category) is IStorageReader) return true;
                else return false;
            }
        }

        public static bool HasCategoryWriter(string category)
        {
            using (categoryToWriterLock.ForReading())
            {
                if(categoryToWriter.ContainsKey(category)) return true;
                else if(HasCategoryReader(category) && GetReader(category) is IStorageWriter) return true;
                else return false;
            }
        }
    }
}