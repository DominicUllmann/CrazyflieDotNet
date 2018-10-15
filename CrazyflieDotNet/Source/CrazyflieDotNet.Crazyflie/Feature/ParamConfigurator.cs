using CrazyflieDotNet.Crazyflie.Feature.Common;
using CrazyflieDotNet.Crazyflie.Feature.Parameter;
using CrazyflieDotNet.CrazyMessaging;
using CrazyflieDotNet.CrazyMessaging.Protocol;

namespace CrazyflieDotNet.Crazyflie.Feature
{
    /// <summary>
    /// implements the access to parameters.
    /// </summary>
    internal class ParamConfigurator : TocContainerBase<ParamTocElement>, IParamConfigurator
    {


        internal ParamConfigurator(ICrtpCommunicator communicator, bool useV2Protocol) : 
            base(communicator, useV2Protocol, (byte)CrtpPort.PARAM)
        {
            _useV2Protocol = useV2Protocol;
            _communicator = communicator;
            _communicator.RegisterEventHandler((byte)CrtpPort.PARAM, ParamPacketReceived);            
        }

        protected override void StartLoadToc()
        {
            FetchTocFromTocFetcher();
        }

        private void ParamPacketReceived(CrtpMessage message)
        {
            // TODO
        }
    }
}
