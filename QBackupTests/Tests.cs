using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using NUnit.Framework;
using QBackup;
using QBackup.Core;
using QBackup.Logging;
using File = System.IO.File;



namespace QBackupTests
{

    /// <summary>
    /// Some integration tests for the utility.
    /// </summary>
    public class Tests
    {
        //todo tests won't clean up after themselves in case of failing assertions


        [SetUp]
        public void Setup()
        {
        }

        /// <summary>
        /// Test that backup won't be able to extract if the password isn't the same as the one that was used to compress.
        /// </summary>
        [Test]
        public static void TestCheckPassword()
        {
            var log = new LogLines();
            var t = new TestDir(InputDir);
            t.Create();
            var src = AnalyzedBackup.Create(t.Dirs[0], true, log);

            var archived_dir = src.Compress(OutputDir, Password, 256, log);
            var archived = ArchivedBackup.CreateFromPath(archived_dir, Password, log);
            QAssert.AreEqual(src, archived);

            var wrong_password = Password + " wrong password";
            try
            {
                ArchivedBackup.CreateFromPath(archived_dir, wrong_password, log);
            }
            catch (Exception e)
            {
                Assert.True(e.Message.Contains("password"));
            }
            

            var extracted_dir = archived.GetDestinationForExtraction(ExtractedDir);
            try
            {
                archived.Extract(ExtractedDir, wrong_password, log);
            }
            catch (Exception e)
            {
                Assert.True(e.Message.Contains("Invalid password for AES"));
            }
            finally
            {
                Directory.Delete(extracted_dir.Path, true);
            }
            Directory.Delete(src.Root.Path, true);
            Directory.Delete(archived_dir.Path, true);
            
        }

        /// <summary>
        /// Test that backup won't write into non empty dir when compressing/extracting.
        /// </summary>
        [Test]
        public static void TestCheckDirectoryBeforeWriting()
        {
            var log = new LogLines();
            var t = new TestDir(InputDir);
            t.Create();
            var src = AnalyzedBackup.Create(t.Dirs[0], true, log);
            var expected_error = "The directory is not empty.";
            var archived_dir = src.GetDestinationDirForCompression(OutputDir);
            test(() => src.Compress(OutputDir, Password, 256, log), expected_error, archived_dir, p => Directory.CreateDirectory(p.Path + "dir\\"));
            test(() => src.Compress(OutputDir, Password, 256, log), expected_error, archived_dir, p => System.IO.File.WriteAllText(p.Path + "file.txt", "content"));
            src.Compress(OutputDir, Password, 256, log);
            var archived = ArchivedBackup.CreateFromPath(archived_dir, Password, log);
            QAssert.AreEqual(src, archived);
            var extracted_dir = archived.GetDestinationForExtraction(ExtractedDir);
            test(() => archived.Extract(ExtractedDir, Password, log), expected_error, extracted_dir, p => Directory.CreateDirectory(p.Path + "dir\\"));
            test(() => archived.Extract(ExtractedDir, Password, log), expected_error, extracted_dir, p => System.IO.File.WriteAllText(p.Path + "file.txt", "content"));
            archived.Extract(ExtractedDir, Password, log);
            var extracted = AnalyzedBackup.Create(extracted_dir, true, log);
            QAssert.AreEqual(src, extracted);

            //clean up
            Directory.Delete(src.Root.Path, true);
            Directory.Delete(archived_dir.Path, true);
            Directory.Delete(extracted_dir.Path, true);

            void test(Action a, string msg, QualifiedPath path, Action<QualifiedPath> do_on_dir)
            {
                Directory.CreateDirectory(path.Path);
                do_on_dir(path);
                try
                {
                    a();
                }
                catch (Exception e)
                {
                    Assert.AreEqual(msg, e.Message);
                }
                Directory.Delete(path.Path, true);
            }
        }

        /// <summary>
        /// Integration test that tests that analysis tools actually generate a correct representation of source directory,
        /// it archives it correctly and is then able to extract it back without corrupting or losing any files.
        /// </summary>
        [Test]
        public static void TestAnalysis()
        {
            var log = new LogLines();
            var t = new TestDir(InputDir);
            t.Create();
            var src = AnalyzedBackup.Create(t.Dirs[0], true, log);
            Assert.AreEqual(t.Dirs.Count, src.Dirs.Count);
            Assert.AreEqual(t.Files.Count, src.Files.Count);

            var dirs_by_path = src.Dirs.ToDictionary(x => x.AbsolutePath);
            foreach (var exp_dir in t.Dirs)
            {
                CompareDir(exp_dir, dirs_by_path[exp_dir], t);
            }

            var files_by_path = src.Files.ToDictionary(x => x.GetAbsolutePath());
            foreach (var exp_file in t.Files)
            {
                Assert.AreEqual(exp_file, files_by_path[exp_file].GetAbsolutePath());
                var fi = new FileInfo(exp_file);
                QAssert.AreEqual(fi.Length, files_by_path[exp_file].Size);
                QAssert.AreEqual(fi.LastWriteTime, files_by_path[exp_file].LastWriteTime);
            }

            var archived_dir = src.Compress(OutputDir, Password, 256, log);
            var archived = ArchivedBackup.CreateFromPath(archived_dir, Password, log);
            QAssert.AreEqual(src, archived);

            var extracted_dir = archived.Extract(ExtractedDir, Password, log);
            var extracted = AnalyzedBackup.Create(extracted_dir, true, log);
            QAssert.AreEqual(src, extracted);

            Directory.Delete(src.Root.Path, true);
            Directory.Delete(archived_dir.Path, true);
            Directory.Delete(extracted_dir.Path, true);
        }

        /// <summary>
        /// Compress, extract and validate backups.
        /// </summary>
        /// <param name="src_dir"></param>
        public static void TestArchivingAndExtraction()
        {
            var log = new LogLines();
            var t = new TestDir(InputDir);
            t.Create();
            var src = AnalyzedBackup.Create(t.Dirs[0], true, log);
            var archived_dir = src.Compress(OutputDir, Password, 256, log);
            var archived = ArchivedBackup.CreateFromPath(archived_dir, Password, log);
            QAssert.AreEqual(src, archived);

            var extracted_dir = archived.Extract(ExtractedDir, Password, log);
            var extracted = AnalyzedBackup.Create(extracted_dir, true, log);
            QAssert.AreEqual(src, extracted);

            Directory.Delete(archived_dir.Path, true);
            Directory.Delete(extracted_dir.Path, true);
        }

        /// <summary>
        /// Integration test to test how utility handles updating the backup,
        /// with files and dirs being deleted, added, moved, renamed, overwritten etc.
        /// </summary>
        [Test]
        public static void TestDiffing()
        {
            var log = new LogConsole();
            var t = new TestDir(InputDir);
            t.Create();
            var src = AnalyzedBackup.Create(t.Dirs[0], true, log);
            var archived_dir = src.Compress(OutputDir, Password, 256, log);
            var archived = ArchivedBackup.CreateFromPath(archived_dir, Password, log);
            QAssert.AreEqual(src, archived);

            var test_adding_dir = src.TopParent.Dirs.Single(x => x.Name == "folder 0");
            var test_deleting_dir = src.TopParent.Dirs.Single(x => x.Name == "folder 1");
            var test_updating_dir = src.TopParent.Dirs.Single(x => x.Name == "folder 2");
            var test_moving_dir = src.TopParent.Dirs.Single(x => x.Name == "folder 3");

            //setup add test
            var dirs_added = new List<string>();
            var files_added = new List<string>();
            dirs_added.Add(test_adding_dir.AppendToAbsolutePath($"added dir1\\"));
            dirs_added.Add(test_adding_dir.AppendToAbsolutePath($"added dir2\\"));
            files_added.Add(test_adding_dir.AppendToAbsolutePath($"added dir1\\added file.txt"));
            dirs_added.Add(test_adding_dir.AppendToAbsolutePath($"added dir1\\added dir\\"));
            files_added.Add(test_adding_dir.AppendToAbsolutePath($"added dir1\\added dir\\added file.txt"));
            foreach (var dir in dirs_added)
            {
                Directory.CreateDirectory(dir);
            }
            foreach (var file in files_added)
            {
                System.IO.File.WriteAllText(file, "This file has been added!");
            }

            //setup delete test
            var dirs_deleted = new List<string>();
            foreach (var dir in test_deleting_dir.Dirs)
            {
                Directory.Delete(dir.AbsolutePath, true);
                dirs_deleted.AddRange(dir.GetThisWithAllDescendants().Select(x => x.AbsolutePath));
            }

            //setup update test
            //add file to empty dir
            var dirs_updated = new List<string>();
            var update = test_updating_dir.GetAllDescendants().First(x => x.Dirs.Count == 0 && x.Files.Count == 0);
            System.IO.File.WriteAllText(update.AppendToAbsolutePath("added file.txt"), "Added file");
            dirs_updated.Add(update.AbsolutePath);
            //empty non empty dir
            update = test_updating_dir.GetAllDescendants().First(x => x.Dirs.Count > 0 && x.Files.Count > 0);
            foreach (var f in update.Files)
            {
                System.IO.File.Delete(f.GetAbsolutePath());
            }
            dirs_updated.Add(update.AbsolutePath);
            //remove file, add another one
            update = test_updating_dir.GetAllDescendants().First(x => x.Dirs.Count > 0 && x.Files.Count > 1 && !dirs_updated.Contains(x.AbsolutePath));
            System.IO.File.Delete(update.Files[0].GetAbsolutePath());
            System.IO.File.WriteAllText(update.AppendToAbsolutePath("added file.txt"), "Added file");
            dirs_updated.Add(update.AbsolutePath);
            //overwrite one file
            update = test_updating_dir.GetAllDescendants().First(x => x.Dirs.Count > 0 && x.Files.Count > 1 && !dirs_updated.Contains(x.AbsolutePath));
            System.IO.File.WriteAllText(update.Files[0].GetAbsolutePath(), "Updated file");
            dirs_updated.Add(update.AbsolutePath);
            //overwrite one file with same content
            update = test_updating_dir.GetAllDescendants().First(x => x.Dirs.Count > 0 && x.Files.Count > 1 && !dirs_updated.Contains(x.AbsolutePath));
            System.IO.File.WriteAllText(update.Files[0].GetAbsolutePath(), System.IO.File.ReadAllText(update.Files[0].GetAbsolutePath()));
            dirs_updated.Add(update.AbsolutePath);
            //rename file
            update = test_updating_dir.GetAllDescendants().First(x => x.Dirs.Count > 0 && x.Files.Count > 1 && !dirs_updated.Contains(x.AbsolutePath));
            System.IO.File.Move(update.Files[0].GetAbsolutePath(), update.Files[0].GetAbsolutePath() + ".renamed");
            dirs_updated.Add(update.AbsolutePath);

            //test moving and renaming dirs
            var moved_dirs_from = new List<string>();
            var moved_dirs_to = new List<string>();
            //move one dir with it's descendants
            var move = test_moving_dir.GetAllDescendants().First(x => x.Dirs.Count > 0 && x.Files.Count > 0 && x.Parent != test_moving_dir && !moved_dirs_from.Contains(x.AbsolutePath));
            Move(move.AbsolutePath, test_moving_dir.AppendToAbsolutePath(move.Name + "\\"));
            moved_dirs_from.AddRange(move.GetThisWithAllDescendants().Select(x => x.AbsolutePath));
            moved_dirs_to.AddRange(move.GetThisWithAllDescendants().Select(x => $"{test_moving_dir.AbsolutePath}{x.RelativePath.Substring(move.Parent.RelativePath.Length)}"));
            //rename dir
            move = test_moving_dir.GetAllDescendants().First(x => x.Dirs.Count > 0 && x.Files.Count > 0 && x.Parent != test_moving_dir && !moved_dirs_from.Contains(x.AbsolutePath));
            Move(move.AbsolutePath, $"{move.AbsolutePath.TrimEnd('\\')}_renamed\\");
            moved_dirs_from.AddRange(move.GetThisWithAllDescendants().Select(x => x.AbsolutePath));
            moved_dirs_to.AddRange(move.GetThisWithAllDescendants().Select(x => $"{move.AbsolutePath.TrimEnd('\\')}_renamed\\{x.RelativePath.Substring(move.RelativePath.Length)}"));
            //delete dir after moving it's child
            var move_parent = test_moving_dir.GetAllDescendants().First(x => x.Dirs.Count > 0 && x.Files.Count > 0 && x.Parent != test_moving_dir && !moved_dirs_from.Contains(x.AbsolutePath));
            move = move_parent.Dirs[0];
            Move(move.AbsolutePath, test_moving_dir.AppendToAbsolutePath(move.Name + "\\"));
            Directory.Delete(move_parent.AbsolutePath, true);
            moved_dirs_from.AddRange(move.GetThisWithAllDescendants().Select(x => x.AbsolutePath));
            moved_dirs_to.AddRange(move.GetThisWithAllDescendants().Select(x => $"{test_moving_dir.AbsolutePath}{x.RelativePath.Substring(move.Parent.RelativePath.Length)}"));
            dirs_deleted.Add(move_parent.AbsolutePath);
            dirs_deleted.AddRange(move_parent.Dirs.Skip(1).SelectMany(x => x.GetThisWithAllDescendants()).Select(x => x.AbsolutePath));

            var updated_src = AnalyzedBackup.Create(src.TopParent.AbsolutePath, true, log);
            //var src_by_path = updated_src.Dirs.ToDictionary(x => x.AbsolutePath);
            //var old_src_by_path = src.Dirs.ToDictionary(x => x.AbsolutePath);
            var comparison = BackupDiff.Create(updated_src, archived);
            comparison.Compare(log);

            //we can't test this by checking that operations to the backup are identical
            //dirs with identical files or empty dirs, can be counted as moved dirs even if they're supposed to be deleted or added
            //QAssert.AreEqual(dirs_added.Select(x => src_by_path[x]).ToList(), comparison.DirsToAdd.ToList(), false, true, true);
            //QAssert.AreEqual(dirs_deleted.Select(x => old_src_by_path[x]).ToList(), comparison.AllDirsToDelete.ToList(), false, true, true);
            //QAssert.AreEqual(dirs_updated.Select(x => src_by_path[x]).ToList(), comparison.DirsToUpdate.Values.ToList(), false, true, true);
            //QAssert.AreEqual(moved_dirs_from.Select(x => old_src_by_path[x]).ToList(), comparison.DirsToMove.Values.ToList(), false, true, true);
            //QAssert.AreEqual(moved_dirs_to.Select(x => src_by_path[x]).ToList(), comparison.DirsToMove.Keys.ToList(), false, true, true);

            comparison.Commit(Password, 256, log);
            var updated_archive = ArchivedBackup.CreateFromPath(archived_dir, Password, log);
            QAssert.AreEqual(updated_src, updated_archive);

            var extracted_dir = updated_archive.Extract(ExtractedDir, Password, log);
            var extracted = AnalyzedBackup.Create(extracted_dir, true, log);
            QAssert.AreEqual(updated_src, extracted);

            //cleanup
            Directory.Delete(updated_src.Root, true);
            Directory.Delete(updated_archive.Root, true);
            Directory.Delete(extracted.Root, true);
        }

        private static void Move(string src, string dst)
        {
            if (Directory.Exists(dst)) throw new InvalidOperationException($"Move dir already exists. This will corrupt the test.");
            Directory.Move(src, dst);
        }

        private static void CompareDir(QualifiedPath exp, Dir res, TestDir t)
        {
            var exp_subdirs = Directory.EnumerateDirectories(exp).Select(x => new QualifiedPath(x)).Where(t.ContainsDir).ToArray();
            var res_subdirs = res.Dirs.ToDictionary(x => x.AbsolutePath);
            Assert.AreEqual(exp_subdirs.Length, res_subdirs.Count);
            foreach (var exp_subdir in exp_subdirs)
            {
                var res_subdir = res_subdirs[exp_subdir];
                Assert.AreEqual(exp_subdir.Path, res_subdir.AbsolutePath);
            }

            var exp_files = Directory.EnumerateFiles(exp).Where(t.ContainsFile).ToArray();
            var res_files = res.Files.ToDictionary(x => x.GetAbsolutePath());
            Assert.AreEqual(exp_files.Length, res_files.Count);
            foreach (var exp_file in exp_files)
            {
                var res_file = res_files[exp_file];
                Assert.AreEqual(exp_file, res_file.GetAbsolutePath());
            }
        }

        

        
        /*[TestCase(Output1)]
        public static void TestSerialization(string dir)
        {
            var log = new LogLines();
            log.WriteLine($"Opening {dir}");
            var sw = new Stopwatch();
            sw.Start();
            var g = ArchivedBackup.CreateFromPath(dir, Password, log);
            log.WriteLine($"Analyzed archived dir in {sw.ElapsedMilliseconds} ms");
            sw.Restart();
            var s = g.Serialize();
            var d = ArchivedBackup.Deserialize(s);
            log.WriteLine($"Serialized and deserialized data {sw.ElapsedMilliseconds} ms");
            sw.Restart();
            Assert.AreEqual(g.ArchivedDirs.Count, d.ArchivedDirs.Count);
            for (int i = 0; i < g.ArchivedDirs.Count; i++)
            {
                var ga = g.ArchivedDirs[i];
                var da = d.ArchivedDirs[i];
                Assert.AreEqual(ga.ArchivedFiles.Count, da.ArchivedFiles.Count);
                QAssert.AreEqual(ga.Dir, da.Dir);
                QAssert.AreEqual(ga.Archive, da.Archive);
                for (int j = 0; j < ga.ArchivedFiles.Count; j++)
                {
                    var gaf = ga.ArchivedFiles[j];
                    var daf = da.ArchivedFiles[j];
                    QAssert.AreEqual(gaf, daf);
                }
            }
            log.WriteLine($"Ran assertions on deserialized data against generated data in {sw.ElapsedMilliseconds} ms");
            sw.Stop();
        }*/

        [Test]
        public static void TestIntegrityCheck()
        {
            var log = new LogLines();
            var t = new TestDir(InputDir);
            t.Create();
            var src = AnalyzedBackup.Create(t.Dirs[0], true, log);
            var archived_dir = src.Compress(OutputDir, Password, 256, log);
            var archive = ArchivedBackup.CreateFromPath(archived_dir, Password, log);
            QAssert.AreEqual(src, archive);
            var file_count = archive.ArchivedDirs.Count(x => !x.IsEmpty);

            //control
            archive.Verify(Password, false, log);
            var errors = log.GetErrors();
            Assert.AreEqual(0, errors.Length);
            log.Reset();

            //check password verification
            archive.Verify(Password + "wrong password", false, log);
            errors = log.GetErrors();
            Assert.AreEqual(file_count, errors.Length);
            foreach (var error in errors)
            {
                Assert.IsTrue(error.Contains("Invalid password for AES"));
            }
            log.Reset();

            //check password verification
            archive.Verify(Password + "wrong password", true, log);
            errors = log.GetErrors();
            Assert.AreEqual(file_count, errors.Length);
            foreach (var error in errors)
            {
                Assert.IsTrue(error.Contains("Invalid password for AES"));
            }
            log.Reset();

            //check corruption verification
            var files_to_corrupt = archive.ArchivedDirs.Where(x => x.Archive != null).Select(x => x.Archive).Take(10).ToArray();
            foreach (var file in files_to_corrupt)
            {
                var b = File.ReadAllBytes(file.GetAbsolutePath());
                for (int i = 0; i < b.Length; i += 5)
                {
                    b[i] = 12;
                }
                File.WriteAllBytes(file.GetAbsolutePath(), b);
            }

            archive.Verify(Password, false, log);
            errors = log.GetErrors();
            Assert.AreEqual(files_to_corrupt.Length, errors.Length);
            log.Reset();

            Directory.Delete(src.Root, true);
            Directory.Delete(archive.Root, true);

        }



        //directories where tests are performed are meant to be created manually
        private const string InputDir = @"M:\qb test\src\";
        private const string ExtractedDir = @"M:\qb test\extracted\";
        private const string OutputDir = @"M:\qb test\dest\";

        private const string Password = "foo";

    }

}