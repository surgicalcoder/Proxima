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
            //logger.Debug("USNJournalSync Execution");
            if (Singleton.Instance.DestinationMountpoints == null || Singleton.Instance.DestinationMountpoints.Count == 0)
            {
                logger.Info("No destination points");
                return;
            }


            try
            {
                using (Repository repo = new Repository())
                {
                    foreach (var syncFrom in Singleton.Instance.DestinationMountpoints)
                    {
                        syncFrom.DestinationServer.Fetch(Singleton.Instance.Servers);

                        syncFrom.Mountpoint.Reference = repo.ById<MonitoredMountpoint>(syncFrom.Mountpoint.ReferenceId);

                        if (syncFrom.Mountpoint.Reference.PublicPath == null)
                        {
                            logger.Warn(string.Format("PublicPath for DestinationMountPoint:{0} is null! Aborting copy.", syncFrom.Id));
                            continue;
                        }

                        logger.Debug("Polling for changes since " + syncFrom.LastUSN);

                        var rawEntries = repo.Many<FileAction>(f => f.Mountpoint == syncFrom.Mountpoint && f.USN > syncFrom.LastUSN, limit: 256).ToList();

                        

                        var changedFiles = RollupService.PerformRollup(rawEntries).ToList();

                        if (rawEntries.Count == 0)
                        {
                            logger.Debug("No changes found!");
                            continue;
                        }
                        logger.Info(string.Format("{0} changed files for {1}", changedFiles.Count, syncFrom.Id));
                        long lastUsn = rawEntries.LastOrDefault().USN;

                        foreach (var fileAction in changedFiles)
                        {
                            USNJournalSyncLog log = new USNJournalSyncLog();
                            log.Enqueued = DateTime.Now;
                            log.DestinationMachine = Singleton.Instance.CurrentServer;
                            log.SourceMachine = syncFrom.Mountpoint.Reference.Server;
                            log.Entry = fileAction.USNEntry;
                            log.Action = fileAction;

                            repo.Add(log);
                            
                            TransferItem(log, syncFrom);
                        }


                        var failedSync = repo.Many<USNJournalSyncLog>(f => !f.Successfull && f.Action.Mountpoint == syncFrom.Mountpoint && !f.RequiresManualIntervention, limit: 32);

                        foreach (var failedItem in failedSync)
                        {
                            if (failedItem.Retries == null)
                            {
                                failedItem.Retries = new List<DateTime> { failedItem.ActionStartDate.Value};
                            }
                            else
                            {
                                failedItem.Retries.Add(DateTime.Now);
                            }

                            TransferItem(failedItem, syncFrom);

                        }

                        syncFrom.LastUSN = lastUsn;
                        repo.Update(syncFrom);

                    }
                }
            }
            catch (Exception e)
            {
                logger.Error(e, "Error during JournalSync");
            }
        }




        private void TransferItem(USNJournalSyncLog syncLog, SyncMountpoint syncFrom)
        {
            try
            {
                syncLog.ActionStartDate = DateTime.Now;
                syncLog.ActionFinishDate = null;
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
                    
                    var publicPath = syncFrom.Mountpoint.Reference.PublicPath;
                    var relativePath = copyAction.RelativePath;

                    if (publicPath == null)
                    {
                        throw new NullReferenceException("publicPath");
                    }
                    if (relativePath == null)
                    {
                        throw new NullReferenceException("relativePath");
                    }

                    Path.Get(publicPath, relativePath).Copy(Path.Get(syncFrom.Path, relativePath), Overwrite.Always, true);

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

                if (e.GetType() == typeof (FileNotFoundException))
                {
                    syncLog.RequiresManualIntervention = true;
                }

                syncLog.ActionFinishDate = DateTime.Now;
                Singleton.Instance.Repository.Update(syncLog);

                logger.Error(e, "Error on item " + syncLog.Id);
                Error error = new Error();
                error.SyncLog = syncLog;
                error.Exception = e;
                error.ItemId = syncLog.Id;
                error.Message = e.Message;
                Singleton.Instance.Repository.Add(error);
            }
        }
    }
}