using System;
using System.Collections.Generic;
using System.Linq;
using NLog;
using Quartz;
using ThreeOneThree.Proxima.Core;
using ThreeOneThree.Proxima.Core.Entities;

namespace ThreeOneThree.Proxima.Agent
{
    [PersistJobDataAfterExecution]
    [DisallowConcurrentExecution]
    public class USNJournalSync : IJob
    {
        private static Logger logger = LogManager.GetCurrentClassLogger();


        public void Execute(IJobExecutionContext context)
        {
            if (USNJournalSingleton.Instance.DrivesToMonitor == null || USNJournalSingleton.Instance.DrivesToMonitor.Count == 0)
            {
                return;
            }


            using (Repository repo = new Repository())
            {
                var syncFroms = repo.Many<USNJournalSyncFrom>(e => e.DestinationMachine == Environment.MachineName.ToLowerInvariant());

                

                foreach (var syncFrom in syncFroms)
                {
                    var rawEntries = repo.Many<USNJournalMongoEntry>(e => !e.CausedBySync.HasValue && e.USN >= syncFrom.CurrentUSNLocation && e.MachineName == syncFrom.SourceMachine.ToLowerInvariant()).ToList();
                    var changedFiles = PerformRollup(rawEntries).ToList();

                    long lastUsn = rawEntries.LastOrDefault().USN;

                    foreach (var fileAction in changedFiles)
                    {
                        USNJournalSyncLog log = new USNJournalSyncLog();
                        log.Enqueued = DateTime.Now;
                        log.DestinationMachine = Environment.MachineName.ToLowerInvariant();
                        log.SourceMachine = syncFrom.SourceMachine;
                        log.Entry = rawEntries.FirstOrDefault(f=>f.USN == fileAction.USN);
                        log.Action = fileAction;

                        repo.Upsert(log);

                        USNJournalSingleton.Instance.ThreadPool.QueueWorkItem(() => TransferItem(log));

                    }


                    syncFrom.CurrentUSNLocation = lastUsn;
                    repo.Upsert(syncFrom);
                }


            }
        }




        private IEnumerable<FileAction> PerformRollup(List<USNJournalMongoEntry> toList)
        {
            toList.Where(f=>f.Close.HasValue && f.Close.Value).ToList().Sort((entry, mongoEntry) => entry.USN.CompareTo(mongoEntry.USN));

            foreach (var entry in toList)
            {
                if (entry.RenameNewName.HasValue)
                {
                    yield return new FileAction()
                    {
                        RenameFrom = toList.FirstOrDefault(f=>f.RenameOldName.HasValue && f.FRN == entry.FRN && f.PFRN == entry.PFRN).Path,
                        Path = entry.Path,
                        USN = entry.USN,
                    };
                }
                else if (entry.FileDelete.HasValue)
                {
                    yield return new FileAction() { Path = entry.Path, USN = entry.USN, DeleteFile = true};
                }
                else
                {
                    yield return new FileAction() {Path = entry.Path, USN = entry.USN};
                }
            }


        }

        private void TransferItem(USNJournalSyncLog usnJournalMongoEntry)
        {
            if (usnJournalMongoEntry.Action.DeleteFile)
            {

                
                // Delete File
            }
            else if (string.IsNullOrWhiteSpace(usnJournalMongoEntry.Action.RenameFrom))
            {
                // Move File
            }
            else
            {
                // Copy File from other node.
            }
        }
    }
}