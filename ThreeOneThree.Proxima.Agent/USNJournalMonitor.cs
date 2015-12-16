using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using Quartz;
using ThreeOneThree.Proxima.Core;

namespace ThreeOneThree.Proxima.Agent
{
    [PersistJobDataAfterExecution]
    [DisallowConcurrentExecution]
    public class USNJournalMonitor : IJob
    {

        uint reasonMask = 
            Win32Api.USN_REASON_DATA_OVERWRITE |
            Win32Api.USN_REASON_DATA_EXTEND |
            //Win32Api.USN_REASON_NAMED_DATA_OVERWRITE |
            //Win32Api.USN_REASON_NAMED_DATA_TRUNCATION |
            Win32Api.USN_REASON_FILE_CREATE |
            Win32Api.USN_REASON_FILE_DELETE |
            //Win32Api.USN_REASON_EA_CHANGE |
            //Win32Api.USN_REASON_SECURITY_CHANGE |
            Win32Api.USN_REASON_RENAME_OLD_NAME |
            Win32Api.USN_REASON_RENAME_NEW_NAME |
            //Win32Api.USN_REASON_INDEXABLE_CHANGE |
            Win32Api.USN_REASON_BASIC_INFO_CHANGE |
            //Win32Api.USN_REASON_HARD_LINK_CHANGE |
            //Win32Api.USN_REASON_COMPRESSION_CHANGE |
            //Win32Api.USN_REASON_ENCRYPTION_CHANGE |
            //Win32Api.USN_REASON_OBJECT_ID_CHANGE |
            //Win32Api.USN_REASON_REPARSE_POINT_CHANGE |
            //Win32Api.USN_REASON_STREAM_CHANGE |
            Win32Api.USN_REASON_CLOSE;


        public void Execute(IJobExecutionContext context)
        {
            if (USNJournalSingleton.Instance.DrivesToMonitor == null || USNJournalSingleton.Instance.DrivesToMonitor.Count == 0)
            {
                return;
            }

            using (Repository repo = new Repository())
            {
                List<USNJournalMongoEntry> entries = new List<USNJournalMongoEntry>();

                foreach (var construct in USNJournalSingleton.Instance.DrivesToMonitor)
                {

                    Win32Api.USN_JOURNAL_DATA newUsnState;
                    List<Win32Api.UsnEntry> usnEntries;

                    NtfsUsnJournal journal = new NtfsUsnJournal(construct.DriveInfo);

                    var rtn = journal.GetUsnJournalEntries(construct.CurrentJournalData, reasonMask, out usnEntries, out newUsnState);

                    if (rtn == NtfsUsnJournal.UsnJournalReturnCode.USN_JOURNAL_SUCCESS)
                    {

                        foreach (var entry in usnEntries)
                        {
                            string rawPath;
                            string actualPath;

                            var usnRtnCode = journal.GetPathFromFileReference(entry.ParentFileReferenceNumber, out rawPath);

                            if (usnRtnCode == NtfsUsnJournal.UsnJournalReturnCode.USN_JOURNAL_SUCCESS && 0 != String.Compare(rawPath, "Unavailable", StringComparison.OrdinalIgnoreCase))
                            {
                                actualPath = $"{journal.VolumeName.TrimEnd('\\')}{rawPath}\\{entry.Name}";
                            }
                            else
                            {
                                actualPath = "#UNKNOWN#";
                            }

                            var dbEntry = new USNJournalMongoEntry();
                            dbEntry.Path = actualPath;
                            dbEntry.File = entry.IsFile;
                            dbEntry.Directory = entry.IsFolder;
                            dbEntry.FRN = entry.FileReferenceNumber;
                            dbEntry.PFRN = entry.ParentFileReferenceNumber;
                            dbEntry.RecordLength = entry.RecordLength;
                            dbEntry.USN = entry.USN;
                            dbEntry.MachineName = Environment.MachineName.ToLower();
                            dbEntry.TimeStamp = entry.TimeStamp;
                            dbEntry.UniversalPath = GetRemotePath(actualPath);


                            PopulateFlags(dbEntry, entry);
                            Console.WriteLine("WIBBLE");
                            entries.Add(dbEntry);
                        }

                        construct.CurrentJournalData = newUsnState;

                    }
                    else
                    {
                        throw new UsnJournalException(rtn);
                    }
                }


                repo.Add<USNJournalMongoEntry>(entries);
            }
        }

