using Topshelf;

namespace ThreeOneThree.Proxima.Agent
{
    public class Service : ServiceControl
    {

        
        public bool Start(HostControl hostControl)
        {
            Singleton.Instance.ThreadPool.MaxThreads = 64;
            return true;
        }

        public bool Stop(HostControl hostControl)
        {
            return true;
        }
        
    }
}