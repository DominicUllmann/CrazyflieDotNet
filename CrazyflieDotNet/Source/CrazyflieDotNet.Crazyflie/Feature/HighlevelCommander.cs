using CrazyflieDotNet.CrazyMessaging;
using CrazyflieDotNet.CrazyMessaging.Protocol;
using System;
using System.Collections.Generic;
using System.Text;

namespace CrazyflieDotNet.Crazyflie.Feature
{
    /// <summary>
    /// Used for sending high level setpoints to the Crazyflie
    /// </summary>
    internal class HighlevelCommander : ICrazyflieHighlevelCommander
    {

        public const byte ALL_GROUPS = 0;

        private enum HighlevelCommands : byte
        {
            COMMAND_SET_GROUP_MASK = 0,
            COMMAND_TAKEOFF = 1,
            COMMAND_LAND = 2,
            COMMAND_STOP = 3,
            COMMAND_GO_TO = 4,
            COMMAND_START_TRAJECTORY = 5,
            COMMAND_DEFINE_TRAJECTORY = 6,
        }

        private ICrtpCommunicator _communicator;

        internal HighlevelCommander(ICrtpCommunicator communicator)
        {
            _communicator = communicator;
        }

        private void Send(MessageBuilder builder)
        {
            var message = builder.Build();
            _communicator.SendMessage(message);
        }

        public void SetGroupMask(byte groupMask = 0)
        {
            byte command = (byte)HighlevelCommands.COMMAND_SET_GROUP_MASK;

            var builder = new MessageBuilder(
                (byte)CrtpPort.SETPOINT_HL, (byte)CrtpChannel.Channel0);
            builder.Add(command);
            builder.Add(groupMask);

            Send(builder);
        }

        public void Takeoff(float absoluteHeightM, float durationInSec, byte groupMask = 0)
        {
            byte command = (byte)HighlevelCommands.COMMAND_TAKEOFF;

            var builder = new MessageBuilder(
                (byte)CrtpPort.SETPOINT_HL, (byte)CrtpChannel.Channel0);
            builder.Add(command);
            builder.Add(groupMask);            
            builder.Add(absoluteHeightM);
            builder.Add(durationInSec);

            Send(builder);
        }

        public void Land(float absoluteHeightM, float durationInSec, byte groupMask = 0)
        {
            byte command = (byte)HighlevelCommands.COMMAND_LAND;

            var builder = new MessageBuilder(
                (byte)CrtpPort.SETPOINT_HL, (byte)CrtpChannel.Channel0);
            builder.Add(command);
            builder.Add(groupMask);
            builder.Add(absoluteHeightM);
            builder.Add(durationInSec);

            Send(builder);
        }
    }
}
