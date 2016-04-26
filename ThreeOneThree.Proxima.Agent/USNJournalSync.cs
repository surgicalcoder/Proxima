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
            logger.Trace("USNJournalSync Execution");
            if (Singleton.Instance.DestinationMountpoints == null || Singleton.Instance.DestinationMountpoints.Count == 0)
            {
                logger.Trace("No destination points");
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
                        
                        List<FileAction> rawEntries;

                        if (String.IsNullOrWhiteSpace(syncFrom.RelativePathStartFilter))
                        {
                            rawEntries = repo.Many<FileAction>(f =>
                                                               f.Mountpoint == syncFrom.Mountpoint
                                                               && f.USN > syncFrom.LastUSN,

                                limit: 256).ToList();
                        }
                        else
                        {
                            rawEntries = repo.Many<FileAction>(f =>
                                                                f.RelativePath.StartsWith(syncFrom.RelativePathStartFilter)
                                                               && f.Mountpoint == syncFrom.Mountpoint
                                                               && f.USN > syncFrom.LastUSN,

                                limit: 256).ToList();
                        }


                        var changedFiles = RollupService.PerformRollup(rawEntries).ToList();

                        if (rawEntries.Count == 0)
                        {
                            logger.Trace("No changes found");
                            continue;
                        }
                        logger.Trace($"{changedFiles.Count} changed files for {syncFrom.Id} since USN {syncFrom.LastUSN}");
                        long lastUsn = rawEntries.Max(f => f.USN);

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


                        IQueryable<USNJournalSyncLog> failedSync;

                        if (String.IsNullOrWhiteSpace(syncFrom.RelativePathStartFilter))
                        {
                            failedSync = repo.Many<USNJournalSyncLog>(f => !f.Successfull && f.Action.Mountpoint == syncFrom.Mountpoint && !f.RequiresManualIntervention, limit: 32);
                        }
                        else
                        {
                            failedSync = repo.Many<USNJournalSyncLog>(f => f.Action.RelativePath.StartsWith(syncFrom.RelativePathStartFilter) && !f.Successfull && f.Action.Mountpoint == syncFrom.Mountpoint && !f.RequiresManualIntervention, limit: 32);
                        }

                        

                        foreach (var failedItem in failedSync)
                        {
                            if (failedItem.Retries == null)
                            {
                                failedItem.Retries = new List<DateTime>
                                {
                                    failedItem.ActionStartDate ?? DateTime.Now
                                };
                            }
                            else
                            {
                                if (failedItem.Retries.Count == 20)
                                {
                                    failedItem.RequiresManualIntervention = true;

                                    Singleton.Instance.Repository.Update(failedItem);
                                    continue;
                                }
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
                    //logger.Info($"[{syncLog.Id}] Deleting {syncLog.Action.RelativePath}");

                    var path = Path.Get(syncFrom.Path, syncLog.Action.RelativePath);

                    logger.Info($"[{syncLog.Id}] [D] " + path);
                    
                    if (path.Exists)
                    {
                        if (syncLog.Action.IsDirectory)
                        {
                            path.Delete(true);
                        }
                        else
                        {
                            path.Delete(true);
                        }
                    }


                    successfull = true;

                }

                else if (syncLog.Action.GetType() == typeof (RenameAction))
                {
                    var renameAction = syncLog.Action as RenameAction;

                    logger.Info($"[{syncLog.Id}] [R] {renameAction.RenameFrom} to {renameAction.RelativePath}");

                    if (String.IsNullOrWhiteSpace(renameAction.RenameFrom))
                    {
                        CopyFile(syncLog, syncFrom);
                        //syncLog.Successfull = false;
                        //syncLog.RequiresManualIntervention = true;
                        //syncLog.ActionFinishDate = DateTime.Now;
                        //Singleton.Instance.Repository.Update(syncLog);

                    }

                    Path.Get(syncFrom.Path, renameAction.RenameFrom).Move(Path.Get(syncFrom.Path, renameAction.RelativePath).FullPath);

                    successfull = true;


                }
                else
                {
                    successfull = CopyFile(syncLog, syncFrom);
                }


                syncLog.ActionFinishDate = DateTime.Now;
                syncLog.Successfull = successfull;
                Singleton.Instance.Repository.Update(syncLog);

                foreach (var error in Singleton.Instance.Repository.Many<Error>(f=>f.SyncLog.Id == syncLog.Id))
                {
                    Singleton.Instance.Repository.Delete(error);
                }

            }
            catch (FileNotFoundException ex)
            {
                syncLog.RequiresManualIntervention = true;
                syncLog.ActionFinishDate = DateTime.Now;
                Singleton.Instance.Repository.Update(syncLog);

                logger.Error(ex, "Error on item " + syncLog.Id);
                Error error = new Error();
                error.SyncLog = syncLog;
                error.Exception = ex;
                error.ItemId = syncLog.Id;
                error.Message = ex.Message;
                Singleton.Instance.Repository.Add(error);
            }
            catch (Exception e)
            {
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

        private static bool CopyFile(USNJournalSyncLog syncLog, SyncMountpoint syncFrom)
        {
            bool successfull;
            logger.Info($"[{syncLog.Id}] [C] {syncLog.Action.RelativePath}");

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

            successfull = true;
            return successfull;
        }
    }
}