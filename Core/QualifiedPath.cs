namespace QBackup
{

    /// <summary>
    /// Path that always ends with backslash \.
    /// </summary>
    public readonly struct QualifiedPath
    {

        public readonly string Path;

        public QualifiedPath(string path)
        {
            if (!path.EndsWith("\\")) path = path + "\\";
            Path = path;
        }

        /// <summary>
        /// Get path without backslash in the end.
        /// </summary>
        /// <returns></returns>
        public string GetTrimmed() => Path.TrimEnd('\\');

        /// <inheritdoc />
        public override string ToString()
        {
            return Path;
        }

        public static implicit operator QualifiedPath (string s) => new QualifiedPath(s);
        public static implicit operator string (QualifiedPath s) => s.Path;

    }

}