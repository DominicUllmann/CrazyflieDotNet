using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using CrazyflieDotNet.Crazyflie.Feature.Common;
using CrazyflieDotNet.Crazyflie.Feature.Param;
using CrazyflieDotNet.Crazyflie.Feature.Parameter;
using CrazyflieDotNet.CrazyMessaging;
using CrazyflieDotNet.CrazyMessaging.Protocol;
using log4net;

namespace CrazyflieDotNet.Crazyflie.Feature
{

    public class AllParamsUpdatedEventArgs
    {
    }

    public delegate void AllParamsUpdatedEventHandler(object sender, AllParamsUpdatedEventArgs args);

    /// <summary>
    /// implements the access to parameters.
    /// </summary>
    internal class ParamConfigurator : TocContainerBase<ParamTocElement>, ICrazyflieParamConfigurator
    {

        private static readonly ILog _log = LogManager.GetLogger(typeof(ParamConfigurator));

        private class ParamRequest : IDisposable
        {
            private ManualResetEvent _waitHandle = new ManualResetEvent(false);

            internal ParamRequest(ushort id)
            {
                Id = id;
            }

            public ushort Id { get; }

            public void Dispose()
            {
                _waitHandle.Dispose();
            }

            public bool Wait(int timeoutMs)
            {
                return _waitHandle.WaitOne(timeoutMs);
            }

            public bool CheckRequestFullfilledWithNotification(ushort receivedId)
            {
                if (Id == receivedId)
                {
                    _waitHandle.Set();
                    return true;
                }
                return false;
            }
        }

        /// <summary>
        ///  Channels used for the param port
        /// </summary>
        internal enum ParamChannel : byte
        {
            TOC_CHANNEL = 0,
            READ_CHANNEL = 1,
            WRITE_CHANNEL = 2,
        }

        private readonly ParameterSynchronizer _paramSynchronizer;
        private readonly IDictionary<ushort, object> _paramValues = new Dictionary<ushort, object>();
        private bool _isUpdated;
        private readonly IList<ParamRequest> _openLoadRequests = new List<ParamRequest>();
        private readonly object _openLoadRequestLock = new object();
        private readonly IList<ParamRequest> _openStoreRequests = new List<ParamRequest>();
        private readonly object _openStoreRequestLock = new object();


        public event AllParamsUpdatedEventHandler AllParametersUpdated;

        internal ParamConfigurator(ICrtpCommunicator communicator, bool useV2Protocol, DirectoryInfo cacheDirectory) :
            base(communicator, useV2Protocol, (byte)CrtpPort.PARAM,
                new DirectoryInfo(Path.Combine(cacheDirectory.FullName, "PARAM")))
        {
            _useV2Protocol = useV2Protocol;
            _communicator = communicator;
            _paramSynchronizer = new ParameterSynchronizer(communicator, useV2Protocol);
            _paramSynchronizer.StartProcessing();
            _paramSynchronizer.ParameterReceived += ParameterReceived;
            _paramSynchronizer.ParameterStored += ParameterStored;
        }

        private void ParameterStored(object sender, ParameterStoredEventArgs e)
        {
            UpdateOpenRequests(_openStoreRequests, _openStoreRequestLock, e.Id, "store");
        }

        private void ParameterReceived(object sender, ParameterReceivedEventArgs e)
        {
            var element = CurrentToc.GetElementById(e.Id);
            var packId = ParamTocElement.GetIdFromCString(element.CType);

            _paramValues[e.Id] = ParamTocElement.Unpack(packId, e.ParamValue);
            if (!_isUpdated && AreAllParamValuesUpdated())
            {
                _isUpdated = true;
                AllParametersUpdated?.Invoke(this, new AllParamsUpdatedEventArgs());
            }
            UpdateOpenRequests(_openLoadRequests, _openLoadRequestLock, e.Id, "load");
        }

        private void UpdateOpenRequests(IList<ParamRequest> requests, object lockObject, ushort paramId, string operationName)
        {
            ParamRequest toRemove = null;
            lock (lockObject)
            {
                _log.Debug($"check for parameter {operationName} request for id {paramId}");
                foreach (var request in requests)
                {
                    if (request.CheckRequestFullfilledWithNotification(paramId))
                    {
                        toRemove = request;
                        _log.Info($"fullfilled parameter {operationName} request for id {toRemove.Id}");
                        break;
                    }
                }
                if (toRemove != null)
                {
                    requests.Remove(toRemove);
                }
                else
                {
                    _log.Warn($"found not matching {operationName} request for answer for id: {paramId}; number of requests open: {requests.Count}");
                }
            }
        }

        protected override void StartLoadToc()
        {
            FetchTocFromTocFetcher();
        }

        /// <summary>
        /// see <see cref="ICrazyflieParamConfigurator.RequestUpdateOfAllParams"/>
        /// </summary>
        public void RequestUpdateOfAllParams()
        {
            EnsureToc();
            foreach (var tocElement in CurrentToc)
            {
                _paramSynchronizer.RequestLoadParamValue(tocElement.Identifier);
            }
        }

        private void EnsureToc()
        {
            if (CurrentToc == null)
            {
                throw new InvalidOperationException("fetch toc first");
            }
        }

        /// <summary>
        /// Check if all parameters from the TOC has at least been fetched once
        /// </summary>
        /// <returns></returns>
        private bool AreAllParamValuesUpdated()
        {
            EnsureToc();
            foreach (var tocElement in CurrentToc)
            {
                if (!_paramValues.ContainsKey(tocElement.Identifier))
                {
                    return false;
                }                
            }
            return true;
        }        

        /// <summary>
        /// <see cref="ICrazyflieParamConfigurator.GetLoadedParameterValue(string)"/>
        /// </summary>
        public object GetLoadedParameterValue(string completeName)
        {
            EnsureToc();
            var id = CurrentToc.GetElementId(completeName);

            if (id != null && _paramValues.ContainsKey(id.Value))
            {
                return _paramValues[id.Value];
            }
            return null;
        }

        /// <summary>
        /// see <see cref="ICrazyflieParamConfigurator.RefreshParameterValue(string)"/>
        /// </summary>
        public Task<object> RefreshParameterValue(string completeName)
        {
            EnsureToc();
            var id = CurrentToc.GetElementId(completeName);
            if (id == null)
            {
                throw new ArgumentException($"{completeName} not found in toc", nameof(completeName));
            }

            var request = new ParamRequest(id.Value);
            lock (_openLoadRequestLock)
            {
                _openLoadRequests.Add(request);
            }

            var task = new Task<object>(() =>
            {
                _paramSynchronizer.RequestLoadParamValue(id.Value);
                try
                {
                    if (!request.Wait(10000))
                    {
                        throw new ApplicationException($"failed to update parameter value {completeName} (timeout)");
                    }
                    return GetLoadedParameterValue(completeName);
                }
                finally
                {
                    request.Dispose();
                }
            });
            task.Start();
            return task;            
        }

        /// <summary>
        /// <see cref="ICrazyflieParamConfigurator.SetValue(string, object)"/>
        /// </summary>
        public Task SetValue(string completeName, object value)
        {
            EnsureToc();
            var id = CurrentToc.GetElementId(completeName);
            if (id == null)
            {
                throw new ArgumentException($"{completeName} not found in toc", nameof(completeName));
            }

            if (CurrentToc.GetElementById(id.Value).Access != ParamTocElement.AccessLevel.Readwrite)
            {
                throw new InvalidOperationException("unable to set a readonly parameter: " + completeName);
            }

            var element = CurrentToc.GetElementById(id.Value);
            var packId = ParamTocElement.GetIdFromCString(element.CType);
            var content = ParamTocElement.Pack(packId, value);

            var request = new ParamRequest(id.Value);
            lock (_openStoreRequestLock)
            {
                _openStoreRequests.Add(request);
            }

            var task = new Task(() =>
            {
                _paramSynchronizer.StoreParamValue(id.Value, content);
                try
                {
                    if (!request.Wait(10000))
                    {
                        throw new ApplicationException($"failed to store new parameter value {completeName} (timeout)");
                    }                    
                }
                finally
                {
                    request.Dispose();
                }
            });
            task.Start();
            return task;
        }

        /// <summary>
        /// <see cref="ICrazyflieParamConfigurator.IsParameterKnown(string)"/>
        /// </summary>
        public bool IsParameterKnown(string completeName)
        {
            EnsureToc();
            return CurrentToc.GetElementByCompleteName(completeName) != null;
        }
    }
}
