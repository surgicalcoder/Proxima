using System;
using System.Collections.Generic;
using System.Configuration;
using System.IO;
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

                    syncFrom.Mountpoint.Reference = repo.ById<MonitoredMountpoint>(syncFrom.Mountpoint.ReferenceId);

                    var rawEntries = repo.Many<USNJournalMongoEntry>(e => !e.CausedBySync && e.USN > syncFrom.LastUSN && e.Mountpoint.ReferenceId == syncFrom.Mountpoint.ReferenceId && e.Path != "#UNKNOWN#").ToList();

                    var changedFiles = RollupService.PerformRollup(rawEntries, syncFrom).ToList();

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

                        repo.Add(log);

                        Singleton.Instance.ThreadPool.QueueWorkItem(() => TransferItem(log));

                    }
                    syncFrom.LastUSN = lastUsn;
                    repo.Update(syncFrom);
                }
            }
        }




        private void TransferItem(USNJournalSyncLog syncLog)
        {
            // return;
            try
            {
                if (syncLog.Action.DeleteFile)
                {
                    logger.Info($"[{syncLog.Id}] Deleting {syncLog.Action.Path}");

                    if (ConfigurationManager.AppSettings["Safety"] != "SAFE")
                    {
                        syncLog.ActionStartDate = DateTime.Now;
                        Singleton.Instance.Repository.Update(syncLog);
                        File.Delete(syncLog.Action.Path);
                        syncLog.ActionFinishDate = DateTime.Now;
                        syncLog.Successfull = true;
                        Singleton.Instance.Repository.Update(syncLog);
                    }
                
                }
                //else if (syncLog.Action.CreateFile)
                //{
                //    logger.Info($"[{syncLog.Id}] Create {syncLog.Action.Path}");
                //    if (ConfigurationManager.AppSettings["Safety"] != "SAFE")
                //    {
                //        syncLog.ActionStartDate = DateTime.Now;
                //        Singleton.Instance.Repository.Update(syncLog);
                //        File.Create(syncLog.Action.Path);
                //        syncLog.ActionFinishDate = DateTime.Now;
                //        syncLog.Successfull = true;
                //        Singleton.Instance.Repository.Update(syncLog);

                //    }
                //}
                else if (!string.IsNullOrWhiteSpace(syncLog.Action.RenameFrom))
                {
                    logger.Info($"[{syncLog.Id}] Moving {syncLog.Action.RenameFrom} to {syncLog.Action.Path}");

                    if (ConfigurationManager.AppSettings["Safety"] != "SAFE")
                    {
                        syncLog.ActionStartDate = DateTime.Now;
                        Singleton.Instance.Repository.Update(syncLog);
                        File.Move(syncLog.Action.RenameFrom, syncLog.Action.Path);
                        syncLog.ActionFinishDate = DateTime.Now;
                        syncLog.Successfull = true;
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
                        File.Copy(syncLog.Entry.Reference.UniversalPath, syncLog.Action.Path, true);
                        syncLog.ActionFinishDate = DateTime.Now;
                        syncLog.Successfull = true;
                        Singleton.Instance.Repository.Update(syncLog);
                    }
                }
            }
            catch (Exception e)
            {
                logger.Error(e, "Error on item " + syncLog.Id);
            }
        }
    }
}