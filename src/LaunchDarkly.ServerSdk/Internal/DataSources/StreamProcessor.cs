using System;
using System.Net.Http;
using System.Threading.Tasks;
using Common.Logging;
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
        private const String INDIRECT_PATCH = "indirect/patch";

        private static readonly ILog Log = LogManager.GetLogger(typeof(StreamProcessor));

        private readonly Configuration _config;
        private readonly StreamManager _streamManager;
        private readonly IFeatureRequestor _featureRequestor;
        private readonly IDataStore _dataStore;

        internal StreamProcessor(Configuration config, IFeatureRequestor featureRequestor,
            IDataStore dataStore, StreamManager.EventSourceCreator eventSourceCreator, IDiagnosticStore diagnosticStore)
        {
            _streamManager = new StreamManager(this,
                MakeStreamProperties(config),
                config.StreamManagerConfiguration,
                ServerSideClientEnvironment.Instance,
                eventSourceCreator, diagnosticStore);
            _config = config;
            _featureRequestor = featureRequestor;
            _dataStore = dataStore;
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
                    _dataStore.Init(JsonUtil.DecodeJson<PutData>(messageData).Data.ToInitData());
                    streamManager.Initialized = true;
                    break;
                case PATCH:
                    PatchData patchData = JsonUtil.DecodeJson<PatchData>(messageData);
                    string patchKey;
                    if (GetKeyFromPath(patchData.Path, DataKinds.Features, out patchKey))
                    {
                        FeatureFlag flag = patchData.Data.ToObject<FeatureFlag>();
                        _dataStore.Upsert(DataKinds.Features, patchKey, new ItemDescriptor(flag.Version, flag));
                    }
                    else if (GetKeyFromPath(patchData.Path, DataKinds.Segments, out patchKey))
                    {
                        Segment segment = patchData.Data.ToObject<Segment>();
                        _dataStore.Upsert(DataKinds.Segments, patchKey, new ItemDescriptor(segment.Version, segment));
                    }
                    else
                    {
                        Log.WarnFormat("Received patch event with unknown path: {0}", patchData.Path);
                    }
                    break;
                case DELETE:
                    DeleteData deleteData = JsonUtil.DecodeJson<DeleteData>(messageData);
                    var tombstone = new ItemDescriptor(deleteData.Version, null);
                    string deleteKey;
                    if (GetKeyFromPath(deleteData.Path, DataKinds.Features, out deleteKey))
                    {
                        _dataStore.Upsert(DataKinds.Features, deleteKey, tombstone);
                    }
                    else if (GetKeyFromPath(deleteData.Path, DataKinds.Segments, out deleteKey))
                    {
                        _dataStore.Upsert(DataKinds.Segments, deleteKey, tombstone);
                    }
                    else
                    {
                        Log.WarnFormat("Received delete event with unknown path: {0}", deleteData.Path);
                    }
                    break;
                case INDIRECT_PATCH:
                    await UpdateTaskAsync(messageData);
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
                _featureRequestor.Dispose();
            }
        }

        private async Task UpdateTaskAsync(string objectPath)
        {
            try
            {
                if (GetKeyFromPath(objectPath, DataKinds.Features, out var key))
                {
                    var feature = await _featureRequestor.GetFlagAsync(key);
                    if (feature != null)
                    {
                        _dataStore.Upsert(DataKinds.Features, key, new ItemDescriptor(feature.Version, feature));
                    }
                }
                else if (GetKeyFromPath(objectPath, DataKinds.Segments, out key))
                {
                    var segment = await _featureRequestor.GetSegmentAsync(key);
                    if (segment != null)
                    {
                        _dataStore.Upsert(DataKinds.Segments, key, new ItemDescriptor(segment.Version, segment));
                    }
                }
                else
                {
                    Log.WarnFormat("Received indirect patch event with unknown path: {0}", objectPath);
                }
            }
            catch (AggregateException ex)
            {
                Log.ErrorFormat("Error Updating {0}: '{1}'",
                    ex, objectPath, Util.ExceptionMessage(ex.Flatten()));
            }
            catch (UnsuccessfulResponseException ex) when (ex.StatusCode == 401)
            {
                Log.ErrorFormat("Error Updating {0}: '{1}'", objectPath, Util.ExceptionMessage(ex));
                if (ex.StatusCode == 401)
                {
                    Log.Error("Received 401 error, no further streaming connection will be made since SDK key is invalid");
                    ((IDisposable)this).Dispose();
                }
            }
            catch (TimeoutException ex) {
                Log.ErrorFormat("Error Updating {0}: '{1}'",
                    ex, objectPath, Util.ExceptionMessage(ex));
                _streamManager.Restart();
            }
            catch (Exception ex)
            {
                Log.ErrorFormat("Error Updating feature: '{0}'",
                    ex, Util.ExceptionMessage(ex));
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