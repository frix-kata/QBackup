using System;



namespace QBackup
{

    [AttributeUsage(AttributeTargets.Method | AttributeTargets.Constructor)]
    public class ThreadSafeAttribute : Attribute
    {

    }

}