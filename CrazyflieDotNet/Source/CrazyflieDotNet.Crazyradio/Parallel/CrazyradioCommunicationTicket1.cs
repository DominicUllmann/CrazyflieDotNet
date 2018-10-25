using CrazyflieDotNet.Crazyradio.Driver;

namespace CrazyflieDotNet.Crazyradio.Parallel
{

    /// <summary>
    /// See <see cref="ICrazyradioCommunicationTicket"/>.
    /// </summary>
    internal class CrazyradioCommunicationTicket : ICrazyradioCommunicationTicket
    {
        private CrazyradioCommunicationLock _radioLock;

        public CrazyradioCommunicationTicket(CrazyradioCommunicationLock radioLock, RadioChannel channel, RadioAddress radioAddress, RadioDataRate radioDataRate)
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
