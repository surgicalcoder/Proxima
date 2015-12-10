using System.Collections.Generic;

namespace ThreeOneThree.Proxima.Agent
{
    public sealed class USNJournalSingleton
    {
        static readonly USNJournalSingleton _instance = new USNJournalSingleton();
        public static USNJournalSingleton Instance => _instance;

        USNJournalSingleton() { }

        //public Win32Api.USN_JOURNAL_DATA CurrentStatus { get; set; }

        

        public List<DriveConstruct> DrivesToMonitor { get; set; }
    }
}