using System.Collections.Generic;
using Amib.Threading;
using ThreeOneThree.Proxima.Core;

namespace ThreeOneThree.Proxima.Agent
{
    public sealed class USNJournalSingleton
    {
        static readonly USNJournalSingleton _instance = new USNJournalSingleton();
        public static USNJournalSingleton Instance => _instance;

        USNJournalSingleton()
        {
            ThreadPool = new SmartThreadPool();
            Repository = new Repository();
        }

        public SmartThreadPool ThreadPool { get; set; }

        public Repository Repository { get; set; }

        public List<DriveConstruct> DrivesToMonitor { get; set; }
    }
}