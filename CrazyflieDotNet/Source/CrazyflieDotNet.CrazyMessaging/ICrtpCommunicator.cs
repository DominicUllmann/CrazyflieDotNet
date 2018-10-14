using CrazyflieDotNet.CrazyMessaging.Protocol;

namespace CrazyflieDotNet.CrazyMessaging
{

    /// <summary>
    /// The interface used to interact with the crazyfly on the communication level.
    /// </summary>
    public interface ICrtpCommunicator
    {
        void SendMessage(CrtpMessage message);

        void RegisterEventHandler(byte port, CrtpEventCallback crtpEventCallback);

        void RemoveEventHandler(byte port, CrtpEventCallback crtpEventCallback);
    }
}
