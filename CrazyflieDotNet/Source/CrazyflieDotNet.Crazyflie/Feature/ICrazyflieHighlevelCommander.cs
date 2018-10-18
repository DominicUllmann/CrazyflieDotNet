using System.Threading.Tasks;

namespace CrazyflieDotNet.Crazyflie.Feature
{
    /// <summary>
    /// Allows to send high level commands to the crazyflie.
    /// </summary>
    /// <remarks>It's required to enable the high level commander first by setting a parameter on the crazyflie.
    /// For this, call enable.</remarks>
    public interface ICrazyflieHighlevelCommander
    {

        /// <summary>
        /// This enables the high level commander on the crazyflie.
        /// </summary>
        Task Enable();

        /// <summary>
        /// Disable the high level commander again.
        /// </summary>        
        Task Disable();

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

        /// <summary>
        /// Go to an absolute or relative position
        /// </summary>
        /// <param name="x">x(m)</param>
        /// <param name="y">y(m)</param>
        /// <param name="z">z(m)</param>
        /// <param name="yaw">yaw(radians)</param>
        /// <param name="durationInSec">time it should take to reach the position(s)</param>
        /// <param name="relative">true if x,y,z is relative to current position, otherwise false.</param>
        /// <param name="groupMask">mask for which CFs this should apply to</param>
        void GoTo(float x, float y, float z, float yaw, float durationInSec, bool relative = false, byte groupMask = HighlevelCommander.ALL_GROUPS);

        /// <summary>
        /// Stops the crazyfly.
        /// </summary>
        void Stop(byte groupMask = HighlevelCommander.ALL_GROUPS);
    }
}
