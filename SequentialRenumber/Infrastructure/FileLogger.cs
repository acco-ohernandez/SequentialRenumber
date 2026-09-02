using System.Text;

namespace SequentialRenumber.Infrastructure
{
    /// <summary>
    /// Plain-text daily log file under <c>%LOCALAPPDATA%\ACCO\SequentialRenumber\logs</c>.
    /// A logging failure must never break the tool, so every write swallows its own exceptions.
    /// </summary>
    internal static class FileLogger
    {
        private static readonly object _lock = new object();

        private static string LogDirectory =>
            Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "ACCO", "SequentialRenumber", "logs");

        private static string LogFilePath =>
            Path.Combine(LogDirectory, $"SequentialRenumber_{DateTime.Now:yyyy-MM-dd}.log");

        /// <summary>Writes an informational line.</summary>
        public static void Info(string message) => WriteLine("INFO", message);

        /// <summary>Writes a warning line (rollovers, skips, duplicates).</summary>
        public static void Warn(string message) => WriteLine("WARN", message);

        /// <summary>Writes an error line, appending the full exception when one is supplied.</summary>
        public static void Error(string message, Exception ex = null)
        {
            var sb = new StringBuilder(message);
            if (ex != null)
            {
                sb.AppendLine().Append(ex);
            }
            WriteLine("ERROR", sb.ToString());
        }

        private static void WriteLine(string level, string message)
        {
            try
            {
                lock (_lock)
                {
                    Directory.CreateDirectory(LogDirectory);
                    File.AppendAllText(
                        LogFilePath,
                        $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} {level,-5} {message}{Environment.NewLine}");
                }
            }
            catch
            {
                // Never let logging take down the tool.
            }
        }
    }
}
