using System;

namespace ThreeOneThree.Proxima.Agent
{
    public class UsnJournalException : Exception
    {
        public NtfsUsnJournal.UsnJournalReturnCode ReturnCode { get; set; }
        public UsnJournalException(NtfsUsnJournal.UsnJournalReturnCode rtn)
        {
            ReturnCode = rtn;
        }
    }
}