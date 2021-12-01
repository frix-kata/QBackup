using System;
using System.IO;
using ICSharpCode.SharpZipLib.Zip;



namespace QBackup
{

    public class File
    {

        public class Serialized
        {

            public int Id;
            public string Name;
            public int Parent;
            public long Size;
            public DateTime LastWriteTime;
            public bool IsIgnored;

            /// <inheritdoc />
            public override string ToString()
            {
                return $"{Id}, {Name}";
            }

        }

        public Serialized Serialize()
        {
            return new Serialized()
            {
                Id = Id,
                Name =  Name,
                Parent = Parent.Id,
                Size = Size,
                LastWriteTime = LastWriteTime,
                IsIgnored =  IsIgnored
            };
        }

        public int Id;
        public string Name;
        public Dir Parent;
        public long Size;
        public DateTime LastWriteTime;
        /// <summary>
        /// Ignore this file for backup
        /// </summary>
        public bool IsIgnored;

        [ThreadSafe]
        public static File Create(string name, Dir parent_dir)
        {
            var res = new File(IdGenerator.GenerateId(), name, parent_dir);
            return res;
        }

        [ThreadSafe]
        public void WriteFileInfo()
        {
            var fi = new FileInfo(GetAbsolutePath());
            Size = fi.Length;
            LastWriteTime = fi.LastWriteTime;
        }

        [ThreadSafe]
        public static File Create(ZipEntry entry, Dir parent_dir)
        {
            var res = new File(IdGenerator.GenerateId(), entry.Name, parent_dir);
            res.Size = entry.Size;
            res.LastWriteTime = entry.DateTime;
            return res;
        }

        [ThreadSafe]
        public File(int id, string name, Dir parent)
        {
            Id = id;
            Name = name;
            Parent = parent;
        }

        internal static File BeginBuild(Serialized s)
        {
            var r = new File();
            r.Id = s.Id;
            r.Name = s.Name;
            r.Size = s.Size;
            r.LastWriteTime = s.LastWriteTime;
            r.IsIgnored = s.IsIgnored;
            return r;
        }

        internal static void FinishBuild(File file, Dir parent)
        {
            file.Parent = parent;
        }


        private File()
        {
        }

        /// <inheritdoc />
        public override string ToString()
        {
            return Name;
        }

        [ThreadSafe]
        public string GetAbsolutePath()
        {
            return Parent.AppendToAbsolutePath(Name);
        }

        public string GetRelativePath()
        {
            return Parent.AppendToRelativePath(Name);
        }

    }

}