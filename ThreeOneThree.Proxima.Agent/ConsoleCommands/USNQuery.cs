using System;
using PowerArgs;
using ThreeOneThree.Proxima.Core;

namespace ThreeOneThree.Proxima.Agent.ConsoleCommands
{
    [ArgExceptionBehavior(ArgExceptionPolicy.StandardExceptionHandling)]
    public class USNQuery
    {

        [ArgActionMethod]
        public void Position(string Path)
        {
            NtfsUsnJournal journal = new NtfsUsnJournal(Path);
            Win32Api.USN_JOURNAL_DATA journalData = new Win32Api.USN_JOURNAL_DATA();
            var usnJournalReturnCode = journal.GetUsnJournalState(ref journalData);

            if (usnJournalReturnCode == NtfsUsnJournal.UsnJournalReturnCode.USN_JOURNAL_SUCCESS)
            {
                Console.WriteLine("Next USN: " + journalData.NextUsn);
            }
        }

        [ArgActionMethod]
        public void GetUSNDetails(string Mountpoint)
        {
            using (var journal = new NtfsUsnJournal(Mountpoint))
            {
                Win32Api.USN_JOURNAL_DATA data = new Win32Api.USN_JOURNAL_DATA();
                var usnJournalReturnCode = journal.GetUsnJournalState(ref data);
                if (usnJournalReturnCode == NtfsUsnJournal.UsnJournalReturnCode.USN_JOURNAL_SUCCESS)
                {
                    Console.WriteLine("Journal ID: " + data.UsnJournalID);
                    Console.WriteLine("Allocation Delta: " + data.AllocationDelta);
                    Console.WriteLine("First USN: " + data.FirstUsn);
                    Console.WriteLine("Lowest Valid USN: " + data.LowestValidUsn);
                    Console.WriteLine("Max USN: " + data.MaxUsn);
                    Console.WriteLine("Maximum Size: " + data.MaximumSize);
                    Console.WriteLine("Next Usn: " + data.NextUsn);

                }
                else
                {
                    Console.WriteLine("ERROR: " + usnJournalReturnCode.ToString());
                }

            }
        }
    }
}