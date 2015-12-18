using System;
using System.Collections.Generic;
using System.Configuration;
//using System.IO;
using System.Linq;
using Fluent.IO;
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
            if (Singleton.Instance.DestinationMountpoints == null || Singleton.Instance.DestinationMountpoints.Count == 0)
            {
                return;
            }


            using (Repository repo = new Repository())
            {
               foreach (var syncFrom in Singleton.Instance.DestinationMountpoints)
                {
                    syncFrom.DestinationServer.Fetch(Singleton.Instance.Servers);

                    var rawEntries = repo.Many<USNJournalMongoEntry>(e => !e.CausedBySync && e.USN >= syncFrom.LastUSN && e.Mountpoint.ReferenceId == syncFrom.Mountpoint.ReferenceId ).ToList();

                    var changedFiles = PerformRollup(rawEntries, syncFrom).ToList();

                    if (rawEntries.Count == 0)
                    {
                        continue;
                    }

                    long lastUsn = rawEntries.LastOrDefault().USN;

                    foreach (var fileAction in changedFiles)
                    {
                        USNJournalSyncLog log = new USNJournalSyncLog();
                        log.Enqueued = DateTime.Now;
                        log.DestinationMachine = Singleton.Instance.CurrentServer;
                        log.SourceMachine = syncFrom.Mountpoint.Reference.Server;
                        log.Entry = rawEntries.FirstOrDefault(f=>f.USN == fileAction.USN);
                        log.Action = fileAction;

                        repo.Upsert(log);

                        Singleton.Instance.ThreadPool.QueueWorkItem(() => TransferItem(log));

                    }
                    syncFrom.LastUSN = lastUsn;
                    repo.Upsert(syncFrom);
                }
            }
        }

        private List<FileAction> PerformRollup(List<USNJournalMongoEntry> toList, SyncMountpoint syncFrom)
        {
            toList.Where(f=>f.Close.HasValue && f.Close.Value).ToList().Sort((entry, mongoEntry) => entry.USN.CompareTo(mongoEntry.USN));

            var toReturn = new List<FileAction>();

            foreach (var entry in toList)
            {
                if (entry.RenameNewName.HasValue)
                {
                    toReturn.Add(new FileAction()
                    {
                        RenameFrom = toList.FirstOrDefault(f=>f.RenameOldName.HasValue && f.FRN == entry.FRN && f.PFRN == entry.PFRN).Path,
                        Path = GetRelativePath(entry.Path, syncFrom),
                        USN = entry.USN,
                    });
                }
                else if (entry.FileDelete.HasValue)
                {
                    toReturn.RemoveAll(f => f.Path == entry.Path);
                    toReturn.Add(new FileAction() { Path = entry.Path, USN = entry.USN, DeleteFile = true});
                }
                else
                {
                    toReturn.RemoveAll(f => f.Path == entry.Path && !f.DeleteFile && string.IsNullOrWhiteSpace(f.RenameFrom));
                    toReturn.Add(new FileAction() {Path = entry.Path, USN = entry.USN});
                }
            }

            return toReturn;
        }

        private string GetRelativePath(string path, SyncMountpoint syncFrom)
        {

            var relativePath = Path.Get(path).MakeRelativeTo(syncFrom.Mountpoint.Reference.MountPoint.TrimEnd('\\'));

            var finalPath = Path.Get(syncFrom.Path).Add(relativePath);
            return finalPath.FullPath;
        }

        private void TransferItem(USNJournalSyncLog syncLog)
        {
           // return;
            if (syncLog.Action.DeleteFile)
            {
                logger.Info($"[{syncLog.Id}] Deleting {syncLog.Action.Path}");

                if (ConfigurationManager.AppSettings["Safety"] != "SAFE")
                {
                    syncLog.ActionStartDate = DateTime.Now;
                    Singleton.Instance.Repository.Update(syncLog);
                    System.IO.File.Delete(syncLog.Action.Path);
                    syncLog.ActionFinishDate = DateTime.Now;
                    Singleton.Instance.Repository.Update(syncLog);
                }
                
            }
            else if (string.IsNullOrWhiteSpace(syncLog.Action.RenameFrom))
            {
                logger.Info($"[{syncLog.Id}] Moving {syncLog.Action.RenameFrom} to {syncLog.Action.Path}");

                if (ConfigurationManager.AppSettings["Safety"] != "SAFE")
                {
                    syncLog.ActionStartDate = DateTime.Now;
                    Singleton.Instance.Repository.Update(syncLog);
                    System.IO.File.Move(syncLog.Action.RenameFrom, syncLog.Action.Path);
                    syncLog.ActionFinishDate = DateTime.Now;
                    Singleton.Instance.Repository.Update(syncLog);
                    
                }
            }
            else
            {
                logger.Info($"[{syncLog.Id}] Copying {syncLog.Action.Path}");

                if (ConfigurationManager.AppSettings["Safety"] != "SAFE")
                {
                    syncLog.ActionStartDate = DateTime.Now;
                    Singleton.Instance.Repository.Update(syncLog);
                    System.IO.File.Copy(syncLog.Entry.Reference.UniversalPath, syncLog.Action.Path);
                    syncLog.ActionFinishDate = DateTime.Now;
                    Singleton.Instance.Repository.Update(syncLog);
                }
            }
        }
    }
}