using System.Collections.Generic;
using ThreeOneThree.Proxima.Core;

namespace ThreeOneThree.Proxima.Agent
{
    public class JournalPathEqualityComparer : IEqualityComparer<USNJournalMongoEntry>
    {
        public bool Equals(USNJournalMongoEntry x, USNJournalMongoEntry y)
        {
            return x.Path.Equals(y.Path);
        }

        public int GetHashCode(USNJournalMongoEntry obj)
        {
            return obj.Path.GetHashCode();
        }
    }
}