using System;
using System.Collections.Generic;
using System.Configuration;
using System.IO;
//using System.IO;
using System.Linq;
using Fluent.IO;
using MongoDB.Bson;
using NLog;
using Quartz;
using ThreeOneThree.Proxima.Core;
using ThreeOneThree.Proxima.Core.Entities;
using Path = Fluent.IO.Path;

namespace ThreeOneThree.Proxima.Agent
{
    [DisallowConcurrentExecution]
    public class USNJournalSync : IJob
    {
        private static Logger logger = LogManager.GetCurrentClassLogger();

        public void Execute(IJobExecutionContext context)
        {
            logger.Debug("USNJournalSync Execution");
            if (Singleton.Instance.DestinationMountpoints == null || Singleton.Instance.DestinationMountpoints.Count == 0)
            {
                logger.Info("No destination points");
                return;
            }


            using (Repository repo = new Repository())
            {
               foreach (var syncFrom in Singleton.Instance.DestinationMountpoints)
                {
                    syncFrom.DestinationServer.Fetch(Singleton.Instance.Servers);

                    syncFrom.Mountpoint.Reference = repo.ById<MonitoredMountpoint>(syncFrom.Mountpoint.ReferenceId);
                    
                    var rawEntries = repo.Many<FileAction>(f => f.Mountpoint == syncFrom.Mountpoint && f.Id > syncFrom.LastSyncID).ToList();

                    //var rawEntries = repo.Many<RawUSNEntry>(e => !e.CausedBySync && !e.SystemFile.HasValue && e.USN > syncFrom.LastUSN && e.Mountpoint == syncFrom.Mountpoint && e.Path != "#UNKNOWN#").ToList();
                    var changedFiles = RollupService.PerformRollup(rawEntries).ToList();

                    if (rawEntries.Count == 0)
                    {
                        continue;
                    }

                    string lastUsn = rawEntries.LastOrDefault().Id;

                    foreach (var fileAction in changedFiles)
                    {
                        USNJournalSyncLog log = new USNJournalSyncLog();
                        log.Enqueued = DateTime.Now;
                        log.DestinationMachine = Singleton.Instance.CurrentServer;
                        log.SourceMachine = syncFrom.Mountpoint.Reference.Server;
                        log.Entry = fileAction.USNEntry;
                        log.Action = fileAction;

                        repo.Add(log);

                        Singleton.Instance.ThreadPool.QueueWorkItem(() => TransferItem(log, syncFrom));

                    }
                    syncFrom.LastSyncID = lastUsn;
                    repo.Update(syncFrom);
                }
            }
        }




        private void TransferItem(USNJournalSyncLog syncLog, SyncMountpoint syncFrom)
        {
            try
            {
                syncLog.ActionStartDate = DateTime.Now;
                Singleton.Instance.Repository.Update(syncLog);

                bool successfull = false;

                if (syncLog.Action.GetType() == typeof (DeleteAction))
                {
                    logger.Info($"[{syncLog.Id}] Deleting {syncLog.Action.RelativePath}");

                    if (syncLog.Action.IsDirectory)
                    {
                        Path.Get(syncFrom.Path, syncLog.Action.RelativePath).Delete(true);
                    }
                    else
                    {
                        Path.Get(syncFrom.Path, syncLog.Action.RelativePath).Delete(true);
                    }

                    successfull = true;
                    
                }

                else if (syncLog.Action.GetType() == typeof(RenameAction))
                {
                    var renameAction = syncLog.Action as RenameAction;

                    logger.Info($"[{syncLog.Id}] Moving {renameAction.RenameFrom} to {renameAction.RenameTo}");

                    Path.Get(syncFrom.Path, renameAction.RenameFrom).Move(Path.Get(syncFrom.Path, renameAction.RenameTo).FullPath);
                    //if (syncLog.Action.IsDirectory)
                    //{

                    //}
                    //else
                    //{
                    //    if (File.Exists(syncLog.Action.RenameFrom))
                    //    {
                    //        File.Move(syncLog.Action.RenameFrom, syncLog.Action.RelativePath);
                    //    }
                    //    else
                    //    {
                    //        File.Copy(syncLog.Entry.Reference.UniversalPath, syncLog.Action.RelativePath, true);
                    //    }
                    //}
                    successfull = true;

                    
                }
                else
                {
                    logger.Info($"[{syncLog.Id}] Copying {syncLog.Action.RelativePath}");

                    var copyAction = syncLog.Action as UpdateAction;

                    Path.Get(syncFrom.Mountpoint.Reference.PublicPath, syncLog.Entry.Reference.RelativePath).Copy(Path.Get(syncFrom.Path, syncLog.Entry.Reference.RelativePath), Overwrite.Always, true);

                    //if (syncLog.Action.IsDirectory)
                    //{
                    //    Path.Get(syncFrom.Mountpoint.Reference.PublicPath, syncLog.Entry.Reference.RelativePath).Copy(Path.Get(syncFrom.Path, syncLog.Entry.Reference.RelativePath), Overwrite.Always, true);
                    //    //Path.Get(syncLog.Entry.Reference. .UniversalPath).Copy(syncLog.Action.RelativePath, Overwrite.Always);
                    //}
                    //else
                    //{

                    //    File.Copy(syncLog.Entry.Reference.UniversalPath, syncLog.Action.RelativePath, true);
                    //}
                    successfull = true;
                    
                }


                syncLog.ActionFinishDate = DateTime.Now;
                syncLog.Successfull = successfull;
                Singleton.Instance.Repository.Update(syncLog);

            }
            catch (Exception e)
            {
                logger.Error(e, "Error on item " + syncLog.Id);
            }
        }
    }
}