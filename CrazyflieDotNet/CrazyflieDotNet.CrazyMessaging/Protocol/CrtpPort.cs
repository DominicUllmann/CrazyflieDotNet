namespace CrazyflieDotNet.CrazyMessaging.Protocol
{

    /// <summary>
    /// Lists the available ports for the CRTP.
    /// </summary>
    public enum CrtpPort : byte
    {                
        CONSOLE = 0x00,
        PARAM = 0x02,
        COMMANDER = 0x03,
        MEM = 0x04,
        LOGGING = 0x05,
        LOCALIZATION = 0x06,
        COMMANDER_GENERIC = 0x07,
        SETPOINT_HL = 0x08,
        PLATFORM = 0x0D,
        DEBUGDRIVER = 0x0E,
        LINKCTRL = 0x0F,
        ALL = 0xFF
    }
}
