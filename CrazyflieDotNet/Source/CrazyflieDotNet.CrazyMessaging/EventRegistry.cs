using CrazyflieDotNet.CrazyMessaging.Protocol;
using log4net;
using System;
using System.Collections.Generic;

namespace CrazyflieDotNet.CrazyMessaging
{
    /// <summary>
    /// class keeps track of event registrations for the crtp communicator.
    /// </summary>
    internal class EventRegistry
    {

        private static readonly ILog _log = LogManager.GetLogger(typeof(EventRegistry));

        private IDictionary<byte, IList<CrtpEventCallback>> _eventCallbacksSpecificPort = 
            new Dictionary<byte, IList<CrtpEventCallback>>();

        private IList<CrtpEventCallback> _eventCallbacksAll = new List<CrtpEventCallback>();

        private object _lock = new object();

        internal void RegisterAllEventHandler(CrtpEventCallback crtpEventCallback)
        {
            lock (_lock)
            {
                _eventCallbacksAll.Add(crtpEventCallback);
            }
        }

        internal void RegisterEventHandler(byte port, CrtpEventCallback crtpEventCallback)
        {
            lock (_lock)
            {
                if (!_eventCallbacksSpecificPort.ContainsKey(port))
                {
                    IList<CrtpEventCallback> handlers = new List<CrtpEventCallback>();
                    _eventCallbacksSpecificPort[port] = handlers;
                }

                _eventCallbacksSpecificPort[port].Add(crtpEventCallback);
            }
        }

        internal void Notify(CrtpMessage crtpMessage)
        {
            // copy handlers so that we don't need to keep lock during notify to protect handler list against modification.
            var handlersToNotify = new List<CrtpEventCallback>();
            lock (_lock)
            {
                handlersToNotify.AddRange(_eventCallbacksAll);
                IList<CrtpEventCallback> handlers;
                if (!_eventCallbacksSpecificPort.TryGetValue(crtpMessage.Port, out handlers))
                {
                    return;
                }
                handlersToNotify.AddRange(handlers);                

            }
            foreach (var handler in handlersToNotify)
            {
                try
                {
                    handler.Invoke(crtpMessage);
                }
                catch (Exception ex)
                {
                    _log.Error("error while notifying receiver", ex);
                }
            }            
        }

        internal void RemoveEventHandler(byte port, CrtpEventCallback crtpEventCallback)
        {
            lock (_lock)
            {
                if (!_eventCallbacksSpecificPort.ContainsKey(port))
                {
                    return;
                }

                _eventCallbacksSpecificPort[port].Remove(crtpEventCallback);
            }
        }
    }
}
