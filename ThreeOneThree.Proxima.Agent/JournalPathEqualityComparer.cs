using System;
using System.Collections.Generic;
using ThreeOneThree.Proxima.Core;
using ThreeOneThree.Proxima.Core.Entities;

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

    public class FileActionPathEqualityComparer : IEqualityComparer<FileAction>
    {
        public bool Equals(FileAction x, FileAction y)
        {
            return string.Equals(x.Path, y.Path);
        }

        public int GetHashCode(FileAction obj)
        {
            return obj.Path.GetHashCode();
        }
    }
}