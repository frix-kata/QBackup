using System;
using System.Collections.Generic;



namespace QBackup
{

    public class LogConsole : LogBase
    {

        private string _last_line = "";
        private bool _inside_overwrite_line_block;

        /// <inheritdoc />
        protected override void WriteLineConcrete(string line)
        {
            lock (Lock)
            {
                if (_inside_overwrite_line_block) Console.WriteLine("");
                _inside_overwrite_line_block = false;
                Console.WriteLine(line);
                _last_line = line;
            }
        }

        /// <inheritdoc />
        protected override void OverWriteLastLineConcrete(string line)
        {
            //todo make this thread safe without locking
            lock (Lock)
            {
                if (!_inside_overwrite_line_block)
                {
                    Console.WriteLine("");
                    _last_line = "";
                }
                _inside_overwrite_line_block = true;
                var write = $"\r{line}{(line.Length < _last_line.Length ? GetEmptySpace(_last_line.Length - line.Length) : "")}";
                Console.Write(write);
                _last_line = line;
            }
        }

        /// <inheritdoc />
        public override string[] GetLog()
        {
            throw new NotImplementedException();
        }

        /// <inheritdoc />
        public override void Reset()
        {
            lock (Lock)
            {
                _inside_overwrite_line_block = false;
            }
        }

        private readonly Dictionary<int, string> _empty_spaces = new Dictionary<int, string>();
        private string GetEmptySpace(int l)
        {
            if (!_empty_spaces.ContainsKey(l))
            {
                var chars = new char[l];
                for (int i = 0; i < l; i++)
                {
                    chars[i] = ' ';
                }
                _empty_spaces[l] = new string(chars);
            }
            return _empty_spaces[l];
        }

    }

}