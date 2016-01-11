using System;
using System.IO;
using System.Linq;
using System.Management;
using NLog;

namespace ThreeOneThree.Proxima.Agent
{
    public class DriveConstruct
    {
        private static Logger logger = LogManager.GetCurrentClassLogger();


        public DriveConstruct(string driveLetter)
        {
            DriveLetter = driveLetter;
            DriveInfo = new DriveInfo(driveLetter);
            CurrentJournalData = UsnJournalHelper.GetCurrentUSNJournalData(driveLetter);

            try
            {
                ManagementObjectSearcher searcher = new ManagementObjectSearcher("root\\CIMV2", "SELECT * FROM Win32_Volume");


                Volume = searcher.Get().Cast<ManagementObject>().FirstOrDefault(f=> f["Caption"].ToString() == driveLetter )["DeviceID"].ToString();

                //foreach (var queryObj in searcher.Get())
                //{
                //    Volume = queryObj["DeviceID"].ToString();
                //}
            }
            catch (Exception e)
            {
                logger.Error(e, "Error when getting WMI");
            }

        }

        public string DriveLetter { get; set; }
        public DriveInfo DriveInfo { get; set; }

        public string Volume { get; set; }
        public Win32Api.USN_JOURNAL_DATA CurrentJournalData { get; set; }

        public long LastUSN { get; set; }
        public bool HavePerformedRewind { get; set; }
    }
}