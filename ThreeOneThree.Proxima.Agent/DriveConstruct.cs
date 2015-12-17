using System;
using System.IO;

namespace ThreeOneThree.Proxima.Agent
{
    public class DriveConstruct
    {
        public DriveConstruct(string driveLetter)
        {
            DriveLetter = driveLetter;
            DriveInfo = new DriveInfo(driveLetter);
            CurrentJournalData = UsnJournalHelper.GetCurrentUSNJournalData(driveLetter);
        }

        public string DriveLetter { get; set; }
        public DriveInfo DriveInfo { get; set; }
        public Win32Api.USN_JOURNAL_DATA CurrentJournalData { get; set; }

        public bool HavePerformedRewind { get; set; }
    }
}