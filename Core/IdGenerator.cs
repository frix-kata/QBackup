using System.Threading;



namespace QBackup.Core
{

    public static class IdGenerator
    {

        #region Static

        private static int _id_counter;

        [ThreadSafe]
        public static int GenerateId()
        {
            return Interlocked.Increment(ref _id_counter);
        }

        #endregion

    }

}