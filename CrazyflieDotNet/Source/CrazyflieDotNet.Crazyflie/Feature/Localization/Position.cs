namespace CrazyflieDotNet.Crazyflie.Feature.Localization
{
    public class Position
    {

        public float X { get; }
        public float Y { get; }

        public float Z { get; }

        public Position(float x, float y, float z)
        {
            X = x;
            Y = y;
            Z = z;
        }

        public override string ToString()
        {
            return $"({X} / {Y} / {Z})";
        }



    }
}
