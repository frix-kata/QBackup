using System.Diagnostics;
using System.Linq;



namespace QBackup
{

    /// <summary>
    /// Base logger class.
    /// </summary>
    public abstract class LogBase : ILog
    {

        protected readonly object Lock = new object();

        /// <inheritdoc />
        public void WriteLine(string line)
        {
            lock (Lock)
            {
                if (_timer_running) line = $"{_sw.Elapsed:g} {line}";
            }
            WriteLineConcrete(line);
        }

        protected abstract void WriteLineConcrete(string line);

        protected abstract void OverWriteLastLineConcrete(string line);

        public void OverWriteLastLine(string line)
        {
            lock (Lock)
            {
                if (_timer_running) line = $"{_sw.Elapsed:g} {line}";
            }
            OverWriteLastLineConcrete(line);
        }

        public void WriteError(string line)
        {
            WriteLine("ERROR: " + line);
        }

        public string[] GetErrors()
        {
            return GetLog().Where(x => x.StartsWith("ERROR: ")).ToArray();
        }

        /// <inheritdoc />
        public abstract string[] GetLog();

        /// <inheritdoc />
        public abstract void Reset();

        private Stopwatch _sw;

        private bool _timer_running;

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

    }

}