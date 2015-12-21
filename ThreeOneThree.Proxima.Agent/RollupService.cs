using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Fluent.IO;
using NLog;
using ThreeOneThree.Proxima.Core.Entities;

namespace ThreeOneThree.Proxima.Agent
{
    public static class RollupService
    {
        private static Logger logger = LogManager.GetCurrentClassLogger();
        public static List<FileAction> PerformRollup(List<USNJournalMongoEntry> rawEntries, SyncMountpoint syncFrom)
        {
            var entries = rawEntries.Where(f => f.Close.HasValue && f.Close.Value && (!f.RenameOldName.HasValue || !f.RenameOldName.Value)).OrderBy(f => f.Path).ThenBy(f=>f.FileCreate)/*.Distinct(new JournalPathEqualityComparer())*/;
            
            var toReturn = new List<FileAction>();

            foreach (var entry in entries)
            {
                try
                {
                    var relativePath = GetRelativePath(entry.Path, syncFrom);

                    if (entry.FileCreate.HasValue)
                    {
                        toReturn.Add(new FileAction()
                        {
                            SourcePath = entry.Path,
                            CreateFile = true,
                            Path = relativePath,
                            USN = entry.USN,
                        });
                    }
                    else if (entry.RenameNewName.HasValue)
                    {
                        var item = new FileAction();
                        if (rawEntries.FirstOrDefault(f => f.RenameOldName.HasValue && f.FRN == entry.FRN && f.PFRN == entry.PFRN) == null)
                        {
                            item.RenameFrom = Singleton.Instance.Repository.One<USNJournalMongoEntry>(f => f.FRN == entry.FRN && f.PFRN == entry.PFRN && f.RenameOldName.HasValue).Path;
                        }
                        else
                        {
                            item.RenameFrom = rawEntries.FirstOrDefault(f => f.RenameOldName.HasValue && f.FRN == entry.FRN && f.PFRN == entry.PFRN).Path;
                        }
                        item.Path = relativePath;
                        item.USN = entry.USN;
                        item.SourcePath = entry.Path;

                        toReturn.Add(item);
                    }
                    else if (entry.FileDelete.HasValue)
                    {
                        toReturn.Add(new FileAction()
                        {
                            Path = relativePath,
                            USN = entry.USN,
                            DeleteFile = true,
                            SourcePath = entry.Path
                        });
                    }
                    else
                    {
                        toReturn.Add(new FileAction() { Path= relativePath, USN = entry.USN, SourcePath = entry.Path});
                    }
                }
                catch (Exception e)
                {
                    logger.Error("Error processing item " + entry.Id, e);
                    continue;
                }
            }


            var deletedFiles = toReturn.Where(f => f.DeleteFile).ToList();

            foreach (var deletedFile in deletedFiles)
            {
                if (toReturn.Where(f => f.CreateFile).Select(f => f.Path).Contains(deletedFile.Path))
                {
                    toReturn.Remove(deletedFile);
                }

                toReturn.RemoveAll(f => f.Path == deletedFile.Path && f.USN < deletedFile.USN);
            }

            var fileActions = toReturn.Select(e => e.Path).Distinct().Select(f => toReturn.FirstOrDefault(a => a.Path == f)).ToList();


            return fileActions;
        }




        private static string GetRelativePath(string path, SyncMountpoint syncFrom)
        {

            var relativePath = Path.Get(path).MakeRelativeTo(syncFrom.Mountpoint.Reference.MountPoint.TrimEnd('\\'));
            var finalPath = Path.Get(syncFrom.Path, relativePath.ToString());
            return finalPath.FullPath;
        }



    }
}
