using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Fluent.IO;
using NLog;
using ThreeOneThree.Proxima.Core.Entities;

namespace ThreeOneThree.Proxima.Agent
{
    public static class RollupService
    {
        private static Logger logger = LogManager.GetCurrentClassLogger();


        public static List<FileAction> PerformRollup(List<FileAction> toReturn)
        {
            var deletedFiles = toReturn.OfType<DeleteAction>().ToList();

            foreach (var deletedFile in deletedFiles)
            {
                if (toReturn.OfType<UpdateAction>().Select(f => f.RelativePath).Contains(deletedFile.RelativePath))
                {
                    toReturn.Remove(deletedFile);
                }

                toReturn.RemoveAll(f => f.RelativePath == deletedFile.RelativePath && f.USN < deletedFile.USN);
            }

            var fileActions = toReturn.Select(e => e.RelativePath).Distinct().Select(f => toReturn.FirstOrDefault(a => a.RelativePath == f)).ToList();

            foreach (var source in fileActions.OfType<RenameAction>().ToList())
            {
                if (fileActions.Any(f => f.RawPath == source.RenameFrom))
                {
                    fileActions.RemoveAll(f => f.RawPath == source.RenameFrom);
                    fileActions.Remove(source);
                    fileActions.Add(new RenameAction()
                    {
                        RelativePath = source.RelativePath,
                        RawPath = source.RawPath,
                        USN = source.USN,
                        IsDirectory = source.IsDirectory,
                        Mountpoint = source.Mountpoint
                    });
                }
            }


            return fileActions;
        }

        public static List<FileAction> PerformRollup(List<RawUSNEntry> rawEntries, MonitoredMountpoint syncFrom)
        {
            var entries = rawEntries.Where(f => !f.CausedBySync && f.Close.HasValue && f.Close.Value && (!f.RenameOldName.HasValue || !f.RenameOldName.Value))/*.Distinct(new JournalPathEqualityComparer())*/;

            if (syncFrom.IgnoreList.Any())
            {
                entries = syncFrom.IgnoreList.Select(ignore => new Regex(ignore)).Aggregate(entries, (current, regex) => current.Where(f => !regex.IsMatch(f.RelativePath)));
            }

            entries = entries.OrderBy(f => f.Path).ThenBy(f => f.FileCreate);

            var toReturn = new List<FileAction>();

            foreach (var entry in entries)
            {
                try
                {
                    if (entry.RenameNewName.HasValue)
                    {
                        var item = new RenameAction();
                        item.IsDirectory = entry.Directory.HasValue && entry.Directory.Value;

                        if (rawEntries.FirstOrDefault(f => f.RenameOldName.HasValue && f.FRN == entry.FRN && f.PFRN == entry.PFRN) == null)
                        {
                            item.RenameFrom = Singleton.Instance.Repository.One<RawUSNEntry>(f => f.FRN == entry.FRN && f.PFRN == entry.PFRN && f.RenameOldName.HasValue).Path;
                        }
                        else
                        {
                            item.RenameFrom = rawEntries.FirstOrDefault(f => f.RenameOldName.HasValue && f.FRN == entry.FRN && f.PFRN == entry.PFRN).Path;
                        }

                        item.RelativePath = entry.RelativePath;
                        item.USNEntry = entry;
                        item.USN = entry.USN;
                        item.RawPath = entry.Path;
                        item.Mountpoint = syncFrom;

                        toReturn.Add(item);
                    }
                    else if (entry.FileDelete.HasValue)
                    {
                        toReturn.Add(new DeleteAction()
                        {
                            
                            RelativePath = entry.RelativePath,
                            USN = entry.USN,
                            USNEntry = entry,
                            RawPath = entry.Path,
                            IsDirectory = entry.Directory.HasValue && entry.Directory.Value,
                            Mountpoint = syncFrom
                        });
                    }
                    else
                    {
                        toReturn.Add(new UpdateAction()
                        {
                            IsDirectory = entry.Directory.HasValue && entry.Directory.Value,
                            RawPath = entry.Path,
                            RelativePath = entry.RelativePath,
                            USN = entry.USN,
                            USNEntry = entry,
                            Mountpoint = syncFrom
                        });
                    }
                }
                catch (Exception e)
                {
                    logger.Error("Error processing item " + entry.Id, e);
                    continue;
                }
            }

            var deletedFiles = toReturn.OfType<DeleteAction>().ToList();

            foreach (var deletedFile in deletedFiles)
            {
                if (toReturn.OfType<UpdateAction>().Select(f => f.RelativePath).Contains(deletedFile.RelativePath))
                {
                    toReturn.Remove(deletedFile);
                }

                toReturn.RemoveAll(f => f.RelativePath == deletedFile.RelativePath && f.USN < deletedFile.USN);
            }

            var fileActions = toReturn.Select(e => e.RelativePath).Distinct().Select(f => toReturn.FirstOrDefault(a => a.RelativePath == f)).ToList();

            foreach (var source in fileActions.OfType<RenameAction>().ToList())
            {
                if (fileActions.Any(f => f.RawPath == source.RenameFrom))
                {
                    fileActions.RemoveAll(f => f.RawPath == source.RenameFrom);
                    fileActions.Remove(source);
                    fileActions.Add(new RenameAction()
                    {
                        RelativePath = source.RelativePath,
                        RawPath = source.RawPath,
                        USN = source.USN,
                        IsDirectory = source.IsDirectory,
                        Mountpoint = syncFrom
                    });
                }
            }


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
