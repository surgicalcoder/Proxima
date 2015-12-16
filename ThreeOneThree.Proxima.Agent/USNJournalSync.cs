using System;
using System.Linq;
using Quartz;
using ThreeOneThree.Proxima.Core;

namespace ThreeOneThree.Proxima.Agent
{
    [PersistJobDataAfterExecution]
    [DisallowConcurrentExecution]
    public class USNJournalSync : IJob
    {
        public void Execute(IJobExecutionContext context)
        {
            Console.WriteLine("Meeples!");
            if (USNJournalSingleton.Instance.DrivesToMonitor == null || USNJournalSingleton.Instance.DrivesToMonitor.Count == 0)
            {
                return;
            }


            using (Repository repo = new Repository())
            {
                var syncFroms = repo.Many<USNJournalSyncFrom>(e => e.DestinationMachine == Environment.MachineName.ToLowerInvariant());

                foreach (var syncFrom in syncFroms)
                {

                    var changedFiles = repo.Many<USNJournalMongoEntry>(e => e.CausedBySync == false && e.USN >= syncFrom.CurrentUSNLocation).ToList();

                    

                    changedFiles.ForEach(delegate(USNJournalMongoEntry f)
                    {
                        USNJournalSyncLog log = new USNJournalSyncLog()
                        {
                            Enqueued = DateTime.Now, DestinationMachine = Environment.MachineName.ToLowerInvariant(), SourceMachine =  syncFrom.SourceMachine, Entry = f
                        };
                        
                        repo.Upsert(log);

                        USNJournalSingleton.Instance.ThreadPool.QueueWorkItem(() => TransferItem(log));
                    });
                }
            }
            
        }

        private void TransferItem(USNJournalSyncLog usnJournalMongoEntry)
        {
            
        }
    }
}