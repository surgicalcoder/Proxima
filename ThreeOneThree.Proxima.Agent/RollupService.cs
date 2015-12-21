using System;
using System.Collections.Generic;
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
            var entries = rawEntries.Where(f => f.Close.HasValue && f.Close.Value && (!f.RenameOldName.HasValue || !f.RenameOldName.Value) ).ToList();
            entries.Sort((entry, mongoEntry) => entry.USN.CompareTo(mongoEntry.USN));

            var toReturn = new List<FileAction>();

            foreach (var entry in entries)
            {
                if (entry.FileCreate.HasValue)
                {
                    toReturn.Add(new FileAction()
                    {
                        CreateFile = true,
                        Path = GetRelativePath(entry.Path, syncFrom),
                        USN = entry.USN,
                    });
                }
                else if (entry.RenameNewName.HasValue)
                {
                    var item = new FileAction();
                    item.RenameFrom = rawEntries.FirstOrDefault(f => f.RenameOldName.HasValue && f.FRN == entry.FRN && f.PFRN == entry.PFRN).Path;
                    item.Path = GetRelativePath(entry.Path, syncFrom);
                    item.USN = entry.USN;

                    toReturn.Add(item);
                }
                else if (entry.FileDelete.HasValue)
                {
                    toReturn.RemoveAll(f => f.Path == entry.Path);
                    toReturn.Add(new FileAction()
                    {
                        Path = GetRelativePath(entry.Path, syncFrom),
                        USN = entry.USN,
                        DeleteFile = true
                    });
                }
                else
                {
                    toReturn.RemoveAll(f => f.Path == entry.Path && !f.DeleteFile && string.IsNullOrWhiteSpace(f.RenameFrom));
                    toReturn.Add(new FileAction() { Path = entry.Path, USN = entry.USN });
                }
            }



            var toDelete= toReturn.Where(e => 
                e.CreateFile && 
                toReturn.Where(f => f.DeleteFile).Select(f => f.Path).Contains(e.Path)
            ).ToList();

            toDelete.AddRange( toReturn.Where(e=>
                e.DeleteFile && toReturn.Where(f=>f.CreateFile).Select(f=>f.Path).Contains(e.Path)
            ) );

            toReturn.RemoveAll(f => toDelete.Contains(f));

            return toReturn;
        }




        private static string GetRelativePath(string path, SyncMountpoint syncFrom)
        {

            var relativePath = Path.Get(path).MakeRelativeTo(syncFrom.Mountpoint.Reference.MountPoint.TrimEnd('\\'));
            var finalPath = Path.Get(syncFrom.Path, relativePath.ToString());
            return finalPath.FullPath;
        }



    }
}