        private string GetRemotePath(string actualPath)
        {
            return "\\" + Environment.MachineName + "\\" + actualPath.Replace(":", "$");
        }

        private void PopulateFlags(USNJournalMongoEntry dbEntry, Win32Api.UsnEntry entry)
        {
            uint value = entry.Reason & Win32Api.USN_REASON_DATA_OVERWRITE;
            if (0 != value)
            {
                dbEntry.DataOverwrite = true;
            }
            value = entry.Reason & Win32Api.USN_REASON_DATA_EXTEND;
            if (0 != value)
            {
                dbEntry.DataExtend = true;
            }
            value = entry.Reason & Win32Api.USN_REASON_DATA_TRUNCATION;
            if (0 != value)
            {
                dbEntry.DataTruncation = true;
            }
            value = entry.Reason & Win32Api.USN_REASON_NAMED_DATA_OVERWRITE;
            if (0 != value)
            {
                dbEntry.NamedDataOverwrite = true;
            }
            value = entry.Reason & Win32Api.USN_REASON_NAMED_DATA_EXTEND;
            if (0 != value)
            {
                dbEntry.NamedDataExtend = true;
            }
            value = entry.Reason & Win32Api.USN_REASON_NAMED_DATA_TRUNCATION;
            if (0 != value)
            {
                dbEntry.NamedDataTruncation = true;
            }
            value = entry.Reason & Win32Api.USN_REASON_FILE_CREATE;
            if (0 != value)
            {
                dbEntry.FileCreate = true;
            }
            value = entry.Reason & Win32Api.USN_REASON_FILE_DELETE;
            if (0 != value)
            {
                dbEntry.FileDelete = true;
            }
            value = entry.Reason & Win32Api.USN_REASON_EA_CHANGE;
            if (0 != value)
            {
                dbEntry.EaChange = true;
            }
            value = entry.Reason & Win32Api.USN_REASON_SECURITY_CHANGE;
            if (0 != value)
            {
                dbEntry.SecurityChange = true;
            }
            value = entry.Reason & Win32Api.USN_REASON_RENAME_OLD_NAME;
            if (0 != value)
            {
                dbEntry.RenameOldName = true;
            }
            value = entry.Reason & Win32Api.USN_REASON_RENAME_NEW_NAME;
            if (0 != value)
            {
                dbEntry.RenameNewName = true;
            }
            value = entry.Reason & Win32Api.USN_REASON_INDEXABLE_CHANGE;
            if (0 != value)
            {
                dbEntry.IndexableChange = true;
            }
            value = entry.Reason & Win32Api.USN_REASON_BASIC_INFO_CHANGE;
            if (0 != value)
            {
                dbEntry.BasicInfoChange = true;
            }
            value = entry.Reason & Win32Api.USN_REASON_HARD_LINK_CHANGE;
            if (0 != value)
            {
                dbEntry.HardLinkChange = true;
            }
            value = entry.Reason & Win32Api.USN_REASON_COMPRESSION_CHANGE;
            if (0 != value)
            {
                dbEntry.CompressionChange = true;
            }
            value = entry.Reason & Win32Api.USN_REASON_ENCRYPTION_CHANGE;
            if (0 != value)
            {
                dbEntry.EncryptionChange = true;
            }
            value = entry.Reason & Win32Api.USN_REASON_OBJECT_ID_CHANGE;
            if (0 != value)
            {
                dbEntry.ObjectIdChange = true;
            }
            value = entry.Reason & Win32Api.USN_REASON_REPARSE_POINT_CHANGE;
            if (0 != value)
            {
                dbEntry.ReparsePointChange = true;
            }
            value = entry.Reason & Win32Api.USN_REASON_STREAM_CHANGE;
            if (0 != value)
            {
                dbEntry.StreamChange = true;
            }
            value = entry.Reason & Win32Api.USN_REASON_CLOSE;
            if (0 != value)
            {
                dbEntry.Close = true;
            }
        }
    

    }
}