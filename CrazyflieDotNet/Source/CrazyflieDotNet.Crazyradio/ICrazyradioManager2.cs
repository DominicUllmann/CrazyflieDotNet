using System.Collections.Generic;

namespace CrazyflieDotNet.Crazyradio
{
    /// <summary>
    /// The RadioManager ensures that the crazyradio can be used by multiple crazyflies.
    /// To use it:    
    /// 
    /// </summary>
    public interface ICrazyradioManager
    {

        /// <summary>
        /// Gets a crazy radio configuration for a given uri.
        /// </summary>
        /// <returns></returns>
        ICrazyradioSelection SelectRadio(CrazyflieUri uri);
        /// <summary>
        /// Free up the usage of the radio for the given uri. If all users have deselected, radio devices will be closed.
        /// </summary>
        void DeselectRadio(CrazyflieUri uri);

        /// <summary>
        /// Scan for all available crazyflies on all devices.
        /// </summary>
        /// <returns></returns>
        IList<CrazyflieUri> Scan();
    }
}
