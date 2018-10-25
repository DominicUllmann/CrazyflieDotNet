using CrazyflieDotNet.Crazyradio.Driver;
using System.Threading;

namespace CrazyflieDotNet.Crazyradio.Parallel
{
    /// <summary>
    /// This class manages the locking of the radio device / release of lock.
    /// </summary>
    internal class CrazyRadioCommunicationLock
    {

        private readonly object _lock = new object();

        internal CrazyRadioCommunicationLock(ICrazyradioDriver driver)
        {
            Driver = driver;
        }

        /// <summary>
        /// Counts how many clients have selected a channel on the radio.
        /// </summary>
        internal int ClientCount { get; set; } = 0;

        internal ICrazyradioDriver Driver { get; }

        internal void AquireLock(RadioChannel channel, RadioAddress address, RadioDataRate rate)
        {
            Monitor.Enter(_lock);
            Driver.Channel = channel;
            Driver.Address = address;
            Driver.DataRate = rate;
        }        

        internal void ReleaseLock()
        {
            Monitor.Exit(_lock);
        }

    }
}
