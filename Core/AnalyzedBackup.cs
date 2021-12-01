using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Newtonsoft.Json;
using QBackup.Logging;



namespace QBackup.Core
{

    /// <summary>
    /// Object that supposed to represent directory that is meant to be backuped and the data we got from analyzing it.
    /// </summary>
    public class AnalyzedBackup
    {

        #region Static

        public static AnalyzedBackup Create(QualifiedPath dir, bool look_for_ignore_rules, ILog log)
        {
            var res = new AnalyzedBackup();
            res.Initialize(dir, log);
            return res;
        }

        #endregion

        #region Fields

        public List<File> Files { get; }
        public List<Dir> Dirs { get; }

        /// <summary>
        ///     Top most dir that is still included in the backup.
        /// </summary>
        public Dir TopParent { get; private set; }

        /// <summary>
        ///     Dir that the backup itself is located in. Parent of TopParent.
        /// </summary>
        public QualifiedPath LocatedIn { get; private set; }

        #endregion

        #region Properties

        public QualifiedPath Root => TopParent.AbsolutePath;

        public int FilesCount => Files.Count;
        public int DirsCount => Dirs.Count;

        #endregion

        #region Constructors

        private AnalyzedBackup()
        {
            Files = new List<File>();
            Dirs = new List<Dir>();
        }

        #endregion

        #region Methods

        private void AddFiles(Dir dir, List<Match> rules, ILog log)
        {
            var files = Directory.EnumerateFiles(dir.AbsolutePath);
            foreach (var filepath in files)
            {
                if (filepath.EndsWith(Constants.IGNORE_FILE_EXT))
                {
                    try
                    {
                        var list = System.IO.File.ReadAllText(filepath);
                        var ignore = JsonConvert.DeserializeObject<List<Match>>(list);
                        rules.AddRange(ignore);
                    }
                    catch (Exception e)
                    {
                        log.WriteLine(e.ToString());
                    }
                }

                var name = Path.GetFileName(filepath);
                var f = File.Create(name, dir);
                dir.Files.Add(f);
            }

            for (int i = dir.Files.Count - 1; i >= 0; i--)
            {
                var file = dir.Files[i];
                if (rules.Any(x => x.IsMatch(file))) dir.Files.RemoveAt(i);
            }

            Files.AddRange(dir.Files);
        }

        public QualifiedPath Compress(QualifiedPath backup_dir, string pass, int aes_key_size, ILog log)
        {
            log.WriteLine($"Compressing data in {Root.Path} into {backup_dir.Path}");
            var res = GetDestinationDirForCompression(backup_dir);
            if (Directory.Exists(res))
            {
                if (Directory.EnumerateFileSystemEntries(res).Any()) throw new InvalidOperationException("The directory is not empty.");
            }

            Utils.WriteOriginFile(TopParent, backup_dir);
            Utils.Compress(Dirs, backup_dir, pass, aes_key_size, log);
            return res;
        }

        public QualifiedPath GetDestinationDirForCompression(QualifiedPath new_root) => Utils.GetAbsolutePathInNewRoot(TopParent, new_root);

        private void Initialize(QualifiedPath dir, ILog log)
        {
            TopParent = Dir.CreateFromPath(dir, IdGenerator.GenerateId(), out QualifiedPath located_in);
            LocatedIn = located_in;
            Dirs.Add(TopParent);
            WriteDirTree(TopParent, new List<Match>(), log);
            Parallel.ForEach(Files, file => file.WriteFileInfo());
        }

        public void MarkIgnored()
        {
            MarkIgnored(TopParent, new List<Match>());
        }

        private void MarkIgnored(Dir dir, List<Match> rules)
        {
            foreach (var file in dir.Files)
            {
                if (file.Name.EndsWith(Constants.IGNORE_FILE_EXT))
                {
                    file.IsIgnored = true;
                    try
                    {
                        var list = System.IO.File.ReadAllText(file.GetAbsolutePath());
                        var ignore = JsonConvert.DeserializeObject<List<Match>>(list);
                        rules.AddRange(ignore);
                    }
                    catch (Exception e)
                    {
                        Console.WriteLine(e);
                    }
                }
            }

            foreach (var file in dir.Files)
                if (rules.Any(x => x.IsMatch(file)))
                    file.IsIgnored = true;

            foreach (var subdir in dir.Dirs)
                if (rules.Any(x => x.IsMatch(subdir)))
                    subdir.IsIgnored = true;

            foreach (var subdir in dir.Dirs)
            {
                if (subdir.IsIgnored) continue;
                var new_rules = new List<Match>();
                new_rules.AddRange(rules.Where(x => x.Recursive));
                MarkIgnored(subdir, new_rules);
            }
        }

        private void WriteDirTree(Dir dir, List<Match> rules, ILog log)
        {
            dir.CheckIfLink();
            if (dir.IsLink) return;
            AddFiles(dir, rules, log);
            var folders = Directory.EnumerateDirectories(dir.AbsolutePath);
            foreach (var folder in folders)
            {
                var name = Path.GetFileName(folder);
                var subdir = new Dir(name, IdGenerator.GenerateId(), dir);
                if (rules.Any(x => x.IsMatch(subdir))) continue;
                Dirs.Add(subdir);
                dir.Dirs.Add(subdir);
                var new_rules = new List<Match>();
                new_rules.AddRange(rules.Where(x => x.Recursive));
                WriteDirTree(subdir, new_rules, log);
            }
        }

        #endregion

    }

}