using System.Threading;



namespace QBackup
{

    public static class IdGenerator
    {

        private static int _id_counter;

        [ThreadSafe]
        public static int GenerateId()
        {
            return Interlocked.Increment(ref _id_counter);
        }

    }

}