using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;



namespace QBackup
{

    public class ArchivedBackup
    {

        public class Serialized
        {

            public string RootDir;
            public File.Serialized[] Files;
            public Dir.Serialized[] Dirs;
            public ArchivedDir.Serialized[] ArchivedDirs;

        }


        public Serialized Serialize()
        {
            return new Serialized()
            {
                RootDir = LocatedIn,
                Files = ArchivedDirs.Select(x => x.Archive).Where(x => x != null).Concat(ArchivedDirs.SelectMany(x => x.ArchivedFiles)).Select(x => x.Serialize()).ToArray(),
                Dirs = ArchivedDirs.Select(x => x.Dir.Serialize()).ToArray(),
                ArchivedDirs = ArchivedDirs.Select(x => x.Serialize()).ToArray()
            };
        }

        public static ArchivedBackup Deserialize(Serialized s)
        {
            var dict = Utils.Deserialize(s.RootDir, s.Dirs, s.Files);
            return new ArchivedBackup()
            {
                ArchivedDirs = s.ArchivedDirs.Select(x => new ArchivedDir(x, dict.Item1, dict.Item2)).ToList()
            };
        }

        

        public List<ArchivedDir> ArchivedDirs { get; private set; }
        public QualifiedPath LocatedIn { get; private set; }

        public QualifiedPath Root => TopParent.Dir.AbsolutePath;

        public ArchivedDir TopParent { get; private set; }

        public static ArchivedBackup CreateFromPath(QualifiedPath path, string pass, ILog log)
        {
            var r = new ArchivedBackup();
            var analyzed = AnalyzedBackup.Create(path, false, log);
            r.LocatedIn = analyzed.LocatedIn;
            r.ArchivedDirs = new List<ArchivedDir>(analyzed.Dirs.Count);
            var bag = new ConcurrentBag<ArchivedDir>();
            Parallel.ForEach(analyzed.Dirs, dir => bag.Add(new ArchivedDir(dir)));
            r.ArchivedDirs.AddRange(bag);
            Parallel.ForEach(r.ArchivedDirs, dir => dir.Analyze(pass, log));
            r.TopParent = r.ArchivedDirs.Single(x => x.Dir.RelativePath == analyzed.TopParent.RelativePath);
            if (!r.IsPasswordCorrect(pass))
            {
                throw new InvalidOperationException($"Supplied password doesn't match the password of at least some of the files in the archive.");
            }
            return r;
        }

        public bool IsPasswordCorrect(string password)
        {
            var max_files_to_check = 10;
            var counter = 0;
            var log = new LogLines();
            for (int i = 0; i < ArchivedDirs.Count; i++)
            {
                var file = ArchivedDirs[i].Archive;
                if (file == null) continue;
                Utils.CheckPassword(file, password, log);
                counter++;
                if (counter >= max_files_to_check) break;
            }
            var errors = log.GetErrors();
            return errors.Length == 0;
        }

        public QualifiedPath GetDestinationForExtraction(QualifiedPath new_root) => Utils.GetAbsolutePathInNewRoot(TopParent.Dir, new_root);

        public QualifiedPath Extract(QualifiedPath extract_to_folder, string password, ILog log)
        {
            log.WriteLine($"Extracting data in {Root.Path} into {extract_to_folder.Path}");
            var res = GetDestinationForExtraction(extract_to_folder);
            if (Directory.Exists(res))
            {
                if (Directory.EnumerateFileSystemEntries(res).Any())
                {
                    throw new InvalidOperationException($"The directory is not empty.");
                }
            }
            Utils.Extract(ArchivedDirs, extract_to_folder, password, log);
            return res;
        }

        public void Verify(string password, bool check_password_only, ILog log)
        {
            Utils.Verify(ArchivedDirs, password, check_password_only, log);
        }

        

        private ArchivedBackup()
        {
            
        }

    }

}