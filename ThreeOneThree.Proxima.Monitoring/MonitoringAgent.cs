using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NLog;
using Tether.Plugins;
using ThreeOneThree.Proxima.Core;
using ThreeOneThree.Proxima.Core.Entities;

namespace ThreeOneThree.Proxima.Monitoring
{
    public class MonitoringAgent : ICheck, IRequireConfigurationData
    {
        private static Logger logger = LogManager.GetCurrentClassLogger();
        private dynamic configurationData;

        public object DoCheck()
        {
            string connectionString = configurationData.ConnectionString.ToString();
            Repository repository = new Repository(connectionString);

            IDictionary<string, object> values = new Dictionary<string, object>();

            Server currentServer = repository.One<Server>(f => f.MachineName == Environment.MachineName.ToLowerInvariant());

            var syncMount = repository.Many<SyncMountpoint>(f => f.DestinationServer == currentServer.Id).ToList();
            logger.Trace("Found {0} mountpoints", syncMount.Count);
            foreach (var syncMountpoint in syncMount)
            {

                logger.Trace("Acting on SyncMountpoint " + syncMountpoint.Id);

                var LastItem = repository.Many<RawUSNEntry>(e => e.Mountpoint == syncMountpoint.Mountpoint && e.USN > syncMountpoint.LastUSN, AscendingSort:entry => entry.USN ).FirstOrDefault();
                var Number = repository.Many<RawUSNEntry>(e => e.Mountpoint == syncMountpoint.Mountpoint && e.USN > syncMountpoint.LastUSN).Count();

                double delay=0;
                if (LastItem != null)
                {
                    delay = ((DateTime.Now - LastItem.TimeStamp).TotalMinutes);
                }
                

                values.Add(string.Format("{0}-DelayInMins", syncMountpoint.Id),  delay );
                values.Add(string.Format("{0}-Items", syncMountpoint.Id), Number);
            }

            return values;
        }

        public string Key
        {
            get { return "Proxima"; }
        }
        public void LoadConfigurationData(dynamic data)
        {
            configurationData = data;
            logger.Trace("Loaded configuration data " + data.ConnectionString.ToString());
        }
    }
}
