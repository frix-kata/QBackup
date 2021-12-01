using System.Diagnostics;
using System.IO;
using System.Linq;
using NUnit.Framework;
using QBackup.Core;
using QBackup.Logging;



namespace QBackupTests
{

    /// <summary>
    /// Some tests that have only been written to test performance. Expect backup dirs to be supplied manually by the user.
    /// </summary>
    public class PerformanceTests
    {

        [TestCase(Input1, Output1)]
        [TestCase(Input2, Output2)]
        public static void TestArchivingSpeed(string dir, string output_dir)
        {
            var log = new LogConsole();
            if (Directory.Exists(output_dir))
            {
                Directory.Delete(output_dir, true);
            }
            var sw = new Stopwatch();
            sw.Start();
            var t = AnalyzedBackup.Create(dir, true, log);
            sw.Stop();
            log.WriteLine($"Analyzed in {sw.ElapsedMilliseconds} ms");
            log.WriteLine($"Found {t.FilesCount} files and {t.DirsCount} dirs");
            var symlinks = t.Dirs.Where(x => x.IsLink).ToArray();
            log.WriteLine($"Found {symlinks.Length} symlinks.");
            t.Compress(OutputDir, Password, 256, log);
        }

        [TestCase(Input1)]
        [TestCase(Input2)]
        public static void TestAnalysisSpeed(string dir)
        {
            var log = new LogConsole();
            var sw = new Stopwatch();
            sw.Start();
            var t = AnalyzedBackup.Create(dir, true, log);
            sw.Stop();
            log.WriteLine($"Analyzed in {sw.ElapsedMilliseconds} ms");
            log.WriteLine($"Found {t.FilesCount} files and {t.DirsCount} dirs");
            var symlinks = t.Dirs.Where(x => x.IsLink).ToArray();
            log.WriteLine($"Found {symlinks.Length} symlinks.");
        }


        [TestCase(Output1)]
        [TestCase(Output2)]
        public static void TestArchiveAnalyzeSpeed(string dir)
        {
            var log = new LogConsole();
            log.WriteLine($"Opening {dir}");
            var sw = new Stopwatch();
            sw.Start();
            var archived_backup = ArchivedBackup.CreateFromPath(dir, Password, log);
            sw.Stop();
            log.WriteLine($"Analyzed in {sw.ElapsedMilliseconds} ms.");
            log.WriteLine($"Dirs: {archived_backup.ArchivedDirs.Count}, files: {archived_backup.ArchivedDirs.Count(x => !x.IsEmpty)}");
        }


        private const string Input1 = InputDir + "mod\\";
        private const string Output1 = OutputDir + "mod\\";
        private const string Input2 = InputDir + "mod2\\";
        private const string Output2 = OutputDir + "mod2\\";


        private const string InputDir = @"M:\qb test\src\";
        private const string OutputDir = @"M:\qb test\dest\";

        private const string Password = "foo";

    }

}