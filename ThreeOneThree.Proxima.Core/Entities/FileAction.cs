using MongoDB.Bson.Serialization.Attributes;

namespace ThreeOneThree.Proxima.Core.Entities
{
    [BsonDiscriminator(RootClass = true)]
    [BsonKnownTypes(typeof(UpdateAction), typeof(DeleteAction), typeof(RenameAction))]
    public abstract class FileAction : MongoEntity
    {
        public MongoRef<MonitoredMountpoint> Mountpoint { get; set; }

        public string RelativePath { get; set; }
        public string RawPath { get; set; }
        public MongoRef<RawUSNEntry> USNEntry { get; set; }
        public long USN { get; set; }
        public bool IsDirectory { get; set; }
    }
}