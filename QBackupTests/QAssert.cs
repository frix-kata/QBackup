using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using QBackup;



namespace QBackupTests
{

    

    public static class QAssert 
    {

        public static void AreEqual(long x1, long x2)
        {
            if (x1 != x2) Assert.Fail();
        }

        public static void AreEqual(DateTime x1, DateTime x2)
        {
            if (!Utils.AreEqual(x1, x2)) Assert.Fail();
        }

        public static void AreEqual(File x1, File x2, bool check_absolute = true, bool check_content = false, bool check_last_write_time = true)
        {
            if (x1 == null)
            {
                Assert.IsNull(x2);
                return;
            }
            Assert.AreEqual(x1.GetRelativePath(), x2.GetRelativePath());
            if (check_absolute) Assert.AreEqual(x1.GetAbsolutePath(), x2.GetAbsolutePath());
            Assert.AreEqual(x1.Name, x2.Name);
            AreEqual(x1.Size, x2.Size);
            if (check_last_write_time) AreEqual(x1.LastWriteTime, x2.LastWriteTime);
            if (check_content)
            {
                var c1 = System.IO.File.ReadAllText(x1.GetAbsolutePath());
                var c2 = System.IO.File.ReadAllText(x2.GetAbsolutePath());
                Assert.AreEqual(c1, c2);
            }
        }

        public static void AreEqual(Dir x1, Dir x2, bool check_absolute = true, bool check_children = true, bool check_last_write_time = true)
        {
            if (check_absolute) Assert.AreEqual(x1.AbsolutePath, x2.AbsolutePath);
            Assert.AreEqual(x1.RelativePath, x2.RelativePath);
            Assert.AreEqual(x1.Name, x2.Name);
            Assert.AreEqual(x1.IsLink, x2.IsLink);

            if (check_children)
            {
                AreEqual(x1.Dirs, x2.Dirs, check_absolute, false);
                AreEqual(x1.Files, x2.Files, check_absolute, false, check_last_write_time);
            }
        }

        public static void AreEqual(Dir x1, ArchivedDir x2, bool check_absolute = true, bool check_children = true, bool check_last_write_time = true)
        {
            if (check_absolute) Assert.AreEqual(x1.AbsolutePath, x2.Dir.AbsolutePath);
            Assert.AreEqual(x1.RelativePath, x2.Dir.RelativePath);
            Assert.AreEqual(x1.Name, x2.Dir.Name);
            Assert.AreEqual(x1.IsLink, x2.Dir.IsLink);
            if (check_children)
            {
                AreEqual(x1.Dirs, x2.Dir.Dirs, check_absolute, false);
                AreEqual(x1.Files, x2.ArchivedFiles, check_absolute, false, check_last_write_time);
            }
        }

        public static void AreEqual(List<Dir> dirs1, List<ArchivedDir> dirs2, bool check_absolute = true, bool check_children = true, bool check_last_write_time = true)
        {
            Assert.AreEqual(dirs1.Count, dirs2.Count);
            var d = dirs2.ToDictionary(x => x.Dir.RelativePath);
            var dirs = dirs1.Select(x => new Tuple<Dir, ArchivedDir>(x, d[x.RelativePath]));
            foreach (var dir in dirs)
            {
                AreEqual(dir.Item1, dir.Item2, check_absolute, check_children, check_last_write_time);
            }
        }

        public static void AreEqual(List<Dir> dirs1, List<Dir> dirs2, bool check_absolute = true, bool check_children = true, bool check_last_write_time = true)
        {
            Assert.AreEqual(dirs1.Count, dirs2.Count);
            var d = dirs2.ToDictionary(x => x.RelativePath);
            var dirs = dirs1.Select(x => new Tuple<Dir, Dir>(x, d[x.RelativePath]));
            foreach (var dir in dirs)
            {
                AreEqual(dir.Item1, dir.Item2, check_absolute, check_children, check_last_write_time);
            }
        }

        public static void AreEqual(AnalyzedBackup src, ArchivedBackup archived)
        {
            AreEqual(src.Dirs, archived.ArchivedDirs, false, false, true);
            AreEqual(src.Files, archived.ArchivedDirs.SelectMany(x => x.ArchivedFiles).ToList(), false, false, true);
        }

        public static void AreEqual(AnalyzedBackup src, AnalyzedBackup extracted)
        {
            AreEqual(src.Dirs, extracted.Dirs, false, true, false);
            AreEqual(src.Files, extracted.Files, false, true, false);
        }

        public static void AreEqual(List<File> files1, List<File> files2, bool check_absolute = true, bool check_content = false, bool check_last_write_time = true)
        {
            Assert.AreEqual(files1.Count, files2.Count);
            var d = files2.ToDictionary(x => x.GetRelativePath());
            var files = files1.Select(x => new Tuple<File, File>(x, d[x.GetRelativePath()]));
            foreach (var file in files)
            {
                AreEqual(file.Item1, file.Item2, check_absolute, check_content, check_last_write_time);
            }
        }

    }

}