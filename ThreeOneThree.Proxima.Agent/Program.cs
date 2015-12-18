using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Threading.Tasks;
using NLog;
using PowerArgs;
using Quartz;
using ThreeOneThree.Proxima.Core;
using ThreeOneThree.Proxima.Core.Entities;
using Topshelf;
using Topshelf.Quartz;

namespace ThreeOneThree.Proxima.Agent
{
    class Program
    {
        static private List<string> TopshelfParameters = new List<string> { "install" ,"start","stop","uninstall"};
        private static Logger logger = LogManager.GetCurrentClassLogger();

        static void Main(string[] args)
        {
            using(Repository repo = new Repository())
            {
                Singleton.Instance.Servers = repo.All<Server>().ToList();

                var currentServer = Singleton.Instance.Servers.FirstOrDefault(f => f.MachineName == Environment.MachineName.ToLowerInvariant());
                if (currentServer == null)
                {
                    currentServer = new Server(Environment.MachineName);
                    repo.Add(currentServer);
                    Singleton.Instance.Servers.Add(currentServer);
                }

                Singleton.Instance.CurrentServer = currentServer;
                Singleton.Instance.DestinationMountpoints = repo.Many<SyncMountpoint>(f => f.DestinationServer.ReferenceId == currentServer.Id).ToList();
                Singleton.Instance.SourceMountpoints = repo.Many<MonitoredMountpoint>(f => f.Server.ReferenceId == currentServer.Id).ToList();
            }

            if (args.Length == 0 || TopshelfParameters.Contains(args[0].ToLowerInvariant()))
            {
                Host host = HostFactory.New(x =>
                {
                    x.Service<Service>(service =>
                    {
                        service.ConstructUsing(f => new Service());

                        service.WhenStarted((a, control) => a.Start(control));
                        service.WhenStopped((a, control) => a.Stop(control));

                        service.ScheduleQuartzJob(b => b.WithJob(() => JobBuilder.Create<USNJournalSync>().Build()).AddTrigger(() => TriggerBuilder.Create().WithSimpleSchedule(builder => builder.WithMisfireHandlingInstructionFireNow().WithIntervalInSeconds(5).RepeatForever()).Build()));
                        service.ScheduleQuartzJob(b => b.WithJob(() => JobBuilder.Create<USNJournalMonitor>().Build()).AddTrigger(() => TriggerBuilder.Create().WithSimpleSchedule(builder => builder.WithMisfireHandlingInstructionFireNow().WithIntervalInSeconds(5).RepeatForever()).Build()));

                    });
                    x.RunAsLocalSystem();
                    x.StartAutomaticallyDelayed();
                    x.SetDescription("The Proxmia Agent that monitors the USN Journal");
                    x.SetDisplayName("ProximaAgent");
                    x.SetServiceName("ThreeOneThree.Proxmia.Agent");



                });

                host.Run();
                return;
            }

            try
            {
               // Args.InvokeAction<AdminCommands>(args);
            }
            catch (ArgException ex)
            {
                Console.WriteLine(ex.Message);
            }


        }
    }
}
