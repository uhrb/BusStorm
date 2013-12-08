using System;
using System.Configuration;
using System.IO;
using System.Text;
using System.Threading;
using System.Linq;

namespace BusStorm.Logging
{
    public static class Tracer
    {
        private static readonly bool IsLoggingEnabled;

        private const string IsLoggingEnabledKey = "isLoggingEnabled";

        private const string TargerKey = "loggingTarget";

        private const string FileNameKey = "loggingFileName";

        private static readonly FileStream FileStream;

        static Tracer()
        {
            IsLoggingEnabled = false;
            if (!ConfigurationManager.AppSettings.AllKeys.Contains(IsLoggingEnabledKey))
            {
                return;
            }
            IsLoggingEnabled = Convert.ToBoolean(ConfigurationManager.AppSettings[IsLoggingEnabledKey]);
            if (!IsLoggingEnabled)
            {
                return;
            }
            var target = ConfigurationManager.AppSettings[TargerKey];
            switch (target)
            {
                case "file":
                    FileStream = File.Open(ConfigurationManager.AppSettings[FileNameKey], FileMode.Append,FileAccess.Write,FileShare.ReadWrite);
                    LoggingEngine = LogToFile;
                    AppDomain.CurrentDomain.ProcessExit += CurrentDomain_ProcessExit;
                    break;
                default:
                    LoggingEngine = Console.WriteLine;
                    break;
            }
            
        }

        private static void CurrentDomain_ProcessExit(object sender, EventArgs e)
        {
            if (FileStream != null)
            {
                FileStream.Close();
            }
        }

        private delegate void LoggingDelegate(string message);

        private static readonly LoggingDelegate LoggingEngine;

        private static void LogToFile(string message)
        {
            var bytes = Encoding.UTF8.GetBytes(message);
            FileStream.Write(bytes,0,bytes.Length);
            FileStream.Flush();
        }

        public static void Log(string format)
        {
            if (!IsLoggingEnabled || LoggingEngine == null)
            {
                return;
            }
            LoggingEngine(string.Format("{0}-{1}", Thread.CurrentThread.ManagedThreadId, format));
        }

        // ReSharper disable MethodOverloadWithOptionalParameter
        public static void Log(string format, params object[] items)
        // ReSharper restore MethodOverloadWithOptionalParameter
        {
            Log(string.Format(format,items));
        }
    }
}
