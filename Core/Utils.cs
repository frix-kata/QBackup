using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ICSharpCode.SharpZipLib.Core;
using ICSharpCode.SharpZipLib.Zip;
using Newtonsoft.Json;
using QBackup.Logging;



namespace QBackup.Core
{

    public static class Utils
    {

        #region Static

        public static TimeSpan TwoSeconds = TimeSpan.FromSeconds(2);

        public static bool AreEqual(DateTime x1, DateTime x2)
        {
            var diff = Math.Abs(x1.Ticks - x2.Ticks);
            return diff <= TwoSeconds.Ticks;
        }

        public static void CheckPassword(File archive, string pass, ILog log)
        {
            try
            {
                using (var fr = System.IO.File.OpenRead(archive.GetAbsolutePath()))
                {
                    using (var zf = new ZipFile(fr))
                    {
                        if (pass != null) zf.Password = pass;
                        for (int i = 0; i < zf.Count; i++)
                        {
                            var entry = zf[0];
                            if (!entry.IsFile)
                                throw new InvalidOperationException(
                                    $"Expected all archived entries to be a file. {entry.Name} in {archive.GetAbsolutePath()} is not.");
                            // Unzip file in buffered chunks. This is just as fast as unpacking
                            // to a buffer the full size of the file, but does not waste memory.
                            // The "using" will close the stream even if an exception occurs.
                            using (zf.GetInputStream(entry))
                            {
                            }
                        }
                    }
                }
            }
            catch (Exception e)
            {
                log.WriteError($"{e.Message} - {archive.GetAbsolutePath()}");
            }
        }

        public static void Compress(IList<Dir> dirs, QualifiedPath root, string pass, int aes_key_size, ILog log)
        {
            log.StartTimer();
            log.WriteLine($"Compressing {dirs.Count} dirs into {root.Path}.");
            foreach (var dir in dirs) CreateDirInNewRoot(dir, root);
            log.WriteLine("Created dir tree");
            var count = 0;
            Parallel.ForEach(
                dirs, (dir, state) =>
                {
                    Interlocked.Increment(ref count);
                    if (dir.IsIgnored) return;
                    Compress(dir, root, pass, aes_key_size, log);
                    log.OverWriteLastLine($"{count} out of {dirs.Count}, dir: {dir.AbsolutePath}");
                });
            log.WriteLine("Compression completed");
            log.StopTimer();
        }

        [ThreadSafe]
        public static void Compress(Dir dir, QualifiedPath backup_dir, string pass, int aes_key_size, ILog log)
        {
            try
            {
                var dest_dir = $"{backup_dir.Path}{dir.RelativePath}";
                if (dir.Files.Count == 0) return;
                var zip_file = $"{dest_dir}{Constants.ARCHIVED_FILES_NAME}";
                using (var create_file = new FileStream(zip_file, FileMode.Create))
                {
                    using (var zs = new ZipOutputStream(create_file))
                    {
                        zs.SetLevel(3);
                        zs.Password = pass;
                        foreach (var file in dir.Files)
                        {
                            if (file.IsIgnored) continue;
                            try
                            {
                                using (var read_file = System.IO.File.OpenRead(file.GetAbsolutePath()))
                                {
                                    var entry_name = ZipEntry.CleanName(file.Name);
                                    var entry = new ZipEntry(entry_name);
                                    entry.AESKeySize = aes_key_size;
                                    entry.DateTime = file.LastWriteTime;
                                    entry.Size = file.Size;
                                    zs.PutNextEntry(entry);
                                    var buffer = new byte[4096];
                                    StreamUtils.Copy(read_file, zs, buffer);
                                    zs.CloseEntry();
                                }
                            }
                            catch (Exception e)
                            {
                                log.WriteError($"{file.GetRelativePath()} - {e.Message}");
                            }
                        }
                    }
                }
            }
            catch (Exception e)
            {
                log.WriteError($"{dir.RelativePath} - {e.Message}");
            }
        }

        public static QualifiedPath CreateDirInNewRoot(Dir dir, QualifiedPath root)
        {
            var dest_dir = $"{root.Path}{dir.RelativePath}";
            Directory.CreateDirectory(dest_dir);
            return dest_dir;
        }

        public static void CreateDirTree(IList<Dir> dirs, QualifiedPath root)
        {
            foreach (var dir in dirs) CreateDirInNewRoot(dir, root);
        }

        public static (Dictionary<int, Dir>, Dictionary<int, File>) Deserialize(
            string path,
            Dir.Serialized[] dir_data,
            File.Serialized[] file_data)
        {
            var dirs = new Dictionary<int, Dir>();
            var files = new Dictionary<int, File>();
            foreach (var s in dir_data) dirs[s.Id] = Dir.BeginBuild(s);

            foreach (var s in file_data) files[s.Id] = File.BeginBuild(s);

            foreach (var s in dir_data)
                Dir.FinishBuild(
                    dirs[s.Id], s.Parent != -1 ? dirs[s.Parent] : null, s.Dirs.Select(x => dirs[x]).ToList(),
                    s.Files.Select(x => files[x]).ToList());

            foreach (var s in dir_data) Dir.FinishBuild2(dirs[s.Id], path);

            foreach (var s in file_data) File.FinishBuild(files[s.Id], dirs[s.Parent]);
            return (dirs, files);
        }

        public static void DoParallel<T>(
            string action_name,
            IList<T> dirs,
            Action<T> a,
            ILog log,
            Func<T, string> identifier,
            Action<T> setup = null,
            string setup_msg = null)
        {
            log.StartTimer();
            log.WriteLine($"{action_name} {dirs.Count} dirs.");

            if (setup != null)
            {
                foreach (var dir in dirs) setup.Invoke(dir);
                if (setup_msg != null) log.WriteLine($"{setup_msg} completed.");
            }

            var count = 0;
            Parallel.ForEach(
                dirs, (dir, state) =>
                {
                    Interlocked.Increment(ref count);
                    try
                    {
                        a(dir);
                    }
                    catch (Exception e)
                    {
                        log.WriteError(e.Message);
                    }

                    log.OverWriteLastLine($"{action_name} {count} out of {dirs.Count}, dir: {identifier(dir)}");
                });
            log.WriteLine($"{action_name} completed.");
            log.StopTimer();
        }

        public static void DoSingleThread<T>(string action_name, IList<T> dirs, Action<T> a, ILog log, Func<T, string> identifier)
        {
            log.StartTimer();
            log.WriteLine($"{action_name} {dirs.Count} dirs.");

            var count = 0;
            foreach (var dir in dirs)
            {
                count++;
                try
                {
                    a(dir);
                }
                catch (Exception e)
                {
                    log.WriteError(e.Message);
                }

                log.OverWriteLastLine($"{action_name} {count} out of {dirs.Count}, dir: {identifier(dir)}");
            }

            log.WriteLine($"{action_name} on {count} dirs completed.");
            log.StopTimer();
        }

        public static void Extract(IList<ArchivedDir> dirs, QualifiedPath extract_to_folder, string password, ILog log)
        {
            log.StartTimer();
            log.WriteLine($"Extracting {dirs.Count} dirs into {extract_to_folder.Path}.");
            foreach (var dir in dirs) CreateDirInNewRoot(dir.Dir, extract_to_folder);
            log.WriteLine("Created dir tree");
            var count = 0;
            Parallel.ForEach(
                dirs, dir =>
                {
                    Interlocked.Increment(ref count);
                    if (dir.Archive == null) return;
                    var dest = $"{extract_to_folder.Path}{dir.Dir.RelativePath}";
                    Extract(dir.Archive, password, dest, log);
                    log.OverWriteLastLine($"{count} out of {dirs.Count}, dir: {dir.Dir.AbsolutePath}");
                });
            log.WriteLine("Extraction complete");
            log.StopTimer();
        }

        public static void Extract(File archive, string pass, QualifiedPath dest_dir, ILog log)
        {
            try
            {
                using (var fr = System.IO.File.OpenRead(archive.GetAbsolutePath()))
                {
                    using (var zf = new ZipFile(fr))
                    {
                        if (pass != null) zf.Password = pass;
                        for (int i = 0; i < zf.Count; i++)
                        {
                            var entry = zf[i];
                            if (!entry.IsFile)
                                throw new InvalidOperationException(
                                    $"Expected all archived entries to be a file. {entry.Name} in {archive.GetAbsolutePath()} is not.");
                            // 4K is optimum
                            var buffer = new byte[4096];

                            // Unzip file in buffered chunks. This is just as fast as unpacking
                            // to a buffer the full size of the file, but does not waste memory.
                            // The "using" will close the stream even if an exception occurs.
                            using (var zip_stream = zf.GetInputStream(entry))
                            {
                                var dest = $"{dest_dir.Path}{entry.Name}";
                                using (Stream fs_output = System.IO.File.Create(dest)) StreamUtils.Copy(zip_stream, fs_output, buffer);
                            }
                        }
                    }
                }
            }
            catch (Exception e)
            {
                log.WriteError($"{e.Message} - {archive.GetAbsolutePath()}");
            }
        }

        public static QualifiedPath GetAbsolutePathInNewRoot(Dir dir, QualifiedPath new_root)
        {
            return $"{new_root.Path}{dir.RelativePath}";
        }


        public static List<File> ReadFilesFromArchive(File archive, Dir parent_dir, string pass, ILog log)
        {
            var res = new List<File>();
            try
            {
                using (var fr = System.IO.File.OpenRead(archive.GetAbsolutePath()))
                {
                    using (var zf = new ZipFile(fr))
                    {
                        if (pass != null) zf.Password = pass;
                        for (int i = 0; i < zf.Count; i++)
                        {
                            var entry = zf[i];
                            if (!entry.IsFile)
                                throw new InvalidOperationException(
                                    $"Expected all archived entries to be a file. {entry.Name} in {archive.GetAbsolutePath()} is not.");
                            var file = File.Create(entry, parent_dir);
                            res.Add(file);
                        }
                    }
                }
            }
            catch (Exception e)
            {
                log.WriteError($"{e.Message} - {archive.GetAbsolutePath()}");
            }

            return res;
        }

        public static void Verify(File archive, string pass, ILog log)
        {
            try
            {
                using (var fr = System.IO.File.OpenRead(archive.GetAbsolutePath()))
                {
                    using (var zf = new ZipFile(fr))
                    {
                        if (pass != null) zf.Password = pass;
                        zf.TestArchive(
                            true, TestStrategy.FindAllErrors, (status, message) =>
                            {
                                if (message != null) log.WriteError($"{message} - {archive.GetAbsolutePath()}");
                            });
                    }
                }
            }
            catch (Exception e)
            {
                log.WriteError($"{e.Message} - {archive.GetAbsolutePath()}");
            }
        }

        public static void Verify(IList<ArchivedDir> dirs, string password, bool check_password_only, ILog log)
        {
            log.StartTimer();
            log.WriteLine($"Verifying {dirs.Count} dirs.");
            var count = 0;
            Parallel.ForEach(
                dirs, dir =>
                {
                    Interlocked.Increment(ref count);
                    if (dir.Archive == null) return;
                    if (check_password_only) CheckPassword(dir.Archive, password, log);
                    else Verify(dir.Archive, password, log);
                    log.OverWriteLastLine($"{count} out of {dirs.Count}, dir: {dir.Dir.AbsolutePath}");
                });
            log.WriteLine("Verification complete");
            log.StopTimer();
        }

        public static void WriteOriginFile(Dir top_dir, QualifiedPath new_root)
        {
            var info = new BackupOrigin {Source = top_dir.AbsolutePath};
            System.IO.File.WriteAllText(
                $"{new_root.Path}{top_dir.Name}{Constants.BACKUP_INFO_FILE_EXT}", JsonConvert.SerializeObject(info));
        }

        #endregion

    }

}