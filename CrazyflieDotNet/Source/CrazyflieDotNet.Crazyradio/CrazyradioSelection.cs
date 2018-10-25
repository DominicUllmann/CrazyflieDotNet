using CrazyflieDotNet.Crazyradio.Driver;
using CrazyflieDotNet.Crazyradio.Parallel;
using log4net;
using System;

namespace CrazyflieDotNet.Crazyradio
{
    internal class CrazyradioSelection : ICrazyradioSelection
    {

        private static readonly ILog _log = LogManager.GetLogger(typeof(CrazyradioSelection));

        private CrazyRadioCommunicationLock _radioLock;
        private CrazyflieId _id;

        internal CrazyradioSelection(CrazyRadioCommunicationLock radioLock, CrazyflieId id)
        {
            _radioLock = radioLock;
            _id = id;
        }

        public ICrazyRadioCommunicationTicket AquireLock()
        {
            if (!_radioLock.Driver.IsOpen)
            {
                try
                {
                    _log.Info("Opening crazy radio.");
                    _radioLock.Driver.Open();
                }
                catch (Exception ex)
                {
                    _log.Error("Failed to open crazy radio", ex);
                    throw new ApplicationException("Failed to open crazy radio");
                }
            }
            return new CrazyRadioCommunicationTicket(_radioLock, _id.RadioChannel, _id.RadioAddress, _id.RadioDataRate);            
        }
    }
}
