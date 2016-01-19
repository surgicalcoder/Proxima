using System;
using System.Collections.Generic;
using ThreeOneThree.Proxima.Core;
using ThreeOneThree.Proxima.Core.Entities;

namespace ThreeOneThree.Proxima.Agent
{
    public class JournalPathEqualityComparer : IEqualityComparer<RawUSNEntry>
    {
        public bool Equals(RawUSNEntry x, RawUSNEntry y)
        {
            return x.Path.Equals(y.Path);
        }

        public int GetHashCode(RawUSNEntry obj)
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