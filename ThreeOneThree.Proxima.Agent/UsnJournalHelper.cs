using System.IO;

namespace ThreeOneThree.Proxima.Agent
{
    public static class UsnJournalHelper
    {
        public static Win32Api.USN_JOURNAL_DATA GetCurrentUSNJournalData(string DriveLetter)
        {
            NtfsUsnJournal journal = new NtfsUsnJournal(DriveLetter);

            Win32Api.USN_JOURNAL_DATA journalState = new Win32Api.USN_JOURNAL_DATA();

            NtfsUsnJournal.UsnJournalReturnCode rtn = journal.GetUsnJournalState(ref journalState);

            if (rtn == NtfsUsnJournal.UsnJournalReturnCode.USN_JOURNAL_SUCCESS)
            {

                return journalState;
            }
            else
            {
                throw new UsnJournalException(rtn);
            }
        }
    }
}