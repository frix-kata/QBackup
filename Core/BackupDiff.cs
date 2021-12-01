using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;



namespace QBackup
{

    public class BackupDiff
    {

        public HashSet<ArchivedDir> TopDirsToDelete { get; private set; }
        public HashSet<ArchivedDir> AllDirsToDelete { get; private set; }
        public HashSet<Dir> DirsToAdd { get; private set; }
        public Dictionary<ArchivedDir, Dir> DirsToUpdate { get; private set; }
        public Dictionary<Dir, ArchivedDir> DirsToMove { get; private set; }
        public AnalyzedBackup Source { get;  }
        public ArchivedBackup Destination { get; }

        public static BackupDiff Create(AnalyzedBackup src, ArchivedBackup dest)
        {
            return new BackupDiff(src, dest);
        }

        private BackupDiff(AnalyzedBackup src, ArchivedBackup dest)
        {
            Destination = dest;
            Source = src;
        }

        public void Compare(ILog log)
        {
            log.WriteLine($"Comparing a source dir {Source.Root.Path} with destination {Destination.Root.Path}");
            (DirsToAdd, DirsToUpdate) = GetDirsToAddAndUpdate(Source.Dirs, Destination.ArchivedDirs);
            AllDirsToDelete = GetAllDirsToDelete(Source.Dirs, Destination.ArchivedDirs);
            DirsToMove = FindMovedDirs(DirsToAdd, AllDirsToDelete);
            TopDirsToDelete = GetTopDirsToDelete(AllDirsToDelete);
            log.WriteLine($"Comparison complete: dirs to add: {DirsToAdd.Count}, to update {DirsToUpdate.Count}, to delete: {AllDirsToDelete.Count}, to move: {DirsToMove.Count}");
        }

        public static (HashSet<Dir> _dirs_to_add, Dictionary<ArchivedDir, Dir> _dirs_to_update) GetDirsToAddAndUpdate(List<Dir> source_dirs, List<ArchivedDir> dest_dirs)
        {
            var dirs_to_add = new HashSet<Dir>();
            var dirs_to_update = new Dictionary<ArchivedDir, Dir>();
            var archived_dirs_by_rel_path = dest_dirs.ToDictionary(x => x.Dir.RelativePath);
            foreach (var dir in source_dirs)
            {
                if (archived_dirs_by_rel_path.TryGetValue(dir.RelativePath, out ArchivedDir a))
                {
                    if (!IsFileContentEqual(dir, a))
                    {
                        IsFileContentEqual(dir, a);
                        dirs_to_update[a] = dir;
                    }
                }
                else dirs_to_add.Add(dir);
            }
            return (dirs_to_add, dirs_to_update);
        }

        public static HashSet<ArchivedDir> GetAllDirsToDelete(List<Dir> source_dirs, List<ArchivedDir> dest_dirs)
        {
            

            var dirs_by_rel_path = source_dirs.ToDictionary(x => x.RelativePath);
            var res = new HashSet<ArchivedDir>();
            foreach (var archived_dir in dest_dirs)
            {
                if (!dirs_by_rel_path.ContainsKey(archived_dir.Dir.RelativePath)) res.Add(archived_dir);
            }
            return res;
        }

        public static Dictionary<Dir, ArchivedDir> FindMovedDirs(HashSet<Dir> dirs_to_add, HashSet<ArchivedDir> all_dirs_to_delete)
        {
            var dirs_to_move = new Dictionary<Dir, ArchivedDir>();
            var dirs_to_add_by_name = new Dictionary<string, List<Dir>>();
            foreach (var dir in dirs_to_add)
            {
                if (!dirs_to_add_by_name.ContainsKey(dir.Name)) dirs_to_add_by_name[dir.Name] = new List<Dir>();
                dirs_to_add_by_name[dir.Name].Add(dir);
            }

            foreach (var dir_to_delete in all_dirs_to_delete)
            {
                if (dirs_to_add_by_name.TryGetValue(dir_to_delete.Dir.Name, out List<Dir> list))
                {
                    Dir found_dir = null;
                    foreach (var dir_to_add in list)
                    {
                        if (IsMovedDir(dir_to_add, dir_to_delete))
                        {
                            found_dir = dir_to_add;
                            dirs_to_move[dir_to_add] = dir_to_delete;
                            dirs_to_add.Remove(dir_to_add);
                            break;
                        }
                    }
                    if (found_dir != null) list.Remove(found_dir);
                }
            }



            var dirs_to_add_by_file_count = new Dictionary<int, List<Dir>>();
            foreach (var dir in dirs_to_add)
            {
                if (!dirs_to_add_by_file_count.ContainsKey(dir.Files.Count)) dirs_to_add_by_file_count[dir.Files.Count] = new List<Dir>();
                dirs_to_add_by_file_count[dir.Files.Count].Add(dir);
            }

            var all_dirs_to_delete_left = all_dirs_to_delete.ToHashSet();
            all_dirs_to_delete_left.ExceptWith(dirs_to_move.Values);
            foreach (var dir_to_delete in all_dirs_to_delete_left)
            {
                if (dirs_to_add_by_file_count.TryGetValue(dir_to_delete.ArchivedFiles.Count, out List<Dir> list))
                {
                    Dir found_dir = null;
                    foreach (var dir_to_add in list)
                    {
                        if (IsRenamedDir(dir_to_add, dir_to_delete))
                        {
                            found_dir = dir_to_add;
                            dirs_to_move[dir_to_add] = dir_to_delete;
                            dirs_to_add.Remove(dir_to_add);
                            break;
                        }
                    }
                    if (found_dir != null) list.Remove(found_dir);
                }
            }

            return dirs_to_move;
        }

        public static HashSet<ArchivedDir> GetTopDirsToDelete(HashSet<ArchivedDir> all_dirs_to_delete)
        {
            var res = new HashSet<ArchivedDir>();
            var dir_set = all_dirs_to_delete.Select(x => x.Dir).ToHashSet();
            foreach (var dir in all_dirs_to_delete)
            {
                //if the parent dir of this dir is not to be deleted than this dir is the local root
                if (!dir_set.Contains(dir.Dir.Parent)) res.Add(dir);
            }
            return res;
        }

        public static bool IsMovedDir(Dir dir, ArchivedDir archive)
        {
            if (dir.Name != archive.Dir.Name) return false;
            if (!IsFileContentEqual(dir, archive)) return false;
            return true;
        }

        public static bool IsRenamedDir(Dir dir, ArchivedDir archive)
        {
            if (dir.Name == archive.Dir.Name) return false;
            if (!IsFileContentEqual(dir, archive)) return false;
            return true;
        }

        public void Commit(string pass, int aes_key_size, ILog log)
        {
            Utils.WriteOriginFile(Source.TopParent, Destination.LocatedIn);
            log.WriteLine($"Syncing {Source.Root.Path} with {Destination.Root.Path}");
            //add
            Utils.DoParallel("Add", DirsToAdd.ToArray(), dir => Utils.Compress(dir, Destination.LocatedIn, pass, aes_key_size, log), log, dir => dir.RelativePath, dir =>
            {
                Utils.CreateDirInNewRoot(dir, Destination.LocatedIn);
            });

            //update
            Utils.DoParallel("Update", DirsToUpdate.ToArray(), dir => Utils.Compress(dir.Value, Destination.LocatedIn, pass, aes_key_size, log), log, dir => dir.Key.Dir.RelativePath, dir =>
            {
                if (dir.Key.Archive != null) System.IO.File.Delete(dir.Key.Archive.GetAbsolutePath());
            });

            //move
            //log.WriteLine($"Moving {DirsToMove.Count} dirs");
            Utils.DoSingleThread("Move", DirsToMove.ToArray(), dir =>
            {
                var dest_dir = Utils.CreateDirInNewRoot(dir.Key, Destination.LocatedIn);
                if (dir.Value.Archive != null) System.IO.File.Move(dir.Value.Archive.GetAbsolutePath(), $"{dest_dir.Path}{dir.Value.Archive.Name}");
            }, log, dir => $"{dir.Value.Dir.RelativePath} to {dir.Key.RelativePath}");
            /*foreach (var dir in DirsToMove)
            {
                var dest_dir = Utils.CreateDirInNewRoot(dir.Key, Destination.LocatedIn);
                log.OverWriteLastLine($"Moving dir {dir.Value.Dir.AbsolutePath} to {dest_dir.Path}");
                if (dir.Value.Archive != null) System.IO.File.Move(dir.Value.Archive.GetAbsolutePath(), $"{dest_dir.Path}{dir.Value.Archive.Name}");
            }*/

            //delete
            //log.WriteLine($"Deleting {AllDirsToDelete.Count} dirs");
            Utils.DoSingleThread("Delete", TopDirsToDelete.ToArray(), dir => Directory.Delete(dir.Dir.AbsolutePath, true), log, dir => dir.Dir.RelativePath);
            /*foreach (var dir in TopDirsToDelete)
            {
                log.OverWriteLastLine($"Deleting dir {dir.Dir.AbsolutePath}");
                Directory.Delete(dir.Dir.AbsolutePath, true);
            }*/
        }

        public static bool IsFileContentEqual(Dir dir, ArchivedDir archive)
        {
            if (dir.Files.Count != archive.ArchivedFiles.Count) return false;
            foreach (var file in dir.Files)
            {
                var found = false;
                foreach (var archived_file in archive.ArchivedFiles)
                {
                    if (file.Name == archived_file.Name)
                    {
                        found = true;
                        if (file.Size != archived_file.Size || !Utils.AreEqual(file.LastWriteTime, archived_file.LastWriteTime)) return false;
                        break;
                    }
                }
                if (!found) return false;
            }
            return true;
        }

        

    }

}