namespace ThreeOneThree.Proxima.Core.Entities
{
    public class MonitoredMountpoint : MongoEntity
    {
        public MongoRef<Server> Server { get; set; }

        public string MountPoint { get; set; }

        public string PublicPath { get; set; }

        public string Volume { get; set; }

        public long CurrentUSNLocation { get; set; }

        public override string ToString()
        {
            return $"MountPoint: {MountPoint}, CurrentUSNLocation: {CurrentUSNLocation}";
        }
    }
}