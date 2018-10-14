using CrazyflieDotNet.CrazyMessaging;
using CrazyflieDotNet.CrazyMessaging.Protocol;
using System;

namespace CrazyflieDotNet.Crazyflie.Feature
{

    /// <summary>
    /// Used for sending control setpoints to the Crazyflie
    /// </summary>
    public class Commander
    {
        private enum CommandTypeGenericCommander
        {
            TYPE_STOP = 0,
            TYPE_VELOCITY_WORLD = 1,
            TYPE_ZDISTANCE = 2,
            TYPE_HOVER = 5,
            TYPE_POSITION = 7
        }

        private readonly ICrtpCommunicator _communicator;

        public bool ClientXMode
        {
            get;
            set;
        }

        /// <summary>
        /// Initialize the commander object. By default the commander is in
        /// +-mode(not x-mode).
        /// </summary>
        public Commander(ICrtpCommunicator communicator, bool clientXMode = false)
        {
            _communicator = communicator;
            ClientXMode = clientXMode;
        }

        private void Send(MessageBuilder builder)
        {
            var message = builder.Build();
            _communicator.SendMessage(message);
        }

        /// <summary>
        /// Send a new control setpoint for roll/pitch/yaw/thrust to the copter
        /// </summary>
        public void SendSetPoint(float roll, float pitch, float yaw, ushort thrust)
        {
            if (thrust > 0xFFFF)
            {
                throw new ArgumentException("trust must be smaller than 0xFFFF", nameof(thrust));
            }

            // The arguments roll / pitch / yaw / trust is the new setpoints that should
            /// be sent to the copter
            if (ClientXMode)
            {
                roll = 0.707f * (roll - pitch);
                pitch = 0.707f * (roll + pitch);
            }

            //     Commander Payload Format:
            //     Name   |  Index  |  Type  |  Size (bytes)
            //     roll        0       float      4
            //     pitch       4       float      4
            //     yaw         8       float      4
            //     thrust      12      ushort     2
            //     .............................total: 14 bytes            

            var builder = new MessageBuilder(                
                (byte)CrtpPort.COMMANDER, (byte)CrtpChannel.Channel0);
            builder.Add(roll);
            builder.Add(-pitch);
            builder.Add(yaw);
            builder.Add(thrust);

            Send(builder);
        }        

        /// <summary>
        /// Send STOP setpoing, stopping the motors and(potentially) falling.
        /// </summary>
        public void SendStopSetPoint()
        {
            byte command = (byte)CommandTypeGenericCommander.TYPE_STOP;

            var builder = new MessageBuilder(
                (byte)CrtpPort.COMMANDER_GENERIC, (byte)CrtpChannel.Channel0);
            builder.Add(command);

            Send(builder);
        }

        /// <summary>
        /// Send Velocity in the world frame of reference setpoint.
        /// </summary>
        public void SendVelocityWorldSetpoint(float vx, float vy, float vz, float yawrate)
        {
            byte command = (byte)CommandTypeGenericCommander.TYPE_VELOCITY_WORLD;

            var builder = new MessageBuilder(                
                (byte)CrtpPort.COMMANDER_GENERIC, (byte)CrtpChannel.Channel0);
            builder.Add(command);
            builder.Add(vx);
            builder.Add(vy);
            builder.Add(vz);
            builder.Add(yawrate);

            Send(builder);
        }

        /// <summary>
        /// Control mode where the height is send as an absolute setpoint(intended
        /// to be the distance to the surface under the Crazflie).
        /// Roll, pitch, yawrate are defined as degrees, degrees, degrees/s
        /// </summary>
        public void SendZdistanceSetPoint(float roll, float pitch, float yawrate, float zdistance)
        {
            byte command = (byte)CommandTypeGenericCommander.TYPE_ZDISTANCE;

            var builder = new MessageBuilder(
                (byte)CrtpPort.COMMANDER_GENERIC, (byte)CrtpChannel.Channel0);
            builder.Add(command);
            builder.Add(roll);
            builder.Add(pitch);
            builder.Add(yawrate);
            builder.Add(zdistance);

            Send(builder);
        }

        /// <summary>
        /// Control mode where the height is send as an absolute setpoint(intended
        /// to be the distance to the surface under the Crazyflie).
        /// vx and vy are in m/s
        /// yawrate is in degrees/s
        /// </summary>
        public void SendHoverSetpoint(float vx, float vy, float yawrate, float zdistance)
        {
            byte command = (byte)CommandTypeGenericCommander.TYPE_HOVER;

            var builder = new MessageBuilder(
                (byte)CrtpPort.COMMANDER_GENERIC, (byte)CrtpChannel.Channel0);
            builder.Add(command);
            builder.Add(vx);
            builder.Add(vy);
            builder.Add(yawrate);
            builder.Add(zdistance);

            Send(builder);
        }

        /// <summary>
        ///     Control mode where the position is sent as absolute x, y, z coordinate in
        /// meter and the yaw is the absolute orientation.
        /// x and y are in m/s
        /// yaw is in degrees/s
        public void SendPositionSetpoint(float x, float y, float z, float yaw)
        {
            byte command = (byte)CommandTypeGenericCommander.TYPE_POSITION;

            var builder = new MessageBuilder(
                (byte)CrtpPort.COMMANDER_GENERIC, (byte)CrtpChannel.Channel0);
            builder.Add(command);
            builder.Add(x);
            builder.Add(y);
            builder.Add(z);
            builder.Add(yaw);

            Send(builder);
        }

    }
}
