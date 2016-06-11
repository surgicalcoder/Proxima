using System;
using PowerArgs;
using ThreeOneThree.Proxima.Core;
using ThreeOneThree.Proxima.Core.Entities;

namespace ThreeOneThree.Proxima.Agent.ConsoleCommands
{
    [ArgExceptionBehavior(ArgExceptionPolicy.StandardExceptionHandling)]
    public class Source
    {
        [ArgActionMethod]
        public void List()
        {
            using (Repository repo = new Repository())
            {
                var items = repo.All<MonitoredMountpoint>();

                foreach (var item in items)
                {
                    item.Server.Fetch(Singleton.Instance.Servers);

                    Console.WriteLine($"{item.Id} - {item.Server.Reference.MachineName} - {item.MountPoint}");
                }
            }
        }
        [ArgActionMethod]
        public void Create(string MountPoint, long LastUSN)
        {
            MonitoredMountpoint mountPoint = new MonitoredMountpoint()
            {
                MountPoint = MountPoint, CurrentUSNLocation = LastUSN, Server = Singleton.Instance.CurrentServer
            };

            using (Repository repo = new Repository())
            {
                repo.Add(mountPoint);
            }

            Console.WriteLine("Success");
        }

        [ArgActionMethod]
        public void Delete(string Id)
        {
            using (Repository repo = new Repository())
            {
                repo.Delete(repo.ById<MonitoredMountpoint>(Id));
            }

            Console.WriteLine("Success");
        }
    }


    [ArgExceptionBehavior(ArgExceptionPolicy.StandardExceptionHandling)]
    public class Dest
    {

        [ArgActionMethod]
        public void Create(string Path, string MountId)
        {
            SyncMountpoint mount = new SyncMountpoint()
            {
                DestinationServer = Singleton.Instance.CurrentServer, Path = Path, Mountpoint = MountId
            };
            using (Repository repo = new Repository())
            {
                repo.Add(mount);
            }

            Console.WriteLine("Success");
        }

        [ArgActionMethod]
        public void List()
        {
            using (Repository repo = new Repository())
            {
                var items = repo.All<SyncMountpoint>();

                foreach (var item in items)
                {
                    item.DestinationServer.Fetch(Singleton.Instance.Servers);

                    Console.WriteLine($"{item.Id} - {item.DestinationServer} - {item.Path}");
                }
            }
        }
    }

    [ArgExceptionBehavior(ArgExceptionPolicy.StandardExceptionHandling)]
    public class USNQuery
    {

        [ArgActionMethod]
        public void Position(string Path)
        {
            NtfsUsnJournal journal = new NtfsUsnJournal(Path);
            Win32Api.USN_JOURNAL_DATA journalData = new Win32Api.USN_JOURNAL_DATA();
            var usnJournalReturnCode = journal.GetUsnJournalState(ref journalData);

            if (usnJournalReturnCode == NtfsUsnJournal.UsnJournalReturnCode.USN_JOURNAL_SUCCESS)
            {
                Console.WriteLine("Next USN: " + journalData.NextUsn);
            }
        }
    }

}