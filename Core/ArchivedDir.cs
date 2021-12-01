using System;
using System.Collections.Generic;
using System.Linq;
using QBackup.Logging;



namespace QBackup.Core
{


    /// <summary>
    /// Objects that represents one of the directories in the main backup after it has been archived and backed up.
    /// </summary>
    public class ArchivedDir
    {

        #region Properties

        public List<File> ArchivedFiles { get; }
        public File Archive { get; }
        public bool IsEmpty => Archive == null;

        public Dir Dir { get; }

        #endregion

        #region Constructors

        public ArchivedDir(Serialized s, Dictionary<int, Dir> dirs, Dictionary<int, File> files)
        {
            Dir = dirs[s.Dir];
            Archive = s.Archive != -1 ? files[s.Archive] : null;
            ArchivedFiles = s.ArchivedFiles.Select(x => files[x]).ToList();
        }

        public ArchivedDir(Dir dir)
        {
            Dir = dir;
            ArchivedFiles = new List<File>();
            if (Dir.Files.Count == 0) return;
            if (Dir.Files.Count != 1) throw new InvalidOperationException($"Dir {Dir.AbsolutePath} should have one zip file.");
            Archive = Dir.Files[0];
            if (Archive.Name != Constants.ARCHIVED_FILES_NAME)
                throw new InvalidOperationException(
                    $"File name of archived files should be {Constants.ARCHIVED_FILES_NAME} not {Archive.Name}");
        }

        #endregion

        #region Methods

        public void Analyze(string pass, ILog log)
        {
            if (Archive == null) return;
            ArchivedFiles.AddRange(Utils.ReadFilesFromArchive(Archive, Dir, pass, log));
        }

        public Serialized Serialize()
        {
            return new Serialized
            {
                ArchivedFiles = ArchivedFiles.Select(x => x.Id).ToArray(), Archive = Archive != null ? Archive.Id : -1, Dir = Dir.Id
            };
        }

        #endregion

        /// <inheritdoc />
        public override string ToString()
        {
            return Dir.RelativePath;
        }



        public class Serialized
        {

            #region Fields

            public int[] ArchivedFiles;
            public int Archive;
            public int Dir;

            #endregion

        }

    }

}