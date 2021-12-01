using System;
using System.IO;
using ICSharpCode.SharpZipLib.Zip;



namespace QBackup.Core
{

    public class File
    {

        #region Static

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

        [ThreadSafe]
        public static File Create(string name, Dir parent_dir)
        {
            var res = new File(IdGenerator.GenerateId(), name, parent_dir);
            return res;
        }

        [ThreadSafe]
        public static File Create(ZipEntry entry, Dir parent_dir)
        {
            var res = new File(IdGenerator.GenerateId(), entry.Name, parent_dir);
            res.Size = entry.Size;
            res.LastWriteTime = entry.DateTime;
            return res;
        }

        internal static void FinishBuild(File file, Dir parent)
        {
            file.Parent = parent;
        }

        #endregion

        #region Fields

        public int Id;
        public string Name;
        public Dir Parent;
        public long Size;
        public DateTime LastWriteTime;

        /// <summary>
        ///     Ignore this file for backup
        /// </summary>
        public bool IsIgnored;

        #endregion

        #region Constructors

        [ThreadSafe]
        public File(int id, string name, Dir parent)
        {
            Id = id;
            Name = name;
            Parent = parent;
        }


        private File()
        {
        }

        #endregion

        #region Methods

        [ThreadSafe]
        public string GetAbsolutePath()
        {
            return Parent.AppendToAbsolutePath(Name);
        }

        public string GetRelativePath()
        {
            return Parent.AppendToRelativePath(Name);
        }

        public Serialized Serialize()
        {
            return new Serialized
            {
                Id = Id,
                Name = Name,
                Parent = Parent.Id,
                Size = Size,
                LastWriteTime = LastWriteTime,
                IsIgnored = IsIgnored
            };
        }

        [ThreadSafe]
        public void WriteFileInfo()
        {
            var fi = new FileInfo(GetAbsolutePath());
            Size = fi.Length;
            LastWriteTime = fi.LastWriteTime;
        }

        #endregion

        /// <inheritdoc />
        public override string ToString()
        {
            return Name;
        }



        public class Serialized
        {

            #region Fields

            public int Id;
            public string Name;
            public int Parent;
            public long Size;
            public DateTime LastWriteTime;
            public bool IsIgnored;

            #endregion

            /// <inheritdoc />
            public override string ToString()
            {
                return $"{Id}, {Name}";
            }

        }

    }

}