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
                MountPoint = MountPoint,
                CurrentUSNLocation = LastUSN,
                Server = Singleton.Instance.CurrentServer,
                PublicPath = "#PublicPath#",
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
}