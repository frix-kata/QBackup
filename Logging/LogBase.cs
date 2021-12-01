using System.Diagnostics;
using System.Linq;



namespace QBackup.Logging
{

    /// <summary>
    ///     Base logger class.
    /// </summary>
    public abstract class LogBase : ILog
    {

        #region Fields

        protected readonly object Lock = new object();

        private Stopwatch _sw;

        private bool _timer_running;

        #endregion

        #region Abstract

        protected abstract void WriteLineConcrete(string line);

        protected abstract void OverWriteLastLineConcrete(string line);

        #endregion

        #region Implementation of ILog

        /// <inheritdoc />
        public void WriteLine(string line)
        {
            WriteLine("", line);
        }

        public void OverWriteLastLine(string line)
        {
            lock (Lock)
                if (_timer_running)
                    line = $"{_sw.Elapsed:g} {line}";
            OverWriteLastLineConcrete(line);
        }

        private void WriteLine(string prefix, string line)
        {
            lock (Lock)
                if (_timer_running)
                    line = $"{prefix}{_sw.Elapsed:g} {line}";
            WriteLineConcrete(line);
        }

        public void WriteError(string line)
        {
            WriteLine("ERROR: ", line);
        }

        public string[] GetErrors()
        {
            return GetLog().Where(x => x.StartsWith("ERROR: ")).ToArray();
        }

        /// <inheritdoc />
        public abstract string[] GetLog();

        /// <inheritdoc />
        public abstract void Reset();

        /// <inheritdoc />
        public void StartTimer()
        {
            lock (Lock)
            {
                if (_sw == null)
                {
                    _sw = new Stopwatch();
                    _sw.Start();
                }
                else _sw.Restart();

                _timer_running = true;
            }
        }

        /// <inheritdoc />
        public void StopTimer()
        {
            lock (Lock)
            {
                _sw?.Stop();
                _timer_running = false;
            }
        }

        #endregion

    }

}