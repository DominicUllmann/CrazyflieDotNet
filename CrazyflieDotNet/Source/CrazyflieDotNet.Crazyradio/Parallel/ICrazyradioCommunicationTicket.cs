using System;


namespace CrazyflieDotNet.Crazyradio.Parallel
{
    /// <summary>
    /// The communication ticket allows to communicate with the
    /// crazyradio exclusively. To release the lock, call dispose.
    /// As long as you hold the lock, it's not possible to communicate with another crazyflie.
    /// </summary>
    public interface ICrazyradioCommunicationTicket : IDisposable
    {
        /// <summary>
        /// Sends a packet of data in array of byte form via the Crazyradio USB dongle.
        /// </summary>
        /// <param name="packetData">The array of bytes to send by this Crazyradio USB dongle.</param>
        /// <returns>the response message</returns>
        byte[] SendData(byte[] packetData);
    }
}
