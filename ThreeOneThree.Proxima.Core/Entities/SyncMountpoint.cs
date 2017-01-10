using MongoDB.Bson;

namespace ThreeOneThree.Proxima.Core.Entities
{
    public class SyncMountpoint : MongoEntity
    {
        public MongoRef<MonitoredMountpoint> Mountpoint { get; set; }

        public MongoRef<Server> DestinationServer { get; set; }

        public string Path { get; set; }

        public string RelativePathStartFilter { get; set; }

        public long LastUSN { get; set; }
    }
}