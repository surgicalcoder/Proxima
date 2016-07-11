namespace ThreeOneThree.Proxima.Core.Entities
{
    public class Server : MongoEntity
    {
        public string MachineName { get; set; }

        public int NormalCopyLimit { get; set; }

        public int FailedCopyLimit { get; set; }

        public int MaxThreads { get; set; }

        public int SyncCheckInSecs { get; set; }

        public int MonitorCheckInSecs { get; set; }

        public string Version { get; set; }
    }
}