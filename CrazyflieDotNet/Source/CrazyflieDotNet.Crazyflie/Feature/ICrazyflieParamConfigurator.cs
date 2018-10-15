using System;
using System.Threading.Tasks;

namespace CrazyflieDotNet.Crazyflie.Feature
{

    /// <summary>
    /// Allows to interact with the crazyfly parameter table.
    /// </summary>
    public interface ICrazyflieParamConfigurator
    {

        /// <summary>
        /// Request an update of all the parameters in the TOC. 
        /// This downloads all parameters stored in the toc one by one.
        /// </summary>
        void RequestUpdateOfAllParams();

        /// <summary>
        /// The event AllParametersUpdated indicate that RequestUpdateOfAllParams has been completed.
        /// </summary>
        event AllParamsUpdatedEventHandler AllParametersUpdated;

        /// <summary>
        /// Returns the loaded value if already downloaded. Otherwise null.
        /// </summary>
        object GetLoadedParameterValue(string completeName);

        /// <summary>
        /// Asynchronously load a parameter value
        /// </summary>
        Task<object> RefreshParameterValue(string completeName);

        /// <summary>
        /// Sets the given parameter to the provided value.
        /// </summary>
        void SetValue(string completeName, object value);
    }
}
