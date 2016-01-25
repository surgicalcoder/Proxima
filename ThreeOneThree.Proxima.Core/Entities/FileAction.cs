namespace ThreeOneThree.Proxima.Core.Entities
{
    public abstract class FileAction : MongoEntity
    {
        public MongoRef<MonitoredMountpoint> Mountpoint { get; set; }

        public string RelativePath { get; set; }
        public string RawPath { get; set; }
        public MongoRef<RawUSNEntry> USN { get; set; }

        public bool IsDirectory { get; set; }
    }

    public class DeleteAction : FileAction
    {
        
    }

    public class UpdateAction : FileAction
    {
        
    }

    public class RenameAction : FileAction
    {
        public string RenameFrom { get; set; }
        public string RenameTo { get; set; }
    }
}