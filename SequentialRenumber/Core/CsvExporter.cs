using System.Text;

namespace SequentialRenumber.Core
{
    /// <summary>
    /// Builds the report CSV by hand (no third-party dependencies — spec section 3.6).
    /// Fields containing commas, quotes, or line breaks are quoted and escaped so the file
    /// opens correctly in Excel.
    /// </summary>
    public static class CsvExporter
    {
        /// <summary>The full grid as CSV text, one line per record, header first.</summary>
        public static string Build(IEnumerable<RenameRecord> records)
        {
            var sb = new StringBuilder();
            sb.AppendLine("Run,Status,Parameter,Old Value,New Value,Category,Element Id,Time,Note");

            foreach (RenameRecord record in records)
            {
                sb.AppendLine(string.Join(",",
                    record.RunNumber.ToString(),
                    record.Status.ToString(),
                    Escape(record.ParameterName),
                    Escape(record.OldValue),
                    Escape(record.NewValue),
                    Escape(record.CategoryName),
                    record.ElementIdValue.ToString(),
                    Escape(record.TimestampLocal.ToString("yyyy-MM-dd HH:mm:ss")),
                    Escape(record.Note)));
            }

            return sb.ToString();
        }

        private static string Escape(string value)
        {
            if (string.IsNullOrEmpty(value)) return string.Empty;

            bool needsQuotes = value.IndexOfAny(new[] { ',', '"', '\r', '\n' }) >= 0;
            return needsQuotes ? $"\"{value.Replace("\"", "\"\"")}\"" : value;
        }
    }
}
