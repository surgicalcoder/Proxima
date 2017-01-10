using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Fluent.IO;
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
        private static List<string> TopshelfParameters = new List<string> { "install" ,"start","stop","uninstall"};
        private static Logger logger = LogManager.GetCurrentClassLogger();

        static void Main(string[] args)
        {
            using(Repository repo = new Repository())
            {
                Singleton.Instance.Servers = repo.All<Server>().ToList();

                logger.Trace($"{Singleton.Instance.Servers.Count} number of total servers");

                var currentServer = Singleton.Instance.Servers.FirstOrDefault(f => f.MachineName == Environment.MachineName.ToLowerInvariant());

                if (currentServer == null)
                {
                    currentServer = new Server {MachineName = Environment.MachineName.ToLowerInvariant(), FailedCopyLimit = 32, MaxThreads = 1, MonitorCheckInSecs = 2, NormalCopyLimit = 256, SyncCheckInSecs = 2, Version = Assembly.GetExecutingAssembly().GetName().Version.ToString() };
                    logger.Trace("Creating new server");
                    repo.Add(currentServer);
                    Singleton.Instance.Servers.Add(currentServer);
                }
                else
                {
                    currentServer.Version = Assembly.GetExecutingAssembly().GetName().Version.ToString();
                    repo.Update(currentServer);
                }

                logger.Trace($"Current server = {currentServer.MachineName} ({currentServer.Id})");

                Singleton.Instance.CurrentServer = currentServer;
                Singleton.Instance.DestinationMountpoints = repo.Many<SyncMountpoint>(f => f.DestinationServer == currentServer.Id).ToList();
                Singleton.Instance.SourceMountpoints = repo.Many<MonitoredMountpoint>(f => f.Server == currentServer.Id).ToList();

                if (Singleton.Instance.DestinationMountpoints.Any())
                {
                    foreach (var instanceDestinationMountpoint in Singleton.Instance.DestinationMountpoints)
                    {
                        Path.Get(instanceDestinationMountpoint.Path, ".proximaTemp.", "toCopy").CreateDirectory();
                        Path.Get(instanceDestinationMountpoint.Path, ".proximaTemp.", "toRename").CreateDirectory();
                        Path.Get(instanceDestinationMountpoint.Path, ".proximaTemp.", "toDelete").CreateDirectory();
                    }
                }

            }

            Singleton.Instance.SourceMountpoints.ForEach(f=>logger.Trace("Source: " + f.ToString()));
            Singleton.Instance.DestinationMountpoints.ForEach(f=>logger.Trace("Destination: " + f.Path + " // " + f.Mountpoint));


            if (args.Length == 0 || TopshelfParameters.Contains(args[0].ToLowerInvariant()))
            {
                Host host = HostFactory.New(x =>
                {
                    x.Service<Service>(service =>
                    {
                        service.ConstructUsing(f => new Service());

                        service.WhenStarted((a, control) => a.Start(control));
                        service.WhenStopped((a, control) => a.Stop(control));

                        service.ScheduleQuartzJob(b => b.WithJob(() => JobBuilder.Create<USNJournalSync>().Build())
                          .AddTrigger(() => TriggerBuilder.Create().WithSimpleSchedule(builder => builder.WithMisfireHandlingInstructionFireNow().WithIntervalInSeconds(Singleton.Instance.CurrentServer.SyncCheckInSecs).RepeatForever()).Build()));

                        service.ScheduleQuartzJob(b => b.WithJob(() => JobBuilder.Create<USNJournalMonitor>().Build())
                            .AddTrigger(() => TriggerBuilder.Create().WithSimpleSchedule(builder => builder.WithMisfireHandlingInstructionFireNow().WithIntervalInSeconds(Singleton.Instance.CurrentServer.MonitorCheckInSecs).RepeatForever()).Build()));

                    });
                    x.RunAsLocalSystem();
                    x.StartAutomaticallyDelayed();
                    x.SetDescription("The Proxima Agent that monitors the USN Journal");
                    x.SetDisplayName("ProximaAgent");
                    x.SetServiceName("ThreeOneThree.Proxima.Agent");



                });

                host.Run();
                return;
            }

            ConsoleString usageHints = new ConsoleString();

            try
            {
                
                if (args[0].ToLowerInvariant() == "source")
                {
                    Args.InvokeAction<ConsoleCommands.Source>(args.Skip(1).ToArray());
                    
                    usageHints = ArgUsage.GenerateUsageFromTemplate<ConsoleCommands.Source>();
                }
                else if (args[0].ToLowerInvariant() == "dest")
                {
                    Args.InvokeAction<ConsoleCommands.Dest>(args.Skip(1).ToArray());
                    usageHints = ArgUsage.GenerateUsageFromTemplate<ConsoleCommands.Dest>();
                }
                else if (args[0].ToLowerInvariant() == "query")
                {
                    Args.InvokeAction<ConsoleCommands.USNQuery>(args.Skip(1).ToArray());
                    usageHints = ArgUsage.GenerateUsageFromTemplate<ConsoleCommands.USNQuery>();
                }
                else
                {
                    Console.WriteLine("Missing type of config - source, dest or query");
                    return;
                }
            }
            catch (ArgException ex)
            {
                Console.WriteLine(ex.Message);
                Console.WriteLine(usageHints);
            }

            //Console.WriteLine();
            //Console.WriteLine("Press any key to exit");
            //Console.ReadKey();
        }
    }
}
