using CrazyflieDotNet.Crazyflie.Feature.Log;

namespace CrazyflieDotNet.Crazyflie.Feature
{
    /// <summary>
    /// Interface of the logger port of the crazyflie.
    /// </summary>
    public interface ICrazyflieLogger
    {
        /// <summary>
        /// Create a new empty log config entry which can then be customized.
        /// To enable logging for this entry, call <see cref="AddConfig"/> afterwards.
        /// </summary>
        /// <param name="name">the name of the log entry</param>
        /// <param name="period">the send interval in ms (max 2550ms).</param>
        LogConfig CreateEmptyLogConfigEntry(string name, ushort period);

        /// <summary>
        /// Add a log configuration to the logging framework.
        ///
        /// When doing this the contents of the log configuration will be validated
        /// and listeners for new log configurations will be notified. When
        /// validating the configuration the variables are checked against the TOC
        /// to see that they actually exist.If they don't then the configuration
        /// cannot be used.Since a valid TOC is required, a Crazyflie has to be
        /// connected when calling this method, otherwise it will fail.
        /// </summary>  
        void AddConfig(LogConfig config);

        /// <summary>
        /// After the log config has been added, it can be started.
        /// After it is started, the crazyflie sends log entries.
        /// </summary>
        void StartConfig(LogConfig config);


        /// <summary>
        /// Stops and added config.
        /// </summary>
        void StopConfig(LogConfig config);

        /// <summary>
        /// Delete and added config.
        /// </summary>
        void DeleteConfig(LogConfig config);

        /// <summary>
        /// Returns true if the log variable is known in the toc.
        /// </summary>
        bool IsLogVariableKnown(string completeName);

    }
}