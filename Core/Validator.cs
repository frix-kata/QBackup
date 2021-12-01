using System;
using System.Collections.Generic;
using System.Linq;



namespace QBackup
{

    public static class Validator
    {

        public static void Validate(File x1, File x2, ILog log, bool check_absolute = true, bool check_content = false, bool check_last_write_time = true)
        {
            if (x1 == null || x2 == null)
            {
                if (!(x1 == null && x2 == null))
                {
                    Write(x1, x2, "both supposed to be null", log);
                    return;
                }
            }
            Validate(x1, x2, x1.GetRelativePath(), x2.GetRelativePath(), nameof(x1.GetRelativePath), log);
            if (check_absolute) Validate(x1, x2, x1.GetAbsolutePath(), x2.GetAbsolutePath(), nameof(x1.GetAbsolutePath), log);
            Validate(x1, x2, x1.Name, x2.Name, nameof(x1.Name), log);
            Validate(x1, x2, x1.Size, x2.Size, nameof(x1.Size), log);
            if (check_last_write_time) Validate(x1, x2, x1.LastWriteTime, x2.LastWriteTime, nameof(x1.LastWriteTime), log);
            if (check_content)
            {
                var c1 = System.IO.File.ReadAllText(x1.GetAbsolutePath());
                var c2 = System.IO.File.ReadAllText(x2.GetAbsolutePath());
                Validate(x1, x2, c1, c2, "content", log);
            }
        }

        private static void Validate(object o1, object o2, string x1, string x2, string msg, ILog log)
        {
            if (x1 != x2) Write(o1, o2, msg, log);
        }

        private static void Validate(object o1, object o2, bool x1, bool x2, string msg, ILog log)
        {
            if (x1 != x2) Write(o1, o2, msg, log);
        }

        public static void Validate(object o1, object o2, long x1, long x2, string msg, ILog log)
        {
            if (x1 != x2) Write(o1, o2, msg, log);
        }

        public static void Validate(object o1, object o2, int x1, int x2, string msg, ILog log)
        {
            if (x1 != x2) Write(o1, o2, msg, log);
        }

        public static void Validate(object o1, object o2, DateTime x1, DateTime x2, string msg, ILog log)
        {
            if (!Utils.AreEqual(x1, x2)) Write(o1, o2, msg, log);
        }

        public static void Write(object x1, object x2, string name, ILog log)
        {
            log.WriteLine($"{x1} vs {x2}, mismatch on {name}");
        }

        public static void Validate(Dir x1, Dir x2, ILog log, bool check_absolute = true, bool check_children = true, bool check_last_write_time = true)
        {
            if (check_absolute) Validate(x1, x2, x1.AbsolutePath, x2.AbsolutePath, nameof(x1.AbsolutePath), log);
            Validate(x1, x2, x1.RelativePath, x2.RelativePath, nameof(x1.RelativePath), log);
            Validate(x1, x2, x1.Name, x2.Name, nameof(x1.Name), log);
            Validate(x1, x2, x1.IsLink, x2.IsLink, nameof(x1.IsLink), log);

            if (check_children)
            {
                Validate(x1.Dirs, x2.Dirs, log, check_absolute, false);
                Validate(x1.Files, x2.Files, log, check_absolute, false, check_last_write_time);
            }
        }

        public static void Validate(Dir x1, ArchivedDir x2, ILog log, bool check_absolute = true, bool check_children = true, bool check_last_write_time = true)
        {
            if (check_absolute) Validate(x1, x2, x1.AbsolutePath, x2.Dir.AbsolutePath, nameof(x1.AbsolutePath), log);
            Validate(x1, x2, x1.RelativePath, x2.Dir.RelativePath, nameof(x1.RelativePath), log);
            Validate(x1, x2, x1.Name, x2.Dir.Name, nameof(x1.Name), log);
            Validate(x1, x2, x1.IsLink, x2.Dir.IsLink, nameof(x1.IsLink), log);
            if (check_children)
            {
                Validate(x1.Dirs, x2.Dir.Dirs, log, check_absolute, false);
                Validate(x1.Files, x2.ArchivedFiles, log, check_absolute, false, check_last_write_time);
            }
        }

        public static void Validate(List<Dir> dirs1, List<ArchivedDir> dirs2, ILog log, bool check_absolute = true, bool check_children = true, bool check_last_write_time = true)
        {
            var dirs = ValidateLists(dirs1, dirs2, dir => dir.RelativePath, dir => dir.Dir.RelativePath, log);
            foreach (var dir in dirs)
            {
                Validate(dir.Item1, dir.Item2, log, check_absolute, check_children, check_last_write_time);
            }
        }

        private static List<Tuple<T1, T2>> ValidateLists<T1, T2>(IList<T1> l1, IList<T2> l2, Func<T1, string> id_func1, Func<T2, string> id_func2, ILog log)
        {
            //Validate(l1, l2, l1.Count, l2.Count, "count", log);
            var d1 = l1.ToDictionary(id_func1);
            var d2 = l2.ToDictionary(id_func2);
            
            var not_found_in_both = d1.Keys.ToHashSet();
            not_found_in_both.SymmetricExceptWith(d2.Keys);
            foreach (var id in not_found_in_both)
            {
                log.WriteLine($"{id} not found");
            }
            var included_in_both = d1.Keys.ToHashSet();
            included_in_both.IntersectWith(d2.Keys);
            return included_in_both.Select(x => new Tuple<T1, T2>(d1[x], d2[x])).ToList();
        }

        public static void Validate(List<Dir> dirs1, List<Dir> dirs2, ILog log, bool check_absolute = true, bool check_children = true, bool check_last_write_time = true)
        {
            var dirs = ValidateLists(dirs1, dirs2, dir => dir.RelativePath, dir => dir.RelativePath, log);
            foreach (var dir in dirs)
            {
                Validate(dir.Item1, dir.Item2, log, check_absolute, check_children, check_last_write_time);
            }
        }

        public static void Validate(AnalyzedBackup backup1, ArchivedBackup archived, ILog log, bool check_absolute_paths, bool check_file_contents, bool check_last_write_time)
        {
            log.WriteLine($"Validating backup {backup1.Root.Path} against archive {archived.Root.Path}");
            Validate(backup1.Dirs, archived.ArchivedDirs, log, check_absolute_paths, false, check_last_write_time);
            Validate(backup1.Files, archived.ArchivedDirs.SelectMany(x => x.ArchivedFiles).ToList(), log, check_absolute_paths, check_file_contents, check_last_write_time);
        }

        public static void Validate(AnalyzedBackup backup1, AnalyzedBackup backup2, ILog log, bool check_absolute_paths, bool check_file_contents, bool check_last_write_time)
        {
            log.WriteLine($"Validating backup {backup1.Root.Path} against backup {backup2.Root.Path}");
            Validate(backup1.Dirs, backup2.Dirs, log, check_absolute_paths, true, check_last_write_time);
            Validate(backup1.Files, backup2.Files, log, check_absolute_paths, check_file_contents, check_last_write_time);
        }

        public static void Validate(List<File> files1, List<File> files2, ILog log, bool check_absolute = true, bool check_content = false, bool check_last_write_time = true)
        {
            var files = ValidateLists(files1, files2, file => file.GetRelativePath(), file => file.GetRelativePath(), log);
            foreach (var file in files)
            {
                Validate(file.Item1, file.Item2, log, check_absolute, check_content, check_last_write_time);
            }
        }

    }

}