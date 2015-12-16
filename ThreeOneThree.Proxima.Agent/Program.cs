using System.Configuration;
using System.Linq;
using System.Threading.Tasks;
using Quartz;
using Topshelf;
using Topshelf.Quartz;

namespace ThreeOneThree.Proxima.Agent
{
    class Program
    {
        static void Main(string[] args)
        {
            USNJournalSingleton.Instance.DrivesToMonitor = ConfigurationManager.AppSettings["DrivesToMonitor"].Split(';').Select(e=>new DriveConstruct(e)).ToList();

            Host host = HostFactory.New(x =>
            {
                x.Service<Service>(service =>
                {
                    service.ConstructUsing(f=> new Service());

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

        }
    }
}
