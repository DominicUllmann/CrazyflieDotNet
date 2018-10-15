namespace CrazyflieDotNet.Crazyflie.Feature
{
    /// <summary>
    /// Allows to send low level commands to the crazyflie.
    /// </summary>
    public interface ICrazyflieCommander
    {

        /// <summary>
        /// Send a new control setpoint for roll/pitch/yaw/thrust to the copter
        /// </summary>
        void SendSetPoint(float roll, float pitch, float yaw, ushort thrust);

        /// <summary>
        /// Send STOP setpoing, stopping the motors and(potentially) falling.
        /// </summary>
        void SendStopSetPoint();

        /// <summary>
        /// Send Velocity in the world frame of reference setpoint.
        /// </summary>
        void SendVelocityWorldSetpoint(float vx, float vy, float vz, float yawrate);

        /// <summary>
        /// Control mode where the height is send as an absolute setpoint(intended
        /// to be the distance to the surface under the Crazflie).
        /// Roll, pitch, yawrate are defined as degrees, degrees, degrees/s
        /// </summary>
        void SendZdistanceSetPoint(float roll, float pitch, float yawrate, float zdistance);


        /// <summary>
        /// Control mode where the height is send as an absolute setpoint(intended
        /// to be the distance to the surface under the Crazyflie).
        /// vx and vy are in m/s
        /// yawrate is in degrees/s
        /// </summary>
        void SendHoverSetpoint(float vx, float vy, float yawrate, float zdistance);


        /// <summary>
        /// Control mode where the position is sent as absolute x, y, z coordinate in
        /// meter and the yaw is the absolute orientation.
        /// x and y are in m/s
        /// yaw is in degrees/s
        void SendPositionSetpoint(float x, float y, float z, float yaw);

    }
}
