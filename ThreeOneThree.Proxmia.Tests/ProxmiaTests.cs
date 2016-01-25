using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NUnit.Framework;
using ThreeOneThree.Proxima.Agent;
using ThreeOneThree.Proxima.Core.Entities;


namespace ThreeOneThree.Proxmia.Tests
{
    [TestFixture]
    public class ProxmiaTests
    {
        
        SyncMountpoint mountPoint = new SyncMountpoint() {
            Mountpoint = new MonitoredMountpoint()
        {
            MountPoint = sourcePath
        },
            Path = destinationPath
        };

        private static string sourcePath = "C:\\TestPath\\Testpath2\\";

        private static string destinationPath = "C:\\TestPath\\TestPath3\\";

        [Test]
        public void PathMappingWorks()
        {
            List<RawUSNEntry> entries = new List<RawUSNEntry>
            {
                new RawUSNEntry {Close = true, Path = sourcePath + "file1.txt", USN = 12345, FRN = 1001, PFRN = 1002},

            };


            var result = RollupService.PerformRollup(entries, mountPoint.Mountpoint.Reference);
            
            Assert.AreEqual( destinationPath + "file1.txt", result[0].RelativePath);
        }

        [Test]
        public void OnlyCopyOneFileForMultipleChangesToSameFile()
        {
            List<RawUSNEntry> entries = new List<RawUSNEntry>
            {
                new RawUSNEntry {Close = true, Path = sourcePath + "file1.txt", USN = 12345, FRN = 1001, PFRN = 1002},
                new RawUSNEntry {Close = true, Path = sourcePath + "file1.txt", USN = 12346, FRN = 1001, PFRN = 1002},
                new RawUSNEntry {Close = true, Path = sourcePath + "file1.txt", USN = 12347, FRN = 1001, PFRN = 1002},
                new RawUSNEntry {Close = true, Path = sourcePath + "file1.txt", USN = 12348, FRN = 1001, PFRN = 1002},

            };
            

            var result = RollupService.PerformRollup(entries, mountPoint.Mountpoint.Reference);
            
            Assert.AreEqual(result.Count, 1);
        }


        [Test]
        public void RenameOldFileGeneratesNoEntries()
        {
            List<RawUSNEntry> entries = new List<RawUSNEntry>
            {
                new RawUSNEntry {Close = true, Path = sourcePath + "file1.txt", USN = 12345, FRN = 1001, PFRN = 1002, RenameOldName = true},
            };


            var result = RollupService.PerformRollup(entries, mountPoint.Mountpoint.Reference);

            Assert.AreEqual(result.Count, 0);
        }

        [Test]
        public void RenameFileTest()
        {
            List<RawUSNEntry> entries = new List<RawUSNEntry>
            {
                new RawUSNEntry {Close = true, Path = sourcePath + "file1.txt", USN = 12345, FRN = 1001, PFRN = 1002, RenameOldName = true},
                new RawUSNEntry {Close = true, Path = sourcePath + "file2.txt", USN = 12301, FRN = 1001, PFRN = 1002, RenameNewName= true},
            };


            var result = RollupService.PerformRollup(entries, mountPoint.Mountpoint.Reference);

            Assert.AreEqual(result.Count, 1);
        }

        [Test]
        public void CreateThenDeleteFileGeneratesNoEntries()
        {
            List<RawUSNEntry> entries = new List<RawUSNEntry>
            {
                new RawUSNEntry {Close = true, Path = sourcePath + "file1.txt", USN = 10000, FRN = 1001, PFRN = 1002, FileCreate = true},
                new RawUSNEntry {Close = true, Path = sourcePath + "file1.txt", USN = 11000, FRN = 1001, PFRN = 1002, FileDelete = true},
            };


            var result = RollupService.PerformRollup(entries, mountPoint.Mountpoint.Reference);

            Assert.AreEqual(0, result.Count);
        }

        [Test]
        public void CreateThenDeleteThenModifyFile()
        {
            List<RawUSNEntry> entries = new List<RawUSNEntry>
            {
                new RawUSNEntry {Close = true, Path = sourcePath + "file1.txt", USN = 10000, FRN = 1001, PFRN = 1002, FileCreate = true},
                new RawUSNEntry {Close = true, Path = sourcePath + "file1.txt", USN = 11000, FRN = 1001, PFRN = 1002},
                new RawUSNEntry {Close = true, Path = sourcePath + "file1.txt", USN = 12000, FRN = 1001, PFRN = 1002, FileDelete = true},
                new RawUSNEntry {Close = true, Path = sourcePath + "file1.txt", USN = 13000, FRN = 1001, PFRN = 1002},
            };


            var result = RollupService.PerformRollup(entries, mountPoint.Mountpoint.Reference);

            Assert.AreEqual(1, result.Count);
            Assert.AreEqual(0, result.OfType<UpdateAction>().Count());
            Assert.AreEqual(0, result.OfType<DeleteAction>().Count());
        }

        [Test]
        public void FavourCopyVSCreate()
        {
            List<RawUSNEntry> entries = new List<RawUSNEntry>
            {
                new RawUSNEntry {Close = true, Path = sourcePath + "file1.txt", USN = 10000, FRN = 1001, PFRN = 1002, FileCreate = true},
                new RawUSNEntry {Close = true, Path = sourcePath + "file1.txt", USN = 13000, FRN = 1001, PFRN = 1002},
            };


            var result = RollupService.PerformRollup(entries, mountPoint.Mountpoint.Reference);

            Assert.AreEqual(1, result.Count);
            Assert.AreEqual(0, result.OfType<UpdateAction>().Count());
        }

        [Test]
        public void CreateThenRenameFile()
        {
            List<RawUSNEntry> entries = new List<RawUSNEntry>
            {
                new RawUSNEntry {Close = true, Path = sourcePath + "file1.txt", USN = 10000, FRN = 1001, PFRN = 1002, FileCreate = true},
                new RawUSNEntry {Close = true, Path = sourcePath + "file1.txt", USN = 10001, FRN = 1001, PFRN = 1002},
                new RawUSNEntry {Close = true, Path = sourcePath + "file1.txt", USN = 10011, FRN = 1001, PFRN = 1002, RenameOldName = true},
                new RawUSNEntry {Close = true, Path = sourcePath + "file2.txt", USN = 10013, FRN = 1001, PFRN = 1002, RenameNewName = true},

            };


            var result = RollupService.PerformRollup(entries, mountPoint.Mountpoint.Reference);

            Assert.AreEqual(1, result.Count);

        }


    }
}
