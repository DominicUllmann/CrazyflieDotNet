using CrazyflieDotNet.CrazyMessaging;
using CrazyflieDotNet.CrazyMessaging.Protocol;
using System;
using System.Threading.Tasks;

namespace CrazyflieDotNet.Crazyflie.Feature
{
    /// <summary>
    /// Used for sending high level setpoints to the Crazyflie
    /// </summary>
    internal class HighlevelCommander : ICrazyflieHighlevelCommander
    {

        public const byte ALL_GROUPS = 0;
        private bool _enabled;

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
        private ICrazyflieParamConfigurator _paramConfigurator;

        internal HighlevelCommander(ICrtpCommunicator communicator, ICrazyflieParamConfigurator paramConfigurator)
        {
            _communicator = communicator;
            _paramConfigurator = paramConfigurator;
        }

        private void EnsureEnabled()
        {
            if (!_enabled)
            {
                throw new InvalidOperationException("please call and wait for enable first.");
            }
        }

        private void Send(MessageBuilder builder)
        {
            EnsureEnabled();

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

        public Task Enable()
        {
            return _paramConfigurator.SetValue("commander.enHighLevel", (byte)1).
                ContinueWith((state) => { _enabled = true;  });
        }

        public Task Disable()
        {
            return _paramConfigurator.SetValue("commander.enHighLevel", (byte)0).
                ContinueWith((state) => { _enabled = false; });
        }

        public void GoTo(float x, float y, float z, float yaw, float durationInSec, bool relative = false, byte groupMask = 0)
        {
            byte command = (byte)HighlevelCommands.COMMAND_GO_TO;

            var builder = new MessageBuilder(
                (byte)CrtpPort.SETPOINT_HL, (byte)CrtpChannel.Channel0);
            builder.Add(command);
            builder.Add(groupMask);
            builder.Add(relative ? (byte)1 : (byte)0);
            builder.Add(x);
            builder.Add(y);
            builder.Add(z);
            builder.Add(yaw);
            builder.Add(durationInSec);

            Send(builder);
        }




}
}
