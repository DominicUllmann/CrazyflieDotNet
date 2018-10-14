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

        private IDictionary<byte, IList<CrtpEventCallback>> _eventCallbacks = 
            new Dictionary<byte, IList<CrtpEventCallback>>();

        private object _lock = new object();

        internal void RegisterEventHandler(byte port, CrtpEventCallback crtpEventCallback)
        {
            lock (_lock)
            {
                if (!_eventCallbacks.ContainsKey(port))
                {
                    IList<CrtpEventCallback> handlers = new List<CrtpEventCallback>();
                    _eventCallbacks[port] = handlers;
                }

                _eventCallbacks[port].Add(crtpEventCallback);
            }
        }

        internal void Notify(CrtpMessage crtpMessage)
        {
            IList<CrtpEventCallback> handlersToNotify = new List<CrtpEventCallback>();
            lock (_lock)
            {
                IList<CrtpEventCallback> handlers;
                if (!_eventCallbacks.TryGetValue(crtpMessage.Port, out handlers))
                {
                    return;
                }
                // copy handlers so that we don't need to keep lock during notify to protect handler list against modification.
                foreach (var handler in handlers)
                {
                    handlersToNotify.Add(handler);
                }

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
                if (!_eventCallbacks.ContainsKey(port))
                {
                    return;
                }

                _eventCallbacks[port].Remove(crtpEventCallback);
            }
        }
    }
}
