using System.Collections.Generic;
using Amib.Threading;
using ThreeOneThree.Proxima.Core;
using ThreeOneThree.Proxima.Core.Entities;

namespace ThreeOneThree.Proxima.Agent
{
    public sealed class Singleton
    {
        static readonly Singleton _instance = new Singleton();
        public static Singleton Instance => _instance;

        Singleton()
        {
            ThreadPool = new SmartThreadPool();
            Repository = new Repository();
        }

        public SmartThreadPool ThreadPool { get; set; }

        public Repository Repository { get; set; }

        public List<SyncMountpoint> DestinationMountpoints { get; set; }

        public List<MonitoredMountpoint> SourceMountpoints { get; set; }

        public List<Server>  Servers { get; set; }

        public Server CurrentServer { get; set; }
    }
}