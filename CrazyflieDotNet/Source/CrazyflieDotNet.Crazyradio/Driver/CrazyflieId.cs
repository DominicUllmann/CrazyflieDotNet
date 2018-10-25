using System;

namespace CrazyflieDotNet.Crazyradio.Driver
{
    public class CrazyflieId : IEquatable<CrazyflieId>
    {
        public RadioChannel RadioChannel {get;}
        public RadioDataRate RadioDataRate { get; }
        public RadioAddress RadioAddress { get; }

        internal CrazyflieId(RadioChannel radioChannel, RadioDataRate radioDataRate, RadioAddress radioAddress)
        {
            RadioChannel = radioChannel;
            RadioDataRate = radioDataRate;
            RadioAddress = radioAddress;
        }

        public bool Equals(CrazyflieId other)
        {
            if (other == null)
            {
                return false;
            }         
            return RadioChannel == other.RadioChannel && RadioDataRate == other.RadioDataRate && Object.Equals(other.RadioAddress, RadioAddress);
        }

        public override bool Equals(object other)
        {
            return Equals((CrazyflieId)other);
        }

        public override int GetHashCode()
        {
            // TODO
            return (RadioAddress != null ? RadioAddress.GetHashCode() : 0)
                ^ RadioChannel.GetHashCode() ^ RadioAddress.GetHashCode();
        }
    }
}
