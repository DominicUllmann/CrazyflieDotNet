namespace CrazyflieDotNet.Crazyflie.Feature.Localization
{
    public class Position
    {

        private float X { get; }
        private float Y { get; }

        private float Z { get; }

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
