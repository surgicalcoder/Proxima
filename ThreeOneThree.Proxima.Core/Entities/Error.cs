using System;

namespace ThreeOneThree.Proxima.Core.Entities
{
    public class Error : MongoEntity
    {
        public Exception Exception { get; set; }
        public USNJournalSyncLog SyncLog { get; set; }
        public string Message { get; set; }
        public string ItemId { get; set; }
    }
}