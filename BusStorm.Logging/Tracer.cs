using System;
using System.Configuration;
using System.IO;
using System.Linq;
using System.Threading;

namespace BusStorm.Logging
{
  public static class Tracer
  {
    private static readonly bool IsLoggingEnabled;
    private const string IsLoggingEnabledKey = "isLoggingEnabled";
    private const string TargerKey = "loggingTarget";
    private const string FileNameKey = "loggingFileName";
    private static readonly FileStream FileStream;
    private static readonly LoggingDelegate LoggingEngine;
    
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
          FileStream = File.Open(ConfigurationManager.AppSettings[FileNameKey], FileMode.Append, FileAccess.Write, FileShare.ReadWrite);
          LoggingEngine = Log; // TODO Implement logging to file engine
          AppDomain.CurrentDomain.ProcessExit += CurrentDomain_ProcessExit;
          break;
        default:
          LoggingEngine = Console.WriteLine;
          break;
      }
    }

    private delegate void LoggingDelegate(string message);

    public static void Log(string format)
    {
      if (!IsLoggingEnabled || LoggingEngine == null)
      {
        return;
      }

      LoggingEngine(string.Format("{0}-{1}", Thread.CurrentThread.ManagedThreadId, format));
    }

    public static void Log(string format, params object[] items)
    {
      Log(string.Format(format, items));
    }

    private static void CurrentDomain_ProcessExit(object sender, EventArgs e)
    {
      if (FileStream != null)
      {
        FileStream.Close();
      }
    }
  }
}
