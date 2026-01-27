using NPOI.SS.UserModel;
using NPOI.XSSF.UserModel;
using RismLogProcessor.ExcelHelpers;
using RismLogProcessor.Models;
using System.Globalization;

namespace RismLogProcessor.ExcelHelpers
{
    public sealed class ExcelReportWriter
    {
        public void Write(
            string outPath,
            List<LogRecord> sourceLog,
            List<LogRecord> only89,
            List<LogRecord> only76,
            List<LogRecord> onlyF6,
            List<LogRecord> only80)
        {
            IWorkbook wb = new XSSFWorkbook();

            // Result Sheet
            var resultWriter = new ResultSheetWriter();
            resultWriter.WriteResultSheet(wb, only89, only76, onlyF6);

            // Summary Sheet
            var summaryWriter = new SummarySheetWriter();
            summaryWriter.WriteSummarySheet(wb, only89, only76);

            WriteSheet(wb, "SourceLog", sourceLog, includeParsedColumns: true, includeOnly80PairHint: false);
            WriteSheet(wb, "Only89", only89, includeParsedColumns: true, includeOnly80PairHint: false);
            WriteSheet(wb, "Only76", only76, includeParsedColumns: true, includeOnly80PairHint: false);
            WriteSheet(wb, "OnlyF6", onlyF6, includeParsedColumns: false, includeOnly80PairHint: false);
            WriteSheet(wb, "Only80", only80, includeParsedColumns: false, includeOnly80PairHint: true);

            using var fs = File.Create(outPath);
            wb.Write(fs);
        }

        private static void WriteSheet(
            IWorkbook wb,
            string sheetName,
            List<LogRecord> rows,
            bool includeParsedColumns,
            bool includeOnly80PairHint)
        {
            var sheet = wb.CreateSheet(sheetName);

            var headers = new List<string>
            {
                "Label","Time","Direction","TraceCode","SeqNo","CmdNo","DbId","Attempt","MaxRetry","HeaderHex","Payload"
            };

            //if (includeParsedColumns)
            //{
            //    headers.AddRange(new[]
            //    {
            //        "PayloadPrefix","MachineNo","CardNo","Effect","SendTimeToken","MatchKey"
            //    });
            //}

            //if (includeOnly80PairHint)
            //{
            //    headers.Add("Only80_PairKey(SeqNo)");
            //}

            var headerRow = sheet.CreateRow(0);
            for (int i = 0; i < headers.Count; i++)
                headerRow.CreateCell(i).SetCellValue(headers[i]);

            for (int r = 0; r < rows.Count; r++)
            {
                var row = sheet.CreateRow(r + 1);
                int c = 0;

                Set(row, c++, rows[r].Label);
                Set(row, c++, rows[r].Time?.ToString("yyyy-MM-dd HH:mm:ss.fff", CultureInfo.InvariantCulture) ?? "");
                Set(row, c++, rows[r].Direction);
                Set(row, c++, rows[r].TraceCode);
                Set(row, c++, rows[r].SeqNo?.ToString(CultureInfo.InvariantCulture) ?? "");
                Set(row, c++, rows[r].CmdNo?.ToString(CultureInfo.InvariantCulture) ?? "");
                Set(row, c++, rows[r].DbId);
                Set(row, c++, rows[r].Attempt?.ToString(CultureInfo.InvariantCulture) ?? "");
                Set(row, c++, rows[r].MaxRetry?.ToString(CultureInfo.InvariantCulture) ?? "");
                Set(row, c++, rows[r].HeaderHex);

                // 重要：Payload 原樣輸出（含尾端空白）
                Set(row, c++, rows[r].Payload);

                //if (includeParsedColumns)
                //{
                //    var p = rows[r].Parsed;
                //    Set(row, c++, p?.Prefix ?? "");
                //    Set(row, c++, p?.MachineNo ?? "");
                //    Set(row, c++, p?.CardNo ?? "");
                //    Set(row, c++, p?.Effect ?? "");
                //    Set(row, c++, p?.SendTimeToken ?? "");
                //    Set(row, c++, p?.MatchKey ?? "");
                //}

                //if (includeOnly80PairHint)
                //{
                //    Set(row, c++, rows[r].SeqNo?.ToString(CultureInfo.InvariantCulture) ?? "");
                //}
            }

            for (int i = 0; i < headers.Count; i++)
                sheet.AutoSizeColumn(i);
        }

        private static void Set(IRow row, int col, string value)
        {
            row.CreateCell(col).SetCellValue(value ?? "");
        }
    }
}