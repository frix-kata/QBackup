using System;



namespace QBackup
{


    public class Match
    {

        public bool Recursive;
        public bool Regex;
        public string Type;
        public string Pattern;

        public bool IsMatch(string s)
        {
            if (Regex) return System.Text.RegularExpressions.Regex.IsMatch(s, Pattern);
            return s == Pattern;
        }

        public const string MATCH_NAME = "name";
        public const string MATCH_RELATIVE_PATH = "relative path";
        public const string MATCH_ABSOLUTE_PATH = "absolute path";


        public bool IsMatch(File f)
        {
            switch(Type)
            {
                case MATCH_NAME: return IsMatch(f.Name);
                case MATCH_RELATIVE_PATH: return IsMatch(f.GetRelativePath());
                case MATCH_ABSOLUTE_PATH: return IsMatch(f.GetAbsolutePath());
                default:
                    throw new InvalidOperationException($"Unrecognized {nameof(Type)} {Type} in {nameof(Match)}, expected 'name', 'relative path' or 'absolute path'");
            }
        }

        public bool IsMatch(Dir d)
        {
            switch (Type)
            {
                case "name": return IsMatch(d.Name);
                case "relative path": return IsMatch(d.RelativePath);
                case "absolute path": return IsMatch(d.AbsolutePath);
                default:
                    throw new InvalidOperationException($"Unrecognized {nameof(Type)} {Type} in {nameof(Match)}, expected 'name', 'relative path' or 'absolute path'");
            }
        }

    }

}