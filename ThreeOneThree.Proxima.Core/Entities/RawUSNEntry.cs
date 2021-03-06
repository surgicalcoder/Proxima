using System;
using MongoDB.Bson.Serialization.Attributes;

namespace ThreeOneThree.Proxima.Core.Entities
{
    public class RawUSNEntry : MongoEntity
    {
        public MongoRef<MonitoredMountpoint> Mountpoint { get; set; }
        public string Path { get; set; }

        public string RenameFromPath { get; set; }
        public string RenameFromRelativePath { get; set; }

        public string RelativePath { get; set; }
        public string SourceInfo { get; set; }
        public bool? File { get; set; }
        public bool? Directory { get; set; }

        public ulong FRN { get; set; }

        public ulong PFRN { get; set; }

        public long RecordLength { get; set; }

        public long USN { get; set; }

        //public bool CausedBySync { get; set; }

        public DateTime TimeStamp { get; set; }

        public bool? DataOverwrite { get; set; }
        public bool? DataExtend { get; set; }
        public bool? DataTruncation { get; set; }
        public bool? NamedDataOverwrite { get; set; }
        public bool? NamedDataExtend { get; set; }
        public bool? NamedDataTruncation { get; set; }
        public bool? FileCreate { get; set; }
        public bool? FileDelete { get; set; }
        public bool? EaChange { get; set; }
        public bool? SecurityChange { get; set; }
        public bool? RenameOldName { get; set; }
        public bool? RenameNewName { get; set; }
        public bool? IndexableChange { get; set; }
        public bool? BasicInfoChange { get; set; }
        public bool? HardLinkChange { get; set; }
        public bool? CompressionChange { get; set; }
        public bool? EncryptionChange { get; set; }
        public bool? ObjectIdChange { get; set; }
        public bool? ReparsePointChange { get; set; }
        public bool? StreamChange { get; set; }
        public bool? Close { get; set; }

        public bool? SystemFile{ get; set; }
        public USNChangeRange ChangeRange { get; set; }
    }

    public class USNChangeRange : MongoEntity
    {
        public ulong FRN { get; set; }

        public DateTime Min { get; set; }
        public DateTime Max { get; set; }

        public bool Closed { get; set; }

        
        public Win32Api.UsnEntry Entry { get; set; }

        
        public Win32Api.UsnEntry RenameFrom { get; set; }
    }

    public class OpenChangeRange : MongoEntity
    {
        public USNChangeRange ChangeRange { get; set; }
    }



}