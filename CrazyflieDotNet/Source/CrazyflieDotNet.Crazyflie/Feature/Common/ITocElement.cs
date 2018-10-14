namespace CrazyflieDotNet.Crazyflie.Feature.Common
{
    public interface ITocElement
    {
        string Name { get; }
        string Group { get; }
        ushort Identifier { get; }

        void InitializeFrom(ushort identifier, byte[] data);
    }
}
