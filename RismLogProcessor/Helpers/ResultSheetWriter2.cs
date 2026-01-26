using NPOI.SS.UserModel;
using NPOI.SS.Util;
using RismLogProcessor.Models;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RismLogProcessor.Helpers
{
    public sealed class ResultSheetWriter2
    {
        private const int Cols = 21;

        public void WriteResultSheet(IWorkbook wb, List<LogRecord> only89, List<LogRecord> only76, List<LogRecord> onlyF6)
        {
            var old = wb.GetSheet("Result");
            if (old != null)
            {
                int idx = wb.GetSheetIndex(old);
                wb.RemoveSheetAt(idx);
            }

            var sheet = wb.CreateSheet("Result");

            // ===== 建立樣式（一次建立，全表共用）=====
            var styles = new ResultStyles(wb);

            // --- Row1 分組標題 + merge ---
            var r1 = sheet.CreateRow(0);
            CreateAndStyleCell(r1, 4, "Altob Send 89", styles.GroupHeader);
            CreateAndStyleCell(r1, 9, "Hidden", styles.GroupHeader);
            CreateAndStyleCell(r1, 13, "Altob Receive 76", styles.GroupHeader);
            CreateAndStyleCell(r1, 18, "Altob Reply f6", styles.GroupHeader);
            CreateAndStyleCell(r1, 20, "Duration", styles.GroupHeader);

            sheet.AddMergedRegion(new CellRangeAddress(0, 0, 4, 8));    // E1:I1
            sheet.AddMergedRegion(new CellRangeAddress(0, 0, 9, 12));   // J1:M1
            sheet.AddMergedRegion(new CellRangeAddress(0, 0, 13, 17));  // N1:R1
            sheet.AddMergedRegion(new CellRangeAddress(0, 0, 18, 19));  // S1:T1

            // 把 merge 範圍內的空格也補 style（不然 Excel 看起來會斷）
            ApplyStyleToMergedRow(r1, 4, 8, styles.GroupHeader);
            ApplyStyleToMergedRow(r1, 9, 12, styles.GroupHeader);
            ApplyStyleToMergedRow(r1, 13, 17, styles.GroupHeader);
            ApplyStyleToMergedRow(r1, 18, 19, styles.GroupHeader);
            // U1 單格
            ApplyStyleToMergedRow(r1, 20, 20, styles.GroupHeader);

            // --- Row2 欄位標頭 ---
            var r2 = sheet.CreateRow(1);
            string[] headers = new[]
            {
                "Label","DbId","SeqNo","Attempt","Payload","MachineNo","CardNo","Effect","Send Time",
                "Receive 76 Time","Receive 76 Payload","Count","Duration Range",
                "Receive Time","Payload","MachineNo","CardNo","Effect","Send Time","Payload","Seconds"
            };
            for (int c = 0; c < headers.Length; c++)
            {
                CreateAndStyleCell(r2, c, headers[c], styles.ColumnHeader);
            }

            // ===== 查表：DbId -> 最後一筆 76 / f6 =====
            var last76ByDbId = only76
                .Where(x => !string.IsNullOrEmpty(x.DbId))
                .GroupBy(x => x.DbId)
                .ToDictionary(
                    g => g.Key,
                    g => g.OrderByDescending(x => x.Attempt ?? -1)
                          .ThenByDescending(x => x.Time ?? DateTime.MinValue)
                          .First()
                );

            var lastF6ByDbId = onlyF6
                .Where(x => !string.IsNullOrEmpty(x.DbId))
                .GroupBy(x => x.DbId)
                .ToDictionary(
                    g => g.Key,
                    g => g.OrderByDescending(x => x.Attempt ?? -1)
                          .ThenByDescending(x => x.Time ?? DateTime.MinValue)
                          .First()
                );

            var dbIdCount = only89
                .Where(x => !string.IsNullOrEmpty(x.DbId))
                .GroupBy(x => x.DbId)
                .ToDictionary(g => g.Key, g => g.Count());

            // 依你原本報告常見排序
            var rows = only89
                .OrderBy(x => x.Label, StringComparer.Ordinal)
                .ThenBy(x => x.SeqNo ?? int.MaxValue)
                .ToList();

            // ===== 依 SeqNo 交錯底色：SeqNo 變更才 toggle =====
            int? lastSeq = null;
            bool zebra = false; // false = A 色, true = B 色

            for (int i = 0; i < rows.Count; i++)
            {
                var rec = rows[i];
                int excelRow = 3 + i;
                int rowIdx = 2 + i;

                if (lastSeq == null || rec.SeqNo != lastSeq)
                {
                    zebra = !zebra;
                    lastSeq = rec.SeqNo;
                }

                var rr = sheet.CreateRow(rowIdx);

                // 挑 body style（A/B）
                var bodyText = zebra ? styles.BodyTextB : styles.BodyTextA;
                var bodyNum = zebra ? styles.BodyNumberB : styles.BodyNumberA;
                var bodyDate = zebra ? styles.BodyDateB : styles.BodyDateA;

                // 先把整列 0..20 全部建 cell 並套 style，避免有些欄位沒寫值時樣式不一致
                for (int c = 0; c < Cols; c++)
                {
                    var cell = rr.CreateCell(c, CellType.String);
                    cell.CellStyle = bodyText;
                }
                // 針對數字/日期欄改用對應 style
                // SeqNo(C), Attempt(D), Count(L), Seconds(U)
                rr.GetCell(2).CellStyle = bodyNum;
                rr.GetCell(3).CellStyle = bodyNum;
                rr.GetCell(11).CellStyle = bodyNum;
                rr.GetCell(20).CellStyle = bodyNum;
                // Send Time (I) 我們用日期 style（DateTime）
                rr.GetCell(8).CellStyle = bodyDate;

                // A~E：值
                rr.GetCell(0).SetCellValue(rec.Label ?? "");
                rr.GetCell(1).SetCellValue(rec.DbId ?? "");
                if (rec.SeqNo.HasValue) rr.GetCell(2).SetCellValue(rec.SeqNo.Value); else rr.GetCell(2).SetCellValue("");
                if (rec.Attempt.HasValue) rr.GetCell(3).SetCellValue(rec.Attempt.Value); else rr.GetCell(3).SetCellValue("");
                rr.GetCell(4).SetCellValue(rec.Payload ?? ""); // 不 trim

                // I：89 Send Time（DateTime）
                if (rec.Time.HasValue) rr.GetCell(8).SetCellValue(rec.Time.Value);
                else rr.GetCell(8).SetCellValue("");

                // F/G/H：保留公式（也可之後改寫值）
                rr.GetCell(5).SetCellFormula($"MID(E{excelRow},3,2)");
                rr.GetCell(6).SetCellFormula($"MID(E{excelRow},5,16)");
                rr.GetCell(7).SetCellFormula($"MID(E{excelRow},21,4)");

                // L：Count（保留公式）
                rr.GetCell(11).SetCellFormula($"COUNTIF(B:B,$B{excelRow})");

                // gating：是否最後一次 attempt
                bool isLastAttempt = false;
                if (!string.IsNullOrEmpty(rec.DbId) && rec.Attempt.HasValue && dbIdCount.TryGetValue(rec.DbId, out var cnt))
                    isLastAttempt = (cnt == rec.Attempt.Value);

                // 76：把 time/payload 原始文字寫入 Hidden(J/K) 與 Receive76(N/O)
                if (isLastAttempt && !string.IsNullOrEmpty(rec.DbId) && last76ByDbId.TryGetValue(rec.DbId, out var r76) && r76.Time.HasValue)
                {
                    var t76 = r76.Time.Value.ToString("yyyy-MM-dd HH:mm:ss.fff", CultureInfo.InvariantCulture);
                    rr.GetCell(9).SetCellValue(t76);          // J
                    rr.GetCell(10).SetCellValue(r76.Payload ?? ""); // K

                    rr.GetCell(13).SetCellValue(t76);         // N
                    rr.GetCell(14).SetCellValue(r76.Payload ?? ""); // O

                    // P/Q/R：留公式（要拔掉也行）
                    rr.GetCell(15).SetCellFormula($"MID(O{excelRow},3,2)");
                    rr.GetCell(16).SetCellFormula($"MID(O{excelRow},7,16)");
                    rr.GetCell(17).SetCellFormula($"MID(O{excelRow},23,4)");
                }
                else
                {
                    rr.GetCell(9).SetCellValue("No Reply");
                    rr.GetCell(10).SetCellValue("No Reply");
                    rr.GetCell(13).SetCellValue("No Reply");
                    rr.GetCell(14).SetCellValue("-");
                    rr.GetCell(15).SetCellValue("-");
                    rr.GetCell(16).SetCellValue("-");
                    rr.GetCell(17).SetCellValue("-");
                }

                // f6：S/T
                if (isLastAttempt && !string.IsNullOrEmpty(rec.DbId) && lastF6ByDbId.TryGetValue(rec.DbId, out var rf6) && rf6.Time.HasValue)
                {
                    var tf6 = rf6.Time.Value.ToString("yyyy-MM-dd HH:mm:ss.fff", CultureInfo.InvariantCulture);
                    rr.GetCell(18).SetCellValue(tf6);
                    rr.GetCell(19).SetCellValue(rf6.Payload ?? "");
                }
                else
                {
                    rr.GetCell(18).SetCellValue("No Reply");
                    rr.GetCell(19).SetCellValue("No Reply");
                }

                // Duration (U) 與 Range (M)：直接算值（避免時間字串公式）
                if (isLastAttempt && rec.Time.HasValue
                    && !string.IsNullOrEmpty(rec.DbId)
                    && last76ByDbId.TryGetValue(rec.DbId, out var r76Dur)
                    && r76Dur.Time.HasValue)
                {
                    var sec = (int)Math.Round((r76Dur.Time.Value - rec.Time.Value).TotalSeconds);
                    rr.GetCell(20).SetCellValue(sec);
                    rr.GetCell(12).SetCellValue(BuildRange(sec));
                }
                else
                {
                    rr.GetCell(20).SetCellValue("No Reply");
                    rr.GetCell(12).SetCellValue("");
                }
            }

            // （可選）凍結前兩列，方便檢視
            sheet.CreateFreezePane(0, 2);
        }

        private static void CreateAndStyleCell(IRow row, int col, string value, ICellStyle style)
        {
            var c = row.CreateCell(col, CellType.String);
            c.SetCellValue(value);
            c.CellStyle = style;
        }

        private static void ApplyStyleToMergedRow(IRow row, int startCol, int endCol, ICellStyle style)
        {
            for (int c = startCol; c <= endCol; c++)
            {
                var cell = row.GetCell(c) ?? row.CreateCell(c);
                cell.CellStyle = style;
            }
        }

        private static string BuildRange(int seconds)
        {
            int lo = (seconds / 30) * 30;
            int hi = ((seconds + 29) / 30) * 30;
            return lo.ToString("000", CultureInfo.InvariantCulture) + "~" + hi.ToString("000", CultureInfo.InvariantCulture);
        }

        // ===== Style container =====
        private sealed class ResultStyles
        {
            public ICellStyle GroupHeader { get; }
            public ICellStyle ColumnHeader { get; }

            public ICellStyle BodyTextA { get; }
            public ICellStyle BodyTextB { get; }

            public ICellStyle BodyNumberA { get; }
            public ICellStyle BodyNumberB { get; }

            public ICellStyle BodyDateA { get; }
            public ICellStyle BodyDateB { get; }

            public ResultStyles(IWorkbook wb)
            {
                // Fonts
                var headerFont = wb.CreateFont();
                headerFont.IsBold = true;

                var groupFont = wb.CreateFont();
                groupFont.IsBold = true;

                // Group header style (Row1)
                GroupHeader = wb.CreateCellStyle();
                GroupHeader.SetFont(groupFont);
                GroupHeader.Alignment = HorizontalAlignment.Center;
                GroupHeader.VerticalAlignment = VerticalAlignment.Center;
                SetBordersThin(GroupHeader);
                SetFill(GroupHeader, IndexedColors.Grey25Percent);

                // Column header style (Row2)
                ColumnHeader = wb.CreateCellStyle();
                ColumnHeader.SetFont(headerFont);
                ColumnHeader.Alignment = HorizontalAlignment.Center;
                ColumnHeader.VerticalAlignment = VerticalAlignment.Center;
                ColumnHeader.WrapText = true;
                SetBordersThin(ColumnHeader);
                SetFill(ColumnHeader, IndexedColors.Grey40Percent);

                // Body styles A/B
                BodyTextA = CreateBodyText(wb, IndexedColors.White);
                BodyTextB = CreateBodyText(wb, IndexedColors.Grey25Percent);

                BodyNumberA = CreateBodyNumber(wb, IndexedColors.White);
                BodyNumberB = CreateBodyNumber(wb, IndexedColors.Grey25Percent);

                BodyDateA = CreateBodyDate(wb, IndexedColors.White);
                BodyDateB = CreateBodyDate(wb, IndexedColors.Grey25Percent);
            }

            private static ICellStyle CreateBodyText(IWorkbook wb, IndexedColors fill)
            {
                var s = wb.CreateCellStyle();
                s.Alignment = HorizontalAlignment.Left;
                s.VerticalAlignment = VerticalAlignment.Center;
                s.WrapText = false;
                SetBordersThin(s);
                SetFill(s, fill);
                return s;
            }

            private static ICellStyle CreateBodyNumber(IWorkbook wb, IndexedColors fill)
            {
                var s = wb.CreateCellStyle();
                s.Alignment = HorizontalAlignment.Right;
                s.VerticalAlignment = VerticalAlignment.Center;
                SetBordersThin(s);
                SetFill(s, fill);
                // 一般整數顯示
                var fmt = wb.CreateDataFormat();
                s.DataFormat = fmt.GetFormat("0");
                return s;
            }

            private static ICellStyle CreateBodyDate(IWorkbook wb, IndexedColors fill)
            {
                var s = wb.CreateCellStyle();
                s.Alignment = HorizontalAlignment.Left;
                s.VerticalAlignment = VerticalAlignment.Center;
                SetBordersThin(s);
                SetFill(s, fill);
                // 你要的顯示格式（先做基本）
                var fmt = wb.CreateDataFormat();
                s.DataFormat = fmt.GetFormat("yyyy-mm-dd hh:mm:ss.000");
                return s;
            }

            private static void SetBordersThin(ICellStyle s)
            {
                s.BorderBottom = BorderStyle.Thin;
                s.BorderTop = BorderStyle.Thin;
                s.BorderLeft = BorderStyle.Thin;
                s.BorderRight = BorderStyle.Thin;
            }

            private static void SetFill(ICellStyle s, IndexedColors color)
            {
                s.FillPattern = FillPattern.SolidForeground;
                s.FillForegroundColor = color.Index;
            }
        }
    }
}
