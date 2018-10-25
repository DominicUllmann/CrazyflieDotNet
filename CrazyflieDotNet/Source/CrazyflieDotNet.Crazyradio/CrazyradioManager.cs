using CrazyflieDotNet.Crazyradio.Driver;
using CrazyflieDotNet.Crazyradio.Parallel;
using log4net;
using System;
using System.Collections.Generic;
using System.Linq;

namespace CrazyflieDotNet.Crazyradio
{

    /// <summary>
    /// The manager which ensures that accesses to different crazyflies are serialized.
    /// </summary>
    public class CrazyradioManager : ICrazyradioManager
    {
        private static readonly ILog _log = LogManager.GetLogger(typeof(CrazyradioManager));
        private static CrazyradioManager _instance;
        private object _lock = new object();

        private readonly IList<CrazyradioCommunicationLock> _radios = new List<CrazyradioCommunicationLock>();

        private CrazyradioManager()
        {
            try
            {
                // Scan for connected Crazyradio USB dongles
                var radios = CrazyradioDriver.GetCrazyradios().ToList();
                foreach (var radio in radios)
                {
                    _radios.Add(new CrazyradioCommunicationLock(radio));
                }
            }
            catch (Exception ex)
            {
                var msg = "Error getting Crazyradio USB dongle devices connected to computer.";
                _log.Error(msg, ex);
                throw new ApplicationException(msg, ex);
            }
        }
        
        public ICrazyradioSelection SelectRadio(CrazyflieUri uri)
        {
            lock (_lock)
            {
                if (uri.DeviceId < _radios.Count)
                {
                    var radio = _radios[uri.DeviceId];
                    radio.ClientCount++;
                    return new CrazyradioSelection(radio, uri.Id);
                }
                else
                {
                    throw new ApplicationException("can't aquire lock on non-existing radio dongle.");
                }
            }
        }

        public void DeselectRadio(CrazyflieUri uri)
        {
            lock (_lock)
            {
                if (uri.DeviceId < _radios.Count)
                {
                    var radio = _radios[uri.DeviceId];
                    radio.ClientCount--;
                    if (radio.ClientCount == 0)
                    {
                        _log.Info($"Closing crazy radio (device {uri.DeviceId})");
                        try
                        {
                            radio.Driver.Close();
                        }
                        catch (Exception ex)
                        {
                            _log.Error("Failed to close crazy radio", ex);
                            throw new ApplicationException("Failed to close crazy radio");
                        }
                    }
                }
                else
                {
                    throw new ApplicationException("can't aquire lock on non-existing radio dongle.");
                }
            }
        }

        public static ICrazyradioManager Instance
        {
            get
            {                
                if (_instance == null)
                {
                    _instance = new CrazyradioManager();
                }
                return _instance;
            }            
        }

        public IList<CrazyflieUri> Scan() 
        {
            var result = new List<CrazyflieUri>();
            try
            {
                for (int i = 0; i < _radios.Count; i++)
                {
                    var driverResult = ScanDriver(_radios[i]);
                    foreach (var id in driverResult)
                    {
                        result.Add(new CrazyflieUri(i, id));
                    }
                }
                return result;
            }
            catch (Exception ex)
            {
                _log.Error("fail to scan for crarzyflies.", ex);
                throw new ApplicationException("Can't scan crazyflies");
            }
        }

        private IList<CrazyflieId> ScanDriver(CrazyradioCommunicationLock crazyRadioCommunicationLock)
        {
            var scanResult = crazyRadioCommunicationLock.Driver.ScanChannels();
            var result = new List<CrazyflieId>();
            foreach (var rateResult in scanResult)
            {
                foreach (var id in rateResult.Channels)
                {
                    result.Add(new CrazyflieId(id, rateResult.DataRate, CrazyradioDefault.Address));
                }
            }
            return result;
        }

    }
}
