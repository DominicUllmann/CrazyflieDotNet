namespace CrazyflieDotNet.Crazyflie.Feature.Common
{
    /// <summary>
    /// Basic interface every toc type (like log, param) needs to implement.
    /// </summary>
    public interface ITocElement
    {
        string Name { get; }
        string Group { get; }
        ushort Identifier { get; }

        void InitializeFrom(ushort identifier, byte[] data);
    }
}
