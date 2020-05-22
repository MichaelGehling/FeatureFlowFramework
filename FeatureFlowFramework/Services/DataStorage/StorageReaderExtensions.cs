﻿using FeatureFlowFramework.DataFlows;
using FeatureFlowFramework.Helpers;

namespace FeatureFlowFramework.Services.DataStorage
{
    public static class StorageReaderExtensions
    {
        public static bool TrySubscribeForChangeUpdate<T>(this IStorageReader reader, string uriPattern, IDataFlowSink updateSink)
        {
            var converter = new MessageConverter<ChangeNotification, ChangeUpdate<T>>(note => new ChangeUpdate<T>(note, reader));
            if(reader.TrySubscribeForChangeNotifications(uriPattern, converter))
            {
                converter.ConnectTo(updateSink);
                return true;
            }
            return false;
        }
    }
}