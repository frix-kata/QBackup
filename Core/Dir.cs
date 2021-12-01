using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using SymbolicLinkSupport;



namespace QBackup.Core
{

    public class Dir
    {

        #region Static

        internal static Dir BeginBuild(Serialized s)
        {
            var r = new Dir();
            r.Name = s.Name;
            r.Id = s.Id;
            r.IsLink = s.IsLink;
            r.IsIgnored = s.IsIgnored;
            r.Dirs = new List<Dir>();
            r.Files = new List<File>();
            return r;
        }

        public static Dir CreateFromPath(QualifiedPath path, int id, out QualifiedPath root_path)
        {
            var p = path.GetTrimmed();
            root_path = $"{Path.GetDirectoryName(p)}\\";
            var name = Path.GetFileName(p);
            return new Dir(name, id, root_path);
        }

        internal static void FinishBuild(Dir d, Dir parent, List<Dir> dirs, List<File> files)
        {
            d.Parent = parent;
            d.Dirs = dirs;
            d.Files = files;
        }

        internal static void FinishBuild2(Dir d, string root_path)
        {
            d.AbsolutePath = $"{root_path}{d.RelativePath}";
        }

        #endregion

        #region Fields

        public string Name;
        public int Id;
        public Dir Parent;

        public List<File> Files;
        public List<Dir> Dirs;

        /// <summary>
        ///     True if dir is symlink, junction or mount point.
        /// </summary>
        public bool IsLink;

        /// <summary>
        ///     Ignore this dir for backup
        /// </summary>
        public bool IsIgnored;

        private string _relative_path;

        #endregion

        #region Properties

        public string AbsolutePath { get; private set; }

        public string RelativePath
        {
            get
            {
                if (_relative_path == null) _relative_path = $"{(Parent != null ? Parent.RelativePath : "")}{Name}\\";
                return _relative_path;
            }
        }

        #endregion

        #region Constructors

        public Dir(string name, int id, Dir parent)
        {
            Name = name;
            Id = id;
            Parent = parent;
            Files = new List<File>();
            Dirs = new List<Dir>();
            AbsolutePath = $"{Parent.AbsolutePath}{Name}\\";
        }

        /// <summary>
        ///     Constructor for root dir.
        /// </summary>
        /// <param name="name"></param>
        /// <param name="id"></param>
        /// <param name="path"></param>
        public Dir(string name, int id, QualifiedPath path)
        {
            Name = name;
            Id = id;
            Parent = null;
            Files = new List<File>();
            Dirs = new List<Dir>();
            AbsolutePath = $"{path.Path}{Name}\\";
        }

        private Dir()
        {
        }

        #endregion

        #region Methods

        [ThreadSafe]
        public string AppendToAbsolutePath(string name)
        {
            return $"{AbsolutePath}{name}";
        }

        [ThreadSafe]
        public string AppendToRelativePath(string name)
        {
            return $"{RelativePath}{name}";
        }

        public void CheckIfLink()
        {
            var info = new DirectoryInfo(AbsolutePath);
            if ((info.Attributes & FileAttributes.ReparsePoint) == FileAttributes.ReparsePoint)
            {
                if (SymbolicLink.IsLink(AbsolutePath)) IsLink = true;
                else throw new NotImplementedException();
            }
        }

        public IEnumerable<Dir> GetAllDescendants()
        {
            foreach (var dir in Dirs) yield return dir;
            foreach (var dir in Dirs)
            {
                foreach (var descendant in dir.GetAllDescendants()) yield return descendant;
            }
        }

        public IEnumerable<Dir> GetThisWithAllDescendants()
        {
            yield return this;
            foreach (var descendant in GetAllDescendants()) yield return descendant;
        }

        public Serialized Serialize()
        {
            return new Serialized
            {
                Name = Name,
                Id = Id,
                Parent = Parent != null ? Parent.Id : -1,
                IsLink = IsLink,
                IsIgnored = IsIgnored,
                Files = Files.Select(x => x.Id).ToArray(),
                Dirs = Dirs.Select(x => x.Id).ToArray()
            };
        }

        #endregion

        /// <inheritdoc />
        public override string ToString()
        {
            return RelativePath;
        }



        public class Serialized
        {

            #region Fields

            public string Name;
            public int Id;
            public int Parent;
            public bool IsLink;
            public bool IsIgnored;

            public int[] Files;
            public int[] Dirs;

            #endregion

            /// <inheritdoc />
            public override string ToString()
            {
                return $"{Id}, {Name}";
            }

        }

    }

}