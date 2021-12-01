using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using QBackup;
using File = System.IO.File;



namespace QBackupTests
{

    public class TestDir
    {

        public QualifiedPath Root;
        public string Name;
        public List<QualifiedPath> Dirs;
        public List<string> Files;

        public TestDir(QualifiedPath root)
        {
            Root = root;
            Dirs = new List<QualifiedPath>();
            Files = new List<string>();
            Name = "TestDir";
        }

        public bool ContainsDir(QualifiedPath q)
        {
            return Dirs.Any(x => x.Path == q.Path);
        }

        public bool ContainsFile(string f)
        {
            return Files.Any(x => x == f);
        }

        public void Delete()
        {
            var d = CreateDir(Root, Name);
            if (Directory.Exists(d.Path))
            {
                Directory.Delete(d.Path, true);
            }
            Dirs.Clear();
            Files.Clear();
        }

        public void Create()
        {
            Delete();
            var d = CreateDir(Root, Name);
            AddRuleFile(d, 0, new Match()
            {
                Pattern = @".+\.ignored",
                Recursive = true,
                Regex = true,
                Type = Match.MATCH_NAME
            });
            for (int i = 0; i < 5; i++)
            {
                CreateFile(d, $"file{i}.txt", $"Foobar {i}");
            }

            for (int i = 0; i < 5; i++)
            {
                var f = CreateDir(d, $"folder {i}");
                var empty_dir = CreateDir(f, "empty");
                for (int j = 0; j < 5; j++)
                {
                    var n = i * 10 + j;
                    AddIgnored(f, n);
                    var f1 = CreateDir(f, $"dir {n}");
                    AddRuleFile(f1, n, new Match()
                    {
                        Pattern = "ignored dir",
                        Recursive = false,
                        Regex = false,
                        Type = Match.MATCH_NAME
                    });
                    var fi = CreateDir(f1, "ignored dir", false);
                    CreateFile(fi, "file.txt", "This file is in ignored dir", false);
                    var fin = CreateDir(f1, "ignoredd dir");
                    CreateFile(fin, "file.txt", "This file is not in ignored dir");
                    var fi_fin = CreateDir(fin, "ignored dir");
                    CreateFile(fi_fin, "file.txt", "This file is not in ignored dir");
                    AddIgnored(fi_fin, n);

                    var normal = CreateDir(f1, "normal dir");
                    CreateFile(normal, "file1.txt", "This file is not in ignored dir");
                    CreateFile(normal, "file2.txt", "This file is not in ignored dir");
                    var normal1 = CreateDir(normal, "normal dir");
                    CreateFile(normal1, "file1.txt", "This file is not in ignored dir");
                    CreateFile(normal1, "file2.txt", "This file is not in ignored dir");
                }
            }

            
            for (int i = 0; i < 2; i++)
            {
                var d1 = CreateDir(d, $"dir_{i}");
                AddRuleFile(d1, i, new Match()
                {
                    Recursive = true,
                    Pattern = $@"dir_{i}\\file_{i}\.txt$",
                    Regex = true,
                    Type = Match.MATCH_ABSOLUTE_PATH
                });
                CreateFile(d1, $"file_{i}.txt", "Some file", false);
                CreateFile(d1, $"file_{i}.txt1", "Some file");
            }
            var d2 = CreateDir(d, $"dir_2");
            var d3 = CreateDir(d2, $"dir_1");
            CreateFile(d3, $"file_1.txt", "Some file");
        }

        private void AddRuleFile(QualifiedPath d, int i, params Match[] rules)
        {
            var json = JsonConvert.SerializeObject(rules.ToList());
            CreateFile(d, $"rules{i}{Constants.IGNORE_FILE_EXT}", json, true);
        }

        private void AddIgnored(QualifiedPath dir, int i)
        {
            CreateFile(dir, $"ignore file {i}.ignored", "This file should be ignored by backup.", false);
        }

        private QualifiedPath CreateDir(QualifiedPath path, string name, bool include = true)
        {
            var dir = new QualifiedPath($"{path.Path}{name}");
            Directory.CreateDirectory(dir.Path);
            if (include) Dirs.Add(dir);
            return dir;
        }

        private void CreateFile(QualifiedPath path, string name, string contents, bool include = true)
        {
            var file = $"{path.Path}{name}";
            File.WriteAllText(file, contents);
            if (include) Files.Add(file);
        }

    }

}