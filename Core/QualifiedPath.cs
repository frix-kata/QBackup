namespace QBackup.Core
{

    /// <summary>
    ///     Path that always ends with backslash \.
    /// </summary>
    public readonly struct QualifiedPath
    {

        #region Static

        public static implicit operator QualifiedPath(string s) => new QualifiedPath(s);
        public static implicit operator string(QualifiedPath s) => s.Path;

        #endregion

        #region Fields

        public readonly string Path;

        #endregion

        #region Constructors

        public QualifiedPath(string path)
        {
            if (!path.EndsWith("\\")) path = path + "\\";
            Path = path;
        }

        #endregion

        #region Methods

        /// <summary>
        ///     Get path without backslash in the end.
        /// </summary>
        /// <returns></returns>
        public string GetTrimmed() => Path.TrimEnd('\\');

        #endregion

        /// <inheritdoc />
        public override string ToString()
        {
            return Path;
        }

    }

}