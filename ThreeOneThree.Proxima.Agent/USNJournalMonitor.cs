using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using NLog;
using Quartz;
using ThreeOneThree.Proxima.Core;
using ThreeOneThree.Proxima.Core.Entities;
using Path = Fluent.IO.Path;

namespace ThreeOneThree.Proxima.Agent
{
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
            if (Singleton.Instance.SourceMountpoints == null || Singleton.Instance.SourceMountpoints.Count == 0)
            {
                return;
            }

            try
            {
                using (Repository repo = new Repository())
                {

                    foreach (var sourceMount in Singleton.Instance.SourceMountpoints)
                    {
                        var construct = new DriveConstruct(sourceMount.MountPoint);
                        Win32Api.USN_JOURNAL_DATA newUsnState;
                        List<Win32Api.UsnEntry> usnEntries;
                        NtfsUsnJournal journal = new NtfsUsnJournal(construct.DriveLetter);
                            
                        var drivePath = Path.Get(construct.DriveLetter);
                            
                        logger.Trace("Polling for changes from " + sourceMount.CurrentUSNLocation);

                        var rtn = journal.GetUsnJournalEntries(construct.CurrentJournalData, reasonMask, out usnEntries, out newUsnState, OverrideLastUsn: sourceMount.CurrentUSNLocation);
                            
                        if (rtn == NtfsUsnJournal.UsnJournalReturnCode.USN_JOURNAL_SUCCESS)
                        {
                            List<RawUSNEntry> entries = new List<RawUSNEntry>();
                            if (usnEntries.Any())
                            {
                                logger.Debug("USN returned with " + usnEntries.Count + " entries");
                            }

                            List<USNChangeRange> changeRange = new List<USNChangeRange>();

                            foreach (var frn in usnEntries.Select(e=>e.FileReferenceNumber).Distinct())
                            {
                                

                                var entriesForFile = usnEntries.Where(f => f.FileReferenceNumber == frn).ToList();

                                if (entriesForFile.All(e => e.SourceInfo == Win32Api.UsnEntry.USNJournalSourceInfo.DataManagement || e.SourceInfo == Win32Api.UsnEntry.USNJournalSourceInfo.ReplicationManagement))
                                {
                                    continue;
                                }

                                var actualPath = GetActualPath(journal, entriesForFile.FirstOrDefault());

                                if (actualPath == "Unavailable" ||  String.IsNullOrWhiteSpace(actualPath) )
                                {
                                    continue;
                                }

                                if (sourceMount.IgnoreList != null && sourceMount.IgnoreList.Any() && sourceMount.IgnoreList.Any(ignore => new Regex(ignore).IsMatch(actualPath)))
                                {
                                    continue;
                                }


                                USNChangeRange range = new USNChangeRange
                                {
                                    FRN = frn,
                                    Min = entriesForFile.Min(e => e.TimeStamp),
                                    Max = entriesForFile.Max(e => e.TimeStamp),
                                    Closed = entriesForFile.OrderBy(f => f.TimeStamp).LastOrDefault() != null ? (entriesForFile.OrderBy(f => f.TimeStamp).LastOrDefault().Reason & Win32Api.USN_REASON_CLOSE) != 0 : false,
                                    RenameFrom = entriesForFile.FirstOrDefault(e => (e.Reason & Win32Api.USN_REASON_RENAME_OLD_NAME) != 0),
                                    Entry = entriesForFile.OrderBy(f => f.TimeStamp).LastOrDefault()
                                };

                                changeRange.Add(range);
                            }

                            //logger.Trace("ChangeRange : " + changeRange.Count);


                            foreach (var item in changeRange)
                            {
                                
                                var actualPath = GetActualPath(journal, item.Entry as Win32Api.UsnEntry );

                                if (actualPath == "Unavailable")
                                {
                                    continue;
                                }

                                string relativePath;
                                try
                                {
  
                                    Uri drivePathUri = new Uri(drivePath.FullPath, UriKind.Absolute);
                                    Uri actualPathUri = new Uri(actualPath, UriKind.Absolute);

                                    relativePath = drivePathUri.MakeRelativeUri(actualPathUri).ToString();
                                }
                                catch (Exception e)
                                {
                                    relativePath = "#ERROR#";
                                }

                                var count = repo.Count<USNJournalSyncLog>(f =>
                                
                                    f.Action.RelativePath == relativePath && f.DestinationMachine == Singleton.Instance.CurrentServer &&

                                    (
                                        ((f.ActionStartDate.HasValue && f.ActionStartDate <= item.Min.Truncate(TimeSpan.TicksPerMillisecond))
                                        &&
                                        (f.ActionFinishDate.HasValue && f.ActionFinishDate >= item.Max.Truncate(TimeSpan.TicksPerMillisecond)))

                                        ||

                                        ((f.ActionStartDate.HasValue && f.ActionStartDate >= item.Min.Truncate(TimeSpan.TicksPerMillisecond))
                                        &&
                                        (f.ActionFinishDate.HasValue && f.ActionFinishDate >= item.Max.Truncate(TimeSpan.TicksPerMillisecond)))

                                        ||

                                        ((f.ActionStartDate.HasValue && f.ActionStartDate <= item.Min.Truncate(TimeSpan.TicksPerMillisecond))
                                        &&
                                        (f.ActionFinishDate.HasValue && f.ActionFinishDate <= item.Max.Truncate(TimeSpan.TicksPerMillisecond)))

                                        ||

                                        ((f.ActionStartDate.HasValue && f.ActionStartDate >= item.Min.Truncate(TimeSpan.TicksPerMillisecond))
                                        &&
                                        (f.ActionFinishDate.HasValue && f.ActionFinishDate <= item.Max.Truncate(TimeSpan.TicksPerMillisecond)))
                                    )
                                );
                                
                                if (count > 0)
                                {
                                    //logger.Info("Count is " + count);
                                    continue;
                                }


                                var dbEntry = new RawUSNEntry();

                                PopulateFlags(dbEntry, item.Entry);

                                dbEntry.Path = actualPath;
                                dbEntry.RelativePath = relativePath;
                                dbEntry.File = item.Entry.IsFile;
                                dbEntry.Directory = item.Entry.IsFolder;
                                dbEntry.FRN = item.Entry.FileReferenceNumber;
                                dbEntry.PFRN = item.Entry.ParentFileReferenceNumber;
                                dbEntry.RecordLength = item.Entry.RecordLength;
                                dbEntry.USN = item.Entry.USN;
                                dbEntry.Mountpoint = sourceMount;
                                
                                dbEntry.TimeStamp = item.Entry.TimeStamp.Truncate(TimeSpan.TicksPerMillisecond);
                                dbEntry.SourceInfo = item.Entry.SourceInfo.ToString();
                                dbEntry.ChangeRange = item;
                                
                                if ( actualPath != null && actualPath != "Unavailable" && actualPath.ToLowerInvariant().StartsWith($"{journal.MountPoint.TrimEnd('\\')}\\$".ToLowerInvariant()))
                                {
                                    dbEntry.SystemFile = true;
                                }


                                if (item.RenameFrom != null)
                                {
                                    dbEntry.RenameFromPath = GetActualPath(journal, ((Win32Api.UsnEntry) item.RenameFrom));
                                    if (!string.IsNullOrWhiteSpace(dbEntry.RenameFromPath ) && dbEntry.RenameFromPath != "Unavailable")
                                    {
                                        dbEntry.RenameFromRelativePath = new Regex(Regex.Escape(drivePath.FullPath), RegexOptions.IgnoreCase).Replace(dbEntry.RenameFromPath, "", 1);
                                    }
                                }

                                entries.Add(dbEntry);
                            }


                            if (changeRange.Any())
                            {
                                repo.Add<USNChangeRange>(changeRange);
                                repo.Add<RawUSNEntry>(entries);

                                var performRollup = RollupService.PerformRollup(entries, sourceMount, Singleton.Instance.Repository);
                                logger.Info(string.Format("Adding [{2}CHANGE/{1}USN/{0}File]", performRollup.Count, entries.Count, changeRange.Count));
                                foreach (var fileAction in performRollup)
                                {
                                  //  logger.Trace("ADD: " + fileAction.RelativePath + ", USN:" + fileAction.USN);
                                }
                                repo.Add<FileAction>(performRollup);

                                //performRollup.ForEach(f=> logger.Debug("Added " + f.Id));
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
                    }
                }
            }
                 
            catch (Exception e)
            {
                logger.Error(e, "Error in USNJournalMonitor");
            }
        }

        private static string GetActualPath(NtfsUsnJournal journal, Win32Api.UsnEntry item)
        {
            string actualPath = null;

            string rawPath;
            var usnRtnCode = journal.GetPathFromFileReference(item.ParentFileReferenceNumber, out rawPath);

            if (usnRtnCode == NtfsUsnJournal.UsnJournalReturnCode.USN_JOURNAL_SUCCESS && 0 != String.Compare(rawPath, "Unavailable", StringComparison.OrdinalIgnoreCase))
            {
                actualPath = $"{journal.MountPoint.TrimEnd('\\')}{rawPath.TrimEnd('\\')}\\{item.Name}";
            }
            else
            {
                return actualPath;
            }
            if (actualPath.ToLowerInvariant().StartsWith($"{journal.MountPoint.TrimEnd('\\')}\\System Volume Information".ToLowerInvariant()))
            {
                return actualPath;
            }
            return actualPath;
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