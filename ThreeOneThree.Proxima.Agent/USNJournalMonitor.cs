using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using NLog;
using Quartz;
using ThreeOneThree.Proxima.Core;
using ThreeOneThree.Proxima.Core.Entities;
using Path = Fluent.IO.Path;

namespace ThreeOneThree.Proxima.Agent
{
   // [PersistJobDataAfterExecution]
    [DisallowConcurrentExecution]
    public class USNJournalMonitor : IJob
    {

        private static Logger logger = LogManager.GetCurrentClassLogger();

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
            //logger.Debug("USNJournalMonitor Execution");
            if (Singleton.Instance.SourceMountpoints == null || Singleton.Instance.SourceMountpoints.Count == 0)
            {
                logger.Debug("No source mount points found");
                return;
            }

            

            using (Repository repo = new Repository())
            {
                try
                {
                    

                    foreach (var sourceMount in Singleton.Instance.SourceMountpoints)
                    {

                        List<RawUSNEntry> entries = new List<RawUSNEntry>();


                        var construct = new DriveConstruct(sourceMount.MountPoint);
                        Win32Api.USN_JOURNAL_DATA newUsnState;
                        List<Win32Api.UsnEntry> usnEntries;
                        NtfsUsnJournal journal = new NtfsUsnJournal(construct.DriveLetter);

                        var drivePath = Fluent.IO.Path.Get(construct.DriveLetter);

                        var rtn = journal.GetUsnJournalEntries(construct.CurrentJournalData, reasonMask, out usnEntries, out newUsnState, OverrideLastUsn: sourceMount.CurrentUSNLocation);

                        if (rtn == NtfsUsnJournal.UsnJournalReturnCode.USN_JOURNAL_SUCCESS)
                        {

                            foreach (var entry in usnEntries)
                            {
                                string rawPath;
                                string actualPath;

                                var usnRtnCode = journal.GetPathFromFileReference(entry.ParentFileReferenceNumber, out rawPath);

                                if (usnRtnCode == NtfsUsnJournal.UsnJournalReturnCode.USN_JOURNAL_SUCCESS && 0 != String.Compare(rawPath, "Unavailable", StringComparison.OrdinalIgnoreCase))
                                {
                                    actualPath = $"{journal.MountPoint.TrimEnd('\\')}{rawPath.TrimEnd('\\')}\\{entry.Name}";
                                }
                                else
                                {
                                    continue;
                                }

                                if (actualPath.ToLowerInvariant().StartsWith($"{journal.MountPoint.TrimEnd('\\')}\\System Volume Information".ToLowerInvariant()) ) // || actualPath.ToLowerInvariant().StartsWith($"{journal.MountPoint.TrimEnd('\\')}\\$".ToLowerInvariant()))
                                {
                                    continue;
                                }

                                var dbEntry = new RawUSNEntry();
                                PopulateFlags(dbEntry, entry);

                                dbEntry.Path = actualPath;
                                dbEntry.File = entry.IsFile;
                                dbEntry.Directory = entry.IsFolder;
                                dbEntry.FRN = entry.FileReferenceNumber;
                                dbEntry.PFRN = entry.ParentFileReferenceNumber;
                                dbEntry.RecordLength = entry.RecordLength;
                                dbEntry.USN = entry.USN;
                                dbEntry.Mountpoint = sourceMount; 
                                dbEntry.TimeStamp = entry.TimeStamp;

                                dbEntry.RelativePath = Path.Get(actualPath).MakeRelativeTo(drivePath.FullPath.TrimEnd('\\')).ToString();
                                
                                dbEntry.CausedBySync = repo.Count<USNJournalSyncLog>(f => 
                                                                                     f.Action.RelativePath == actualPath &&
                                                                                     (f.ActionStartDate.HasValue && entry.TimeStamp >= f.ActionStartDate) && 
                                                                                     (f.ActionFinishDate.HasValue  && entry.TimeStamp <= f.ActionFinishDate ) ) 
                                                       > 0;
                                if (actualPath.ToLowerInvariant().StartsWith($"{journal.MountPoint.TrimEnd('\\')}\\$".ToLowerInvariant()))
                                {
                                    dbEntry.SystemFile = true;
                                }


                                entries.Add(dbEntry);
                            }

                            construct.CurrentJournalData = newUsnState;
                            sourceMount.CurrentUSNLocation = newUsnState.NextUsn;
                            sourceMount.Volume = construct.Volume;
                            
                            repo.Update(sourceMount);

                        }
                        else
                        {
                            logger.Error("Error on Monitor - " + rtn.ToString());
                            throw new UsnJournalException(rtn);
                        }

                        if (entries.Any())
                        {
                            repo.Add<RawUSNEntry>(entries);
                            RollupService.PerformRollup(entries, sourceMount);
                        }

                    }
                    
                }
                catch (Exception e)
                {
                    logger.Error(e, "Error in USNJournalMonitor");
                }
            }
        }

        private string GetRemotePath(string actualPath)
        {
            return "\\\\" + Environment.MachineName + "\\" + actualPath.Replace(":", "$");
        }

        private void PopulateFlags(RawUSNEntry dbEntry, Win32Api.UsnEntry entry)
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