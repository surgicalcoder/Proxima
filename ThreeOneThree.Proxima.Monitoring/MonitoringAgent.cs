using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Tether.Plugins;
using ThreeOneThree.Proxima.Core;
using ThreeOneThree.Proxima.Core.Entities;

namespace ThreeOneThree.Proxima.Monitoring
{
    public class MonitoringAgent : ICheck, IRequireConfigurationData
    {
        private dynamic configurationData;

        public object DoCheck()
        {
            Repository repository = new Repository();

            repository.ConnectionString = configurationData.ConnectionString;

            IDictionary<string, object> values = new Dictionary<string, object>();

            Server currentServer = repository.One<Server>(f => f.MachineName == Environment.MachineName.ToLowerInvariant());

            var syncMount = repository.Many<SyncMountpoint>(f => f.DestinationServer == currentServer.Id).ToList();

            foreach (var syncMountpoint in syncMount)
            {
                var LastItem = repository.Many<RawUSNEntry>(e => e.Mountpoint == syncMountpoint.Id && e.USN > syncMountpoint.LastUSN).FirstOrDefault();
                var Number = repository.Many<RawUSNEntry>(e => e.Mountpoint == syncMountpoint.Id && e.USN > syncMountpoint.LastUSN).Count();
                
                values.Add(string.Format("{0}-DelayInMins", syncMountpoint.Id),  ((DateTime.Now - LastItem.TimeStamp).TotalMinutes) );
                values.Add(string.Format("{0}-Items", syncMountpoint.Id), Number);
            }

            return values;
        }

        public string Key { get; }
        public void LoadConfigurationData(dynamic data)
        {
            configurationData = data;
        }
    }
}
