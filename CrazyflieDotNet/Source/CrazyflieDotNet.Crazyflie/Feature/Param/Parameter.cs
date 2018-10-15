using CrazyflieDotNet.CrazyMessaging;
using log4net;

namespace CrazyflieDotNet.Crazyflie.Feature.Param
{
    public class Parameter
    {
        private static readonly ILog _log = LogManager.GetLogger(typeof(Parameter));
        private ICrtpCommunicator _communicator;

        internal Parameter(ICrtpCommunicator communicator, string name)
        {
            _communicator = communicator;
        }



    }
}
