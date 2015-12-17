namespace ThreeOneThree.Proxima.Core.Entities
{
    public class USNJournalSyncFrom : MongoEntity
    {
        public string SourceMachine { get; set; }

        public string DestinationMachine { get; set; }

        public long CurrentUSNLocation { get; set; }
    }
}