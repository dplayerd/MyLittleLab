using System.Globalization;
using System.Text;
using CsvHelper;
using CsvHelper.Configuration;
using RismLogProcessor.Models;


public sealed class CsvLogReader
{
    public List<LogRecord> Read(string filePath, string label)
    {
        var config = new CsvConfiguration(CultureInfo.InvariantCulture)
        {
            HasHeaderRecord = true,
            Encoding = Encoding.UTF8,
            DetectDelimiter = true,
            IgnoreBlankLines = false,

            // 關鍵：不要 Trim 欄位，保留 Payload 尾端空白
            TrimOptions = TrimOptions.None,

            // 避免因缺欄位/壞資料直接炸
            MissingFieldFound = null,
            BadDataFound = null,
            HeaderValidated = null
        };

        using var sr = new StreamReader(filePath, Encoding.UTF8);
        using var csv = new CsvReader(sr, config);

        var rows = new List<LogRecord>();

        csv.Read();
        csv.ReadHeader();

        while (csv.Read())
        {
            var r = new LogRecord
            {
                Label = label,
                Direction = csv.GetField("Direction") ?? "",
                TraceCode = csv.GetField("TraceCode") ?? "",
                DbId = csv.GetField("DbId") ?? "",
                HeaderHex = csv.GetField("HeaderHex") ?? "",
                Payload = csv.GetField("Payload") ?? ""  // 不做 Trim
            };

            var timeStr = csv.GetField("Time");
            if (!string.IsNullOrEmpty(timeStr) &&
                DateTime.TryParse(timeStr, CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal, out var dt))
                r.Time = dt;

            if (int.TryParse(csv.GetField("SeqNo"), out var seq)) r.SeqNo = seq;
            if (int.TryParse(csv.GetField("CmdNo"), out var cmd)) r.CmdNo = cmd;
            if (int.TryParse(csv.GetField("Attempt"), out var att)) r.Attempt = att;
            if (int.TryParse(csv.GetField("MaxRetry"), out var mr)) r.MaxRetry = mr;

            rows.Add(r);
        }

        return rows;
    }
}
