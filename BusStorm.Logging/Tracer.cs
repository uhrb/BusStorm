using System;
using System.Threading;

namespace BusStorm.Logging
{
    public static class Tracer
    {
        public static void Log(string format)
        {
            Console.WriteLine("{0}-{1}",Thread.CurrentThread.ManagedThreadId, format);
        }

        // ReSharper disable MethodOverloadWithOptionalParameter
        public static void Log(string format, params object[] items)
        // ReSharper restore MethodOverloadWithOptionalParameter
        {
            Log(string.Format(format,items));
        }
    }
}
