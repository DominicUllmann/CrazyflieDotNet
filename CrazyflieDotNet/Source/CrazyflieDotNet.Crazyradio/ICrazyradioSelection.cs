using CrazyflieDotNet.Crazyradio.Parallel;

namespace CrazyflieDotNet.Crazyradio
{
    public interface ICrazyradioSelection
    {
        /// <summary>
        /// Aquires a lock for the selected crazyflie connected via the selected device.
        /// </summary>        
        ICrazyRadioCommunicationTicket AquireLock();

    }
}
