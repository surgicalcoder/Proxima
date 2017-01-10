using System;
using PowerArgs;
using ThreeOneThree.Proxima.Core;
using ThreeOneThree.Proxima.Core.Entities;

namespace ThreeOneThree.Proxima.Agent.ConsoleCommands
{
    [ArgExceptionBehavior(ArgExceptionPolicy.StandardExceptionHandling)]
    public class Dest
    {

        [ArgActionMethod]
        public void Create(string Path, string MountId)
        {
            SyncMountpoint mount = new SyncMountpoint()
            {
                DestinationServer = Singleton.Instance.CurrentServer,
                Path = Path,
                Mountpoint = MountId,
                LastUSN = 0
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
}