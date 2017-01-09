using System;
using System.Collections.Generic;
using System.Configuration;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Alphaleonis.Win32.Filesystem;
using Fluent.IO;
using MongoDB.Bson;
using NLog;
using Quartz;
using ThreeOneThree.Proxima.Core;
using ThreeOneThree.Proxima.Core.Entities;
using DirectoryInfo = Alphaleonis.Win32.Filesystem.DirectoryInfo;
using FileInfo = Alphaleonis.Win32.Filesystem.FileInfo;
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
                
                foreach (var syncFrom in Singleton.Instance.DestinationMountpoints)
                {
                    syncFrom.DestinationServer.Fetch(Singleton.Instance.Servers);

                    syncFrom.Mountpoint.Reference = Singleton.Instance.Repository.ById<MonitoredMountpoint>(syncFrom.Mountpoint.ReferenceId);

                    if (syncFrom.Mountpoint.Reference.PublicPath == null)
                    {
                        logger.Warn($"PublicPath for DestinationMountPoint:{syncFrom.Id} is null! Aborting copy.");
                        continue;
                    }
                        
                    List<FileAction> rawEntries;

                    if (String.IsNullOrWhiteSpace(syncFrom.RelativePathStartFilter))
                    {
                        rawEntries = Singleton.Instance.Repository.Many<FileAction>(f =>
                                                            f.Mountpoint == syncFrom.Mountpoint
                                                            && f.USN > syncFrom.LastUSN,

                            limit: Singleton.Instance.CurrentServer.NormalCopyLimit, AscendingSort: e => e.USN).ToList();
                    }
                    else
                    {
                        rawEntries = Singleton.Instance.Repository.Many<FileAction>(f =>
                                                            f.RelativePath.StartsWith(syncFrom.RelativePathStartFilter)
                                                            && f.Mountpoint == syncFrom.Mountpoint
                                                            && f.USN > syncFrom.LastUSN,

                            limit: Singleton.Instance.CurrentServer.NormalCopyLimit, AscendingSort:e=>e.USN).ToList();
                    }


                    var changedFiles = RollupService.PerformRollup(rawEntries).ToList();

                    if (rawEntries.Count == 0)
                    {
                        logger.Trace("No changes found");
                        continue;
                    }
                    logger.Trace($"{changedFiles.Count} changed files for {syncFrom.Id} since USN {syncFrom.LastUSN}");
                    long lastUsn = rawEntries.Max(f=>f.USN);

                    Parallel.ForEach(changedFiles, new ParallelOptions{MaxDegreeOfParallelism = Singleton.Instance.CurrentServer.MaxThreads}, fileAction =>
                    {
                        USNJournalSyncLog log = new USNJournalSyncLog();
                        log.Enqueued = DateTime.Now;
                        log.DestinationMachine = Singleton.Instance.CurrentServer;
                        log.SourceMachine = syncFrom.Mountpoint.Reference.Server;
                        log.Entry = fileAction.USNEntry;
                        log.Action = fileAction;

                        Singleton.Instance.Repository.Add(log);

                        TransferItem(log, syncFrom);
                    });


                    IQueryable<USNJournalSyncLog> failedSync;


                    if (String.IsNullOrWhiteSpace(syncFrom.RelativePathStartFilter))
                    {
                        failedSync = Singleton.Instance.Repository.Many<USNJournalSyncLog>(f => !f.Successfull && f.Action.Mountpoint == syncFrom.Mountpoint && !f.RequiresManualIntervention, limit: Singleton.Instance.CurrentServer.FailedCopyLimit);
                    }
                    else
                    {
                        failedSync = Singleton.Instance.Repository.Many<USNJournalSyncLog>(f => f.Action.RelativePath.StartsWith(syncFrom.RelativePathStartFilter) && !f.Successfull && f.Action.Mountpoint == syncFrom.Mountpoint && !f.RequiresManualIntervention, limit: Singleton.Instance.CurrentServer.FailedCopyLimit);
                    }

                    Parallel.ForEach(failedSync, new ParallelOptions { MaxDegreeOfParallelism = Singleton.Instance.CurrentServer.MaxThreads }, failedItem =>
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
                                return;
                            }
                            failedItem.Retries.Add(DateTime.Now);
                        }



                        TransferItem(failedItem, syncFrom);
                    });

                    syncFrom.LastUSN = lastUsn;
                    Singleton.Instance.Repository.Update(syncFrom);

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
                    var path = Path.Get(syncFrom.Path, syncLog.Action.RelativePath);

                    logger.Info($"[{syncLog.Id}] [D] " + path);
                    
                    if (path.Exists)
                    {
                        var destinationPath = Path.Get(Singleton.Instance.CurrentServer.LocalTempPath, "toDelete", syncLog.Id).FullPath;

                        if (syncLog.Action.IsDirectory)
                        {
                            DirectoryInfo info = new DirectoryInfo(path.FullPath);
                            info.MoveTo(destinationPath);
                            info.Delete(true, true);
                        }
                        else
                        {
                            FileInfo info = new FileInfo(path.FullPath);
                            info.MoveTo(destinationPath);
                            info.Delete();
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

                    }

                    string pathFrom = Path.Get(syncFrom.Path, renameAction.RenameFrom).FullPath;
                    string tempPath = Path.Get(Singleton.Instance.CurrentServer.LocalTempPath, "toRename", syncLog.Id).FullPath;
                    string pathTo = Path.Get(syncFrom.Path, renameAction.RelativePath).FullPath;

                    if (syncLog.Action.IsDirectory)
                    {
                        new DirectoryInfo(pathFrom).MoveTo(tempPath);
                        new DirectoryInfo(tempPath).MoveTo(pathTo);
                    }
                    else
                    {
                        new FileInfo(pathFrom).MoveTo(tempPath);
                        new FileInfo(tempPath).MoveTo(pathTo);
                    }
                    
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

            var destination = Path.Get(syncFrom.Path, relativePath);
            string tempPath = Path.Get(Singleton.Instance.CurrentServer.LocalTempPath, "toCopy", syncLog.Id).FullPath;
            var source = Path.Get(publicPath, relativePath);

            if (source.IsDirectory)
            {
                DirectoryInfo sourceInfo = new DirectoryInfo(source.FullPath);
                DirectoryInfo tempInfo = new DirectoryInfo(tempPath);
                DirectoryInfo destInfo = new DirectoryInfo(destination.FullPath);
                sourceInfo.CopyTo(tempInfo.FullName);
                tempInfo.MoveTo(destInfo.FullName);
            }
            else
            {
                source.Copy(tempPath);
                FileInfo tempInfo = new FileInfo(tempPath);
                tempInfo.MoveTo(destination.FullPath, MoveOptions.None);
            }

            return true;
        }
    }
}