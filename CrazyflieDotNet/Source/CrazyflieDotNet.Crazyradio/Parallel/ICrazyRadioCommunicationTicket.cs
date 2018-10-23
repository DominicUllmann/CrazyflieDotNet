using System;


namespace CrazyflieDotNet.Crazyradio.Parallel
{
    /// <summary>
    /// The communication ticket allows to communicate with the
    /// crazyradio exclusively. To release the lock, call dispose.
    /// As long as you hold the lock, it's not possible to communicate with another crazyflie.
    /// </summary>
    public interface ICrazyRadioCommunicationTicket : IDisposable
    {
    }
}
