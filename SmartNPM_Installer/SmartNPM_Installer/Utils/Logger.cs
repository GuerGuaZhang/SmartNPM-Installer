using System;
using System.IO;
using System.Threading;

namespace SmartNPM_Installer.Utils
{
    /// <summary>
    /// 日志服务
    /// </summary>
    public class Logger
    {
        private readonly string _logDirectory;
        private readonly string _currentLogFile;
        private readonly ReaderWriterLockSlim _fileLock = new ReaderWriterLockSlim();

        /// <summary>
        /// 日志级别
        /// </summary>
        public enum LogLevel
        {
            DEBUG,
            INFO,
            WARN,
            ERROR,
            FATAL
        }

        /// <summary>
        /// 初始化日志服务
        /// </summary>
        /// <param name="logDirectory">日志目录</param>
        public Logger(string? logDirectory = null)
        {
            _logDirectory = logDirectory ?? Path.Combine(AppContext.BaseDirectory, "sni-logs");
            Directory.CreateDirectory(_logDirectory);

            var today = DateTime.Now.ToString("yyyy-MM-dd");
            _currentLogFile = Path.Combine(_logDirectory, $"{today}.log");

            // 清理30天前的日志
            CleanupOldLogs();
        }

        /// <summary>
        /// 记录DEBUG日志
        /// </summary>
        public void LogDebug(string message)
        {
            Log(LogLevel.DEBUG, message);
        }

        /// <summary>
        /// 记录INFO日志
        /// </summary>
        public void LogInfo(string message)
        {
            Log(LogLevel.INFO, message);
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine(message);
            Console.ResetColor();
        }

        /// <summary>
        /// 记录WARN日志
        /// </summary>
        public void LogWarning(string message)
        {
            Log(LogLevel.WARN, message);
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine(message);
            Console.ResetColor();
        }

        /// <summary>
        /// 记录ERROR日志
        /// </summary>
        public void LogError(string message)
        {
            Log(LogLevel.ERROR, message);
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine(message);
            Console.ResetColor();
        }

        /// <summary>
        /// 记录FATAL日志
        /// </summary>
        public void LogFatal(string message)
        {
            Log(LogLevel.FATAL, message);
            Console.ForegroundColor = ConsoleColor.Red;
            Console.BackgroundColor = ConsoleColor.Black;
            Console.Write("FATAL: ");
            Console.WriteLine(message);
            Console.ResetColor();
        }

        /// <summary>
        /// 记录日志
        /// </summary>
        private void Log(LogLevel level, string message)
        {
            var timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
            var logEntry = $"[{timestamp}] [{level}] {message}";

            try
            {
                _fileLock.EnterWriteLock();
                File.AppendAllText(_currentLogFile, logEntry + Environment.NewLine);
            }
            catch
            {
                // 写日志失败时忽略错误
            }
            finally
            {
                if (_fileLock.IsWriteLockHeld)
                    _fileLock.ExitWriteLock();
            }
        }

        /// <summary>
        /// 清理旧日志文件
        /// </summary>
        private void CleanupOldLogs()
        {
            try
            {
                var cutoffDate = DateTime.Now.AddDays(-30);
                var logFiles = Directory.GetFiles(_logDirectory, "*.log");

                foreach (var logFile in logFiles)
                {
                    var fileInfo = new FileInfo(logFile);
                    if (fileInfo.LastWriteTime < cutoffDate)
                    {
                        fileInfo.Delete();
                    }
                }
            }
            catch
            {
                // 清理失败时忽略错误
            }
        }

        /// <summary>
        /// 获取当前日志文件路径
        /// </summary>
        public string GetCurrentLogFile()
        {
            return _currentLogFile;
        }
    }
}