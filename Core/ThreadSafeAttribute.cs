using System;



namespace QBackup.Core
{

    [AttributeUsage(AttributeTargets.Method | AttributeTargets.Constructor)]
    public class ThreadSafeAttribute : Attribute
    {

    }

}