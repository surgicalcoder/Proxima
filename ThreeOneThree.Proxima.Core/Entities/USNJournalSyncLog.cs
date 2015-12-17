using System;

namespace ThreeOneThree.Proxima.Core.Entities
{
    public class USNJournalSyncLog : MongoEntity
    {
        public string SourceMachine { get; set; }

        public string DestinationMachine { get; set; }


        public MongoRef<USNJournalMongoEntry> Entry { get; set; }

        public DateTime Enqueued { get; set; }

        public DateTime? ActionStartDate { get; set; }
        public DateTime? ActionFinishDate { get; set; }

        public bool Successfull { get; set; }

        public FileAction Action { get; set; }

    }
}