using System;
using System.Linq;
using PowerArgs;
using ThreeOneThree.Proxima.Core;
using ThreeOneThree.Proxima.Core.Entities;

namespace ThreeOneThree.Proxima.Agent
{
    //[ArgExceptionBehavior(ArgExceptionPolicy.StandardExceptionHandling)]
    //public class AdminCommands
    //{
    //    [ArgActionMethod, ArgDescription("Sets up a machine to copy from")]
    //    public void SetMachineCopyFrom(string MachineName, long USNLocation, string RemoteFolder, string LocalFolder)
    //    {
    //        USNJournalSyncFrom j = new USNJournalSyncFrom()
    //        {
    //            CurrentUSNLocation = USNLocation,
    //            DestinationMachine = Environment.MachineName.ToLowerInvariant(),
    //            SourceMachine = MachineName,
    //            RemotePathMap = RemoteFolder,
    //            LocalPathMap = LocalFolder
    //        };

    //        using (Repository repo = new Repository())
    //        {
    //            repo.Add(j);
    //        }

    //        Console.WriteLine("Success");
    //    }

    //    [ArgActionMethod, ArgDescription("Lists all machine copy from's set up")]
    //    public void ListMachineCopyFrom()
    //    {
    //        using (Repository repo = new Repository())
    //        {
    //            var output = repo.All<USNJournalSyncFrom>().ToList();

    //            foreach (var usnJournalSyncFrom in output)
    //            {
    //                Console.WriteLine($"{usnJournalSyncFrom.Id} - {usnJournalSyncFrom.SourceMachine} ({usnJournalSyncFrom.CurrentUSNLocation})");
    //            }
    //        }
    //    }

    //    [ArgActionMethod, ArgDescription("Deletes a machine copy from")]
    //    public void DeleteMachineCopyFrom(string Id)
    //    {
    //        using (Repository repo = new Repository())
    //        {
    //            repo.Delete(repo.ById<USNJournalSyncFrom>(Id));
    //        }
    //        Console.WriteLine("Success");
    //    }

    //    [ArgActionMethod, ArgDescription("Lists mounts that will be synced")]
    //    public void ListMountToSync()
    //    {
    //        using (var repo = new Repository())
    //        {
    //            repo.Many<MountpointToMonitor>(e=>e.SourceMachine == Environment.MachineName.ToLowerInvariant()).ToList().ForEach(f=>Console.WriteLine($"{f.Id} - ${f.MountPath} "));

    //        }
    //    }

    //    [ArgActionMethod, ArgDescription("Adds a local sync point")]
    //    public void AddMountToSync(string MountPoint)
    //    {
    //        using (var repo = new Repository())
    //        {
    //            MountpointToMonitor monitor = new MountpointToMonitor() {MountPath =  MountPoint, SourceMachine = Environment.MachineName.ToLowerInvariant()};
    //            repo.Add(monitor);
    //        }

    //        Console.WriteLine("Success");
    //    }

    //    [ArgActionMethod, ArgDescription("Removes a local sync point")]
    //    public void RemoveMountToSync(string Id)
    //    {
    //        using (Repository repo = new Repository())
    //        {
    //            repo.Delete(repo.ById<MountpointToMonitor>(Id));
    //        }
    //        Console.WriteLine("Success");
    //    }
    //}
}