using System;
using System.Collections.Generic;
using System.Threading;
using CrazyflieDotNet.CrazyMessaging.Protocol;
using log4net;

namespace CrazyflieDotNet.CrazyMessaging
{
    /// <summary>
    /// Responsible to ensure that messages are sent at least once.
    /// </summary>
    internal class AtLeastOnceCommunicatorStrategy
    {
        private static readonly ILog _log = LogManager.GetLogger(typeof(AtLeastOnceCommunicatorStrategy));

        private class WaitForMessageRequest : IDisposable
        {

            private readonly Timer _waitTimer;
            private readonly Func<CrtpMessage, bool> _checkFunction;
            private readonly Action<WaitForMessageRequest> _notifyTimeout;
            public CrtpMessage Request { get; }

            internal WaitForMessageRequest(CrtpMessage request, Func<CrtpMessage, bool> checkFunction, Action<WaitForMessageRequest> notifyTimeout, TimeSpan timeout)
            {
                Request = request;
                _checkFunction = checkFunction;
                var timerInterValInMs = (int) Math.Round(timeout.TotalMilliseconds);
                _waitTimer = new Timer(RequestTimeout, null, timerInterValInMs, timerInterValInMs);

                _notifyTimeout = notifyTimeout;
            }

            private void RequestTimeout(object state)
            {
                _notifyTimeout(this);
            }

            public bool IsSatisfiedBy(CrtpMessage message)
            {
                return _checkFunction(message);
            }

            public void Dispose()
            {
                _waitTimer?.Dispose();
            }
        }

        private readonly ICrtpCommunicator _communicator;
        private readonly List<WaitForMessageRequest> _registeredMessageExceptions = new List<WaitForMessageRequest> ();
        
        private readonly object _lock = new object();

        internal AtLeastOnceCommunicatorStrategy(ICrtpCommunicator communicator)
        {
            _communicator = communicator;
            _communicator.RegisterAllEventHandler(CheckExceptedAnswer);
        }

        internal void RegisterExpectation(CrtpMessage message, Func<CrtpMessage, bool> checkFunction, TimeSpan timeout)
        {
            lock (_lock)
            {
                _registeredMessageExceptions.Add(new WaitForMessageRequest(message, checkFunction, NotifyTimeout, timeout));
            }
        }

        private void NotifyTimeout(WaitForMessageRequest request)
        {
            
            lock (_lock)
            {
                if (_registeredMessageExceptions.Contains(request))
                {                    
                    // resend with fire and forget semantic as expectation already registered.
                    _communicator.SendMessage(request.Request);
                    _log.Info($"retrying message to port {request.Request.Port} / channel {request.Request.Channel}");
                }
                else
                {
                    // already removed, so no need to retry.
                    _log.Info($"timout with no matching request to port {request.Request.Port} / channel {request.Request.Channel}. Ignore.");
                }
            }
        }

        private void CheckExceptedAnswer(CrtpMessage message)
        {
            WaitForMessageRequest foundRequest = null;
            lock (_lock)
            {

                foreach (var expectation in _registeredMessageExceptions)
                {
                    if (expectation.IsSatisfiedBy(message))
                    {
                        foundRequest = expectation;                        
                        break;
                    }
                }

                if (foundRequest != null)
                {
                    // stop retry timer.
                    foundRequest.Dispose();
                    _registeredMessageExceptions.Remove(foundRequest);
                    _log.Debug($"received expected message for port {foundRequest.Request.Port} / channel: {foundRequest.Request.Channel}");
                }
            }
        }

    }
}
