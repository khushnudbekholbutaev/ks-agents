using Common.Interfaces;
using Common.Models;
using System;

namespace Common.Helpers
{
    public class Logger : ILogger
    {
        public void LogInformation(string message)
        {
            WriteLog("INFO", message);
        }

        public void LogError(string message)
        {
            WriteLog("ERROR", message);
        }

        private void WriteLog(string level, string message)
        {
            try
            {
                var logEntry = new LoggerEntry
                {
                    LogLevel = level,
                    Message = message,
                    Timestamp = DateTime.Now
                };
                DBContexts.Insert(logEntry);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[LOG ERROR] {ex.Message}");
                Console.WriteLine($"[{level}] {message}");
            }
        }
    }
}
