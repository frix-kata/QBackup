namespace QBackup
{

    public interface ILog
    {

        void WriteLine(string line);

        void OverWriteLastLine(string line);

        void WriteError(string line);

        string[] GetErrors();

        string[] GetLog();

        void Reset();

        void StartTimer();

        void StopTimer();

    }

}