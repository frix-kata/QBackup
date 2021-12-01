using System.Collections.Generic;



namespace QBackup.Logging
{

    public class LogLines : LogBase
    {

        #region Fields

        private readonly List<string> _lines = new List<string>();

        #endregion

        #region Overrides

        /// <inheritdoc />
        public override string[] GetLog()
        {
            lock (Lock) return _lines.ToArray();
        }

        /// <inheritdoc />
        public override void Reset()
        {
            lock (Lock) _lines.Clear();
        }

        /// <inheritdoc />
        protected override void WriteLineConcrete(string line)
        {
            lock (Lock) _lines.Add(line);
        }

        /// <inheritdoc />
        protected override void OverWriteLastLineConcrete(string line)
        {
            /*lock (_lock)
            {
                if (_lines.Count == 0) _lines.Add(line);
                else _lines[_lines.Count - 1] = line;
            }*/
        }

        #endregion

    }

}