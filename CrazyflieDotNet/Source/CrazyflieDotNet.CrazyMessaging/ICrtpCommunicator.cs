using CrazyflieDotNet.CrazyMessaging.Protocol;
using System;

namespace CrazyflieDotNet.CrazyMessaging
{

    /// <summary>
    /// The interface used to interact with the crazyfly on the communication level.
    /// </summary>
    public interface ICrtpCommunicator
    {
        /// <summary>
        /// Send Message in fire and forget mode.
        /// </summary>
        void SendMessage(CrtpMessage message);

        /// <summary>
        /// Send a message and ensure that it is received by the crazyflie.
        /// (At least once).
        /// </summary>
        /// <param name="message">the message to send.</param>
        /// <param name="timeout">the timeout until resend.</param>
        /// <param name="isExpectedResponse">function which determines if a response message is the expected one.</param>
        void SendMessageExcpectAnswer(CrtpMessage message, Func<CrtpMessage, bool> isExpectedResponse, TimeSpan timeout);

        /// <summary>
        /// Send a message and ensure that it is received by the crazyflie.
        /// (At least once).
        /// </summary>
        /// <param name="message">the message to send.</param>
        /// <param name="timeout">the timeout until resend.</param>
        /// <param name="startResponseContent">the data at the beginning of the message. Convenience form of the isExpectedResponse func.
        /// <see cref="SendMessageExcpectAnswer(CrtpMessage, Func{CrtpMessage, bool}, TimeSpan)"/></param>
        void SendMessageExcpectAnswer(CrtpMessage message, byte[] startResponseContent, TimeSpan timeout);

        void RegisterEventHandler(byte port, CrtpEventCallback crtpEventCallback);

        void RemoveEventHandler(byte port, CrtpEventCallback crtpEventCallback);
    }
}
