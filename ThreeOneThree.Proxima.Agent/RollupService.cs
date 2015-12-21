using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Fluent.IO;
using ThreeOneThree.Proxima.Core.Entities;

namespace ThreeOneThree.Proxima.Agent
{
    public static class RollupService
    {
        public static List<FileAction> PerformRollup(List<USNJournalMongoEntry> rawEntries, SyncMountpoint syncFrom)
        {
            var entries = rawEntries.Where(f => f.Close.HasValue && f.Close.Value && (!f.RenameOldName.HasValue || !f.RenameOldName.Value)).OrderBy(f => f.Path)/*.Distinct(new JournalPathEqualityComparer())*/;
            
            var toReturn = new List<FileAction>();

            foreach (var entry in entries)
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
                    item.RenameFrom = rawEntries.FirstOrDefault(f => f.RenameOldName.HasValue && f.FRN == entry.FRN && f.PFRN == entry.PFRN).Path;
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
