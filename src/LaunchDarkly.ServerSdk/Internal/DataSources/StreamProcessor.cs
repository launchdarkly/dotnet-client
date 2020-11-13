using System;
using System.Net.Http;
using System.Threading.Tasks;
using LaunchDarkly.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using LaunchDarkly.Sdk.Internal;
using LaunchDarkly.Sdk.Internal.Events;
using LaunchDarkly.Sdk.Internal.Helpers;
using LaunchDarkly.Sdk.Internal.Stream;
using LaunchDarkly.Sdk.Server.Interfaces;
using LaunchDarkly.Sdk.Server.Internal.Model;

using static LaunchDarkly.Sdk.Server.Interfaces.DataStoreTypes;

namespace LaunchDarkly.Sdk.Server.Internal.DataSources
{
    internal class StreamProcessor : IDataSource, IStreamProcessor
    {
        private const String PUT = "put";
        private const String PATCH = "patch";
        private const String DELETE = "delete";

        private readonly Configuration _config;
        private readonly StreamManager _streamManager;
        private readonly IDataStoreUpdates _dataStoreUpdates;
        private readonly Logger _log;

        internal StreamProcessor(
            LdClientContext context,
            IDataStoreUpdates dataStoreUpdates,
            StreamManager.EventSourceCreator eventSourceCreator
            )
        {
            _config = context.Configuration;
            _log = context.Logger.SubLogger(LogNames.DataSourceSubLog);
            _streamManager = new StreamManager(this,
                MakeStreamProperties(_config),
                _config.StreamManagerConfiguration,
                ServerSideClientEnvironment.Instance,
                eventSourceCreator,
                context.DiagnosticStore,
                _log);
            _dataStoreUpdates = dataStoreUpdates;
        }

        private StreamProperties MakeStreamProperties(Configuration config)
        {
            return new StreamProperties(new Uri(config.StreamUri, "/all"),
                HttpMethod.Get, null);
        }

        #region IDataSource

        bool IDataSource.Initialized()
        {
            return _streamManager.Initialized;
        }

        Task<bool> IDataSource.Start()
        {
            return _streamManager.Start();
        }

        #endregion

        #region IStreamProcessor
        
        public async Task HandleMessage(StreamManager streamManager, string messageType, string messageData)
        {
            switch (messageType)
            {
                case PUT:
                    _dataStoreUpdates.Init(JsonUtil.DecodeJson<PutData>(messageData).Data.ToInitData());
                    streamManager.Initialized = true;
                    break;
                case PATCH:
                    PatchData patchData = JsonUtil.DecodeJson<PatchData>(messageData);
                    string patchKey;
                    if (GetKeyFromPath(patchData.Path, DataKinds.Features, out patchKey))
                    {
                        FeatureFlag flag = patchData.Data.ToObject<FeatureFlag>();
                        _dataStoreUpdates.Upsert(DataKinds.Features, patchKey, new ItemDescriptor(flag.Version, flag));
                    }
                    else if (GetKeyFromPath(patchData.Path, DataKinds.Segments, out patchKey))
                    {
                        Segment segment = patchData.Data.ToObject<Segment>();
                        _dataStoreUpdates.Upsert(DataKinds.Segments, patchKey, new ItemDescriptor(segment.Version, segment));
                    }
                    else
                    {
                        _log.Warn("Received patch event with unknown path: {0}", patchData.Path);
                    }
                    break;
                case DELETE:
                    DeleteData deleteData = JsonUtil.DecodeJson<DeleteData>(messageData);
                    var tombstone = new ItemDescriptor(deleteData.Version, null);
                    string deleteKey;
                    if (GetKeyFromPath(deleteData.Path, DataKinds.Features, out deleteKey))
                    {
                        _dataStoreUpdates.Upsert(DataKinds.Features, deleteKey, tombstone);
                    }
                    else if (GetKeyFromPath(deleteData.Path, DataKinds.Segments, out deleteKey))
                    {
                        _dataStoreUpdates.Upsert(DataKinds.Segments, deleteKey, tombstone);
                    }
                    else
                    {
                        _log.Warn("Received delete event with unknown path: {0}", deleteData.Path);
                    }
                    break;
            }
        }

        #endregion

        void IDisposable.Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        private void Dispose(bool disposing)
        {
            if (disposing)
            {
                ((IDisposable)_streamManager).Dispose();
            }
        }

        private static string GetDataKindPath(DataKind kind)
        {
            if (kind == DataKinds.Features)
            {
                return "/flags/";
            }
            else if (kind == DataKinds.Segments)
            {
                return "/segments/";
            }
            return null;
        }

        private static bool GetKeyFromPath(string path, DataKind kind, out string key)
        {
            if (path.StartsWith(GetDataKindPath(kind)))
            {
                key = path.Substring(GetDataKindPath(kind).Length);
                return true;
            }
            key = null;
            return false;
        }

        internal class PutData
        {
            internal AllData Data { get; private set; }

            [JsonConstructor]
            internal PutData(AllData data)
            {
                Data = data;
            }
        }

        internal class PatchData
        {
            internal string Path { get; private set; }
            internal JToken Data { get; private set; }

            [JsonConstructor]
            internal PatchData(string path, JToken data)
            {
                Path = path;
                Data = data;
            }
        }

        internal class DeleteData
        {
            internal string Path { get; private set; }
            internal int Version { get; private set; }

            [JsonConstructor]
            internal DeleteData(string path, int version)
            {
                Path = path;
                Version = version;
            }
        }
    }
}
