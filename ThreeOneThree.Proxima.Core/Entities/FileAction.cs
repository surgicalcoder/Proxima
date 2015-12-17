namespace ThreeOneThree.Proxima.Core.Entities
{
    public class FileAction
    {
        public bool DeleteFile { get; set; }

        public string RenameTo { get; set; }

        public string RenameFrom { get; set; }
        public string Path { get; set; }

        public long USN { get; set; }
    }
}