namespace CrazyflieDotNet.Crazyflie.Feature
{
    /// <summary>
    /// Allows to send high level commands to the crazyflie.
    /// </summary>
    public interface ICrazyflieHighlevelCommander
    {

        /// <summary>
        /// Set the group mask that the Crazyflie belongs to
        /// </summary>
        /// <param name="groupMask">mask for which groups this CF belongs to</param>
        void SetGroupMask(byte groupMask = HighlevelCommander.ALL_GROUPS);                

        /// <summary>
        /// vertical takeoff from current x-y position to given height
        /// </summary>
        /// <param name="absoluteHeightM"> absolut(m)</param>
        /// <param name="durationInSec">time it should take until target height is reached(s)</param>
        /// <param name="groupMask">mask for which CFs this should apply to</param>
        void Takeoff(float absoluteHeightM, float durationInSec, byte groupMask = HighlevelCommander.ALL_GROUPS);

        /// <summary>
        /// vertical land from current x-y position to given height
        /// </summary>
        /// <param name="absoluteHeightM">absolut(m)</param>
        /// <param name="durationInSec">time it should take until target height is reached(s)</param>
        /// <param name="groupMask">mask for which CFs this should apply to</param>
        void Land(float absoluteHeightM, float durationInSec, byte groupMask = HighlevelCommander.ALL_GROUPS);

    }
}
