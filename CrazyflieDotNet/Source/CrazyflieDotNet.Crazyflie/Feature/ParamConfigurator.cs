using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using CrazyflieDotNet.Crazyflie.Feature.Common;
using CrazyflieDotNet.Crazyflie.Feature.Param;
using CrazyflieDotNet.Crazyflie.Feature.Parameter;
using CrazyflieDotNet.CrazyMessaging;
using CrazyflieDotNet.CrazyMessaging.Protocol;

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

        private class LoadParamRequest : IDisposable
        {
            private ManualResetEvent _waitHandle = new ManualResetEvent(false);

            internal LoadParamRequest(ushort id)
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
        private readonly IList<LoadParamRequest> _openLoadRequests = new List<LoadParamRequest>();
        private readonly object _openLoadRequestLock = new object();

        public event AllParamsUpdatedEventHandler AllParametersUpdated;

        internal ParamConfigurator(ICrtpCommunicator communicator, bool useV2Protocol) : 
            base(communicator, useV2Protocol, (byte)CrtpPort.PARAM)
        {
            _useV2Protocol = useV2Protocol;
            _communicator = communicator;
            _paramSynchronizer = new ParameterSynchronizer(communicator, useV2Protocol);
            _paramSynchronizer.StartProcessing();
            _paramSynchronizer.ParameterReceived += ParameterReceived;
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
            LoadParamRequest toRemove = null;
            lock (_openLoadRequestLock)
            {
                foreach (var request in _openLoadRequests)
                {
                    if (request.CheckRequestFullfilledWithNotification(e.Id))
                    {
                        toRemove = request;
                    }
                    break;
                }
                if (toRemove != null)
                {
                    _openLoadRequests.Remove(toRemove);
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

            var request = new LoadParamRequest(id.Value);
            _openLoadRequests.Add(request);

            var task = new Task<object>(() =>
            {
                _paramSynchronizer.RequestLoadParamValue(id.Value);
                try
                {
                    if (!request.Wait(5000))
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
        public void SetValue(string completeName, object value)
        {
            EnsureToc();
            var id = CurrentToc.GetElementId(completeName);
            if (id == null)
            {
                throw new ArgumentException($"{completeName} not found in toc", nameof(completeName));
            }
            var element = CurrentToc.GetElementById(id.Value);
            var packId = ParamTocElement.GetIdFromCString(element.CType);
            _paramSynchronizer.StoreParamValue(id.Value, ParamTocElement.Pack(packId, value));
        }
    }
}
