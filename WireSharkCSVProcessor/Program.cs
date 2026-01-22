using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace WireSharkCSVProcessor
{
    internal class Program
    {
        const string inputFile = "Input.csv";
        const string outputFile = "Result.csv";

        static int Main(string[] args)
        {
            var serviceFolder = AppContext.BaseDirectory;

            string inputFilePath = Path.Combine(serviceFolder, inputFile);
            string outputFilePath = Path.Combine(serviceFolder, outputFile);



            if (!File.Exists(inputFilePath))
            {
                Console.Error.WriteLine($"找不到檔案：{inputFilePath}");
                return 1;
            }

            var lines = File.ReadAllLines(inputFilePath);
            if (lines.Length == 0)
            {
                Console.Error.WriteLine("Input.csv 是空的。");
                return 1;
            }

            // Parse header row
            var headerCols = ParseCsvLine(lines[0]);
            int payloadIdx = FindColumnIndex(headerCols, "Payload");
            if (payloadIdx < 0)
            {
                Console.Error.WriteLine("找不到欄位：Payload");
                return 1;
            }

            // Output header
            var outHeader = new List<string>(headerCols)
            {
                "CmdType",
                "SeqNo",
                "Header",
                "Body"
            };

            using var sw = new StreamWriter(outputFilePath, false, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
            sw.WriteLine(string.Join(",", outHeader.Select(CsvEscape)));

            // Process rows
            for (int i = 1; i < lines.Length; i++)
            {
                if (string.IsNullOrWhiteSpace(lines[i])) continue;

                var cols = ParseCsvLine(lines[i]);

                // Make sure column count matches header count (pad if needed)
                while (cols.Count < headerCols.Count) cols.Add("");
                if (cols.Count > headerCols.Count)
                {
                    // If a row has extra columns, keep them (rare) but align payload index based on header
                    // We'll still write all original cols.
                }

                string payload = payloadIdx < cols.Count ? cols[payloadIdx] : "";
                var bytes = ParseHexPayload(payload);

                var headerBytes = bytes.Take(12).ToArray();
                var bodyBytes = bytes.Skip(12).ToArray();

                string headerStr = string.Join(" ", headerBytes.Select(b => b.ToString("x2")));
                string seqNoStr = "";
                if (headerBytes.Length >= 2)
                {
                    ushort seqNo = (ushort)(headerBytes[^2] | (headerBytes[^1] << 8));
                    seqNoStr = seqNo.ToString(); // 轉成十進位數字
                }

                string cmdTypeStr = bodyBytes.Length >= 2
                    ? Encoding.ASCII.GetString(bodyBytes.Take(2).ToArray())
                    : "";

                string bodyAscii = BytesToSafeAscii(bodyBytes);

                var outRow = new List<string>(cols)
                {
                    cmdTypeStr,
                    seqNoStr,
                    headerStr,
                    bodyAscii
                };

                sw.WriteLine(string.Join(",", outRow.Select(CsvEscape)));
            }

            Console.WriteLine($"完成：{outputFile}");
            return 0;
        }

        static int FindColumnIndex(List<string> headers, string name)
        {
            for (int i = 0; i < headers.Count; i++)
            {
                if (string.Equals(headers[i]?.Trim(), name, StringComparison.OrdinalIgnoreCase))
                    return i;
            }
            return -1;
        }

        // --- Payload parsing ---
        static byte[] ParseHexPayload(string payload)
        {
            if (string.IsNullOrWhiteSpace(payload))
                return Array.Empty<byte>();

            // Example: "c5:6d:00:00:2b:00:..."
            var parts = payload.Split(':', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

            var bytes = new List<byte>(parts.Length);
            foreach (var p in parts)
            {
                // tolerate accidental spaces
                var s = p.Trim();
                if (s.Length == 0) continue;

                // Some CSVs might contain "0x.." style; handle it
                if (s.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
                    s = s.Substring(2);

                if (byte.TryParse(s, System.Globalization.NumberStyles.HexNumber, null, out var b))
                    bytes.Add(b);
                else
                    bytes.Add(0); // if malformed, keep output stable
            }

            return bytes.ToArray();
        }

        // Convert to ASCII, replacing non-printable chars with '.'
        static string BytesToSafeAscii(byte[] data)
        {
            if (data == null || data.Length == 0) return "";

            var sb = new StringBuilder(data.Length);
            foreach (var b in data)
            {
                char c = (char)b;
                // allow common printable ASCII + space (0x20..0x7E)
                if (b >= 0x20 && b <= 0x7E)
                    sb.Append(c);
                else
                    sb.Append('.');
            }
            return sb.ToString();
        }

        // --- CSV helpers (no external package) ---
        static List<string> ParseCsvLine(string line)
        {
            var result = new List<string>();
            if (line == null) return result;

            var sb = new StringBuilder();
            bool inQuotes = false;

            for (int i = 0; i < line.Length; i++)
            {
                char ch = line[i];

                if (inQuotes)
                {
                    if (ch == '"')
                    {
                        // double-quote escape
                        if (i + 1 < line.Length && line[i + 1] == '"')
                        {
                            sb.Append('"');
                            i++;
                        }
                        else
                        {
                            inQuotes = false;
                        }
                    }
                    else
                    {
                        sb.Append(ch);
                    }
                }
                else
                {
                    if (ch == '"')
                    {
                        inQuotes = true;
                    }
                    else if (ch == ',')
                    {
                        result.Add(sb.ToString());
                        sb.Clear();
                    }
                    else
                    {
                        sb.Append(ch);
                    }
                }
            }

            result.Add(sb.ToString());
            return result;
        }

        static string CsvEscape(string s)
        {
            s ??= "";
            bool mustQuote = s.Contains(',') || s.Contains('"') || s.Contains('\n') || s.Contains('\r');
            if (!mustQuote) return s;

            return "\"" + s.Replace("\"", "\"\"") + "\"";
        }
    }
}
