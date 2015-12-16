using Topshelf;

namespace ThreeOneThree.Proxima.Agent
{
    public class Service : ServiceControl
    {

        
        public bool Start(HostControl hostControl)
        {
            USNJournalSingleton.Instance.ThreadPool.MaxThreads = 128;
            return true;
        }

        public bool Stop(HostControl hostControl)
        {
            return true;
        }
        
    }
}