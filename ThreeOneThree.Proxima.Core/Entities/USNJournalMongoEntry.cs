using System;

namespace ThreeOneThree.Proxima.Core.Entities
{
    public class USNJournalMongoEntry : MongoEntity
    {
        public string MachineName { get; set; }
        public string Path { get; set; }
        public string UniversalPath { get; set; }
        public bool? File { get; set; }
        public bool? Directory { get; set; }

        public ulong FRN { get; set; }

        public ulong PFRN { get; set; }

        public long RecordLength { get; set; }

        public long USN { get; set; }

        public bool? CausedBySync { get; set; }

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
    }
}