namespace CrazyflieDotNet.Crazyflie.Feature.Common
{
    public interface ITocContainer<T> where T: ITocElement
    {
        Toc<T> CurrentLogToc
        {
            get;
        }

    }
}
