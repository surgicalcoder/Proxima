using System;
using System.Linq;
using PowerArgs;
using ThreeOneThree.Proxima.Core;

namespace ThreeOneThree.Proxima.Agent
{
    [ArgExceptionBehavior(ArgExceptionPolicy.StandardExceptionHandling)]
    public class AdminCommands
    {
        [ArgActionMethod, ArgDescription("Sets up a machine to copy from")]
        public void SetMachineCopyFrom(string MachineName, long USNLocation)
        {
            USNJournalSyncFrom j = new USNJournalSyncFrom()
            {
                CurrentUSNLocation = USNLocation,
                DestinationMachine = Environment.MachineName.ToLowerInvariant(),
                SourceMachine = MachineName
            };

            using (Repository repo = new Repository())
            {
                repo.Add(j);
            }

            Console.WriteLine("Success");
        }

        [ArgActionMethod, ArgDescription("Lists all machine copy from's set up")]
        public void ListMachineCopyFrom()
        {
            using (Repository repo = new Repository())
            {
                var output = repo.All<USNJournalSyncFrom>().ToList();

                foreach (var usnJournalSyncFrom in output)
                {
                    Console.WriteLine($"{usnJournalSyncFrom.Id} - {usnJournalSyncFrom.SourceMachine} ({usnJournalSyncFrom.CurrentUSNLocation})");
                }
            }
        }

        [ArgActionMethod, ArgDescription("Deletes a machine copy from")]
        public void DeleteMachineCopyFrom(string Id)
        {
            using (Repository repo = new Repository())
            {
                repo.Delete(repo.ById<USNJournalSyncFrom>(Id));
            }
            Console.WriteLine("Success");
        }

    }
}