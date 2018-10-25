using CrazyflieDotNet.Crazyradio.Driver;

namespace CrazyflieDotNet.Crazyradio.Parallel
{

    /// <summary>
    /// See <see cref="ICrazyRadioCommunicationTicket"/>.
    /// </summary>
    internal class CrazyRadioCommunicationTicket : ICrazyRadioCommunicationTicket
    {
        private CrazyRadioCommunicationLock _radioLock;

        public CrazyRadioCommunicationTicket(CrazyRadioCommunicationLock radioLock, RadioChannel channel, RadioAddress radioAddress, RadioDataRate radioDataRate)
        {
            _radioLock = radioLock;
            _radioLock.AquireLock(channel, radioAddress, radioDataRate);
        }

        public void Dispose()
        {
            _radioLock.ReleaseLock();
        }

        public byte[] SendData(byte[] packetData)
        {
            return _radioLock.Driver.SendData(packetData);
        }
    }
}
