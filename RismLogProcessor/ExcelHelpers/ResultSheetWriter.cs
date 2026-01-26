using NPOI.SS.UserModel;
using NPOI.SS.Util;
using RismLogProcessor.Models;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace RismLogProcessor.ExcelHelpers
{
    public sealed class ResultSheetWriter
    {
        // A~V 共 22 欄（新增 V: Range）
        private const int Cols = 22;

        public void WriteResultSheet(IWorkbook wb, List<LogRecord> only89, List<LogRecord> only76, List<LogRecord> onlyF6)
        {
            var old = wb.GetSheet("Result");
            if (old != null) wb.RemoveSheetAt(wb.GetSheetIndex(old));

            var sheet = wb.CreateSheet("Result");
            var styles = new ResultStyles(wb);

            // ===== Row 1：群組標頭（含底色與 merge）=====
            var r1 = sheet.CreateRow(0);

            // Altob Send 89: E~I（淺灰）
            CreateCell(r1, 4, "Altob Send 89", styles.GroupGrey);
            sheet.AddMergedRegion(new CellRangeAddress(0, 0, 4, 8));
            ApplyStyleToRowRange(r1, 4, 8, styles.GroupGrey);

            // Hidden: J~M（這塊你沒指定顏色，我用淺灰，且之後 J/K 會空）
            CreateCell(r1, 9, "Hidden", styles.GroupGrey);
            sheet.AddMergedRegion(new CellRangeAddress(0, 0, 9, 12));
            ApplyStyleToRowRange(r1, 9, 12, styles.GroupGrey);

            // Altob Receive 76: N~R（淺藍）
            CreateCell(r1, 13, "Altob Receive 76", styles.GroupBlue);
            sheet.AddMergedRegion(new CellRangeAddress(0, 0, 13, 17));
            ApplyStyleToRowRange(r1, 13, 17, styles.GroupBlue);

            // Altob Reply f6: S~T（淺紅）
            CreateCell(r1, 18, "Altob Reply f6", styles.GroupRed);
            sheet.AddMergedRegion(new CellRangeAddress(0, 0, 18, 19));
            ApplyStyleToRowRange(r1, 18, 19, styles.GroupRed);

            // Duration: U~V（淺橘）
            CreateCell(r1, 20, "Duration", styles.GroupOrange);
            sheet.AddMergedRegion(new CellRangeAddress(0, 0, 20, 21));
            ApplyStyleToRowRange(r1, 20, 21, styles.GroupOrange);

            // ===== Row 2：欄位標頭（照你的既有規劃 + 新增 Range）=====
            var r2 = sheet.CreateRow(1);

            // A~U 沿用你原本那 21 欄；V 新增 Range
            // J/K 不再保留（標頭留空）
            // M 原本 Duration Range → 這欄保留位置但標頭留空（你要的是搬到 V）
            var headers = new string[Cols];
            headers[0] = "Label";
            headers[1] = "DbId";
            headers[2] = "SeqNo";
            headers[3] = "Attempt";
            headers[4] = "Payload";
            headers[5] = "MachineNo";
            headers[6] = "CardNo";
            headers[7] = "Effect";
            headers[8] = "Send Time";

            headers[9] = "";          // J (移除)
            headers[10] = "";          // K (移除)
            headers[11] = "Count";     // L
            headers[12] = "";          // M (原 Duration Range，現在移走)

            headers[13] = "Receive Time"; // N
            headers[14] = "Payload";      // O
            headers[15] = "MachineNo";    // P
            headers[16] = "CardNo";       // Q
            headers[17] = "Effect";       // R

            headers[18] = "Send Time";    // S (f6)
            headers[19] = "Payload";      // T (f6)

            headers[20] = "Seconds";      // U
            headers[21] = "Range";        // V (new, moved here)

            for (int c = 0; c < Cols; c++)
            {
                var cell = r2.CreateCell(c, CellType.String);
                cell.SetCellValue(headers[c] ?? "");

                // 標頭底色分群：依你的指定
                if (c >= 4 && c <= 8) cell.CellStyle = styles.HeaderGrey;         // E~I
                else if (c >= 13 && c <= 17) cell.CellStyle = styles.HeaderBlue;  // N~R
                else if (c >= 18 && c <= 19) cell.CellStyle = styles.HeaderRed;   // S~T
                else if (c >= 20 && c <= 21) cell.CellStyle = styles.HeaderOrange;// U~V
                else cell.CellStyle = styles.HeaderGrey;                           // 其他（含 A~D、Hidden）
            }

            // ===== 建查表：DbId -> 最後一筆 76 / f6（取 Attempt 最大，其次 Time 最大）=====
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

            // ===== 寫資料列（Row 3 起）=====
            var rows = only89
                .OrderBy(x => x.Label, StringComparer.Ordinal)
                .ThenBy(x => x.SeqNo ?? int.MaxValue)
                .ToList();

            for (int i = 0; i < rows.Count; i++)
            {
                var rec = rows[i];
                int excelRow = 3 + i; // 1-based
                int rowIdx = 2 + i;   // 0-based

                var rr = sheet.CreateRow(rowIdx);

                // 先建立 0..V 欄位，套上一般 body 樣式（白底）
                for (int c = 0; c < Cols; c++)
                {
                    var cell = rr.CreateCell(c, CellType.String);
                    cell.CellStyle = styles.Body;
                }

                // A~E：值
                rr.GetCell(0).SetCellValue(rec.Label ?? "");
                rr.GetCell(1).SetCellValue(rec.DbId ?? "");

                // C SeqNo / D Attempt 用數字型別
                rr.GetCell(2).SetCellType(CellType.Numeric);
                if (rec.SeqNo.HasValue) rr.GetCell(2).SetCellValue(rec.SeqNo.Value); else rr.GetCell(2).SetCellValue(0);

                rr.GetCell(3).SetCellType(CellType.Numeric);
                if (rec.Attempt.HasValue) rr.GetCell(3).SetCellValue(rec.Attempt.Value); else rr.GetCell(3).SetCellValue(0);

                rr.GetCell(4).SetCellValue(rec.Payload ?? ""); // 不 Trim

                // I Send Time：DateTime（讓 U 的公式可用）
                rr.GetCell(8).SetCellType(CellType.Numeric);
                rr.GetCell(8).CellStyle = styles.BodyDate;
                if (rec.Time.HasValue) rr.GetCell(8).SetCellValue(rec.Time.Value);
                else rr.GetCell(8).SetBlank();

                // F/G/H：保留公式（你之後若要也可改成用 Parsed 直接寫值）
                rr.GetCell(5).SetCellFormula($"MID(E{excelRow},3,2)");
                rr.GetCell(6).SetCellFormula($"MID(E{excelRow},5,16)");
                rr.GetCell(7).SetCellFormula($"MID(E{excelRow},21,4)");

                // L Count（保留公式）
                rr.GetCell(11).SetCellType(CellType.Numeric);
                rr.GetCell(11).SetCellFormula($"COUNTIF(B:B,$B{excelRow})");

                // gating：只在最後一次 attempt 才填 76/f6（與你原本 IF(L=D,...) 同意義）
                bool isLastAttempt = false;
                if (!string.IsNullOrEmpty(rec.DbId) && rec.Attempt.HasValue && dbIdCount.TryGetValue(rec.DbId, out var cnt))
                    isLastAttempt = (cnt == rec.Attempt.Value);

                // N/O：76 Receive Time & Payload（N 用 DateTime，O 原文）
                if (isLastAttempt && !string.IsNullOrEmpty(rec.DbId) &&
                    last76ByDbId.TryGetValue(rec.DbId, out var r76) && r76.Time.HasValue)
                {
                    rr.GetCell(13).SetCellType(CellType.Numeric);
                    rr.GetCell(13).CellStyle = styles.BodyDate;
                    rr.GetCell(13).SetCellValue(r76.Time.Value);

                    rr.GetCell(14).SetCellValue(r76.Payload ?? "");

                    // P/Q/R：沿用 MID 從 O 拆（保留公式）
                    rr.GetCell(15).SetCellFormula($"MID(O{excelRow},3,2)");
                    rr.GetCell(16).SetCellFormula($"MID(O{excelRow},7,16)");
                    rr.GetCell(17).SetCellFormula($"MID(O{excelRow},23,4)");
                }
                else
                {
                    rr.GetCell(13).SetCellValue(""); // 留空，讓 ISNUMBER 判斷失敗
                    rr.GetCell(14).SetCellValue("");
                    rr.GetCell(15).SetCellValue("-");
                    rr.GetCell(16).SetCellValue("-");
                    rr.GetCell(17).SetCellValue("-");
                }

                // S/T：f6 Send Time & Payload（S 用 DateTime）
                if (isLastAttempt && !string.IsNullOrEmpty(rec.DbId) &&
                    lastF6ByDbId.TryGetValue(rec.DbId, out var rf6) && rf6.Time.HasValue)
                {
                    rr.GetCell(18).SetCellType(CellType.Numeric);
                    rr.GetCell(18).CellStyle = styles.BodyDate;
                    rr.GetCell(18).SetCellValue(rf6.Time.Value);

                    rr.GetCell(19).SetCellValue(rf6.Payload ?? "");
                }
                else
                {
                    rr.GetCell(18).SetCellValue("");
                    rr.GetCell(19).SetCellValue("");
                }

                // U Seconds：Excel 公式（可靠性要求）
                // =IF(ISNUMBER(N3),ROUND((N3-I3)*86400,0),"No Reply")
                rr.GetCell(20).SetCellType(CellType.Numeric);
                rr.GetCell(20).SetCellFormula($"IF(ISNUMBER(N{excelRow}),ROUND((N{excelRow}-I{excelRow})*86400,0),\"No Reply\")");

                // V Range：Excel 公式（依 U 分桶 30 秒）
                // =IF(ISNUMBER(U3),TEXT(FLOOR(U3,30),"000")&"~"&TEXT(CEILING(U3,30),"000"),"")
                rr.GetCell(21).SetCellFormula($"IF(ISNUMBER(U{excelRow}),TEXT(FLOOR(U{excelRow},30),\"000\")&\"~\"&TEXT(CEILING(U{excelRow},30),\"000\"),\"\")");
            }

            // ===== 條件式格式：SeqNo 偶數（C 欄）整列淺綠 =====
            // 套用範圍：A3:V(last)
            ApplyConditionalFormattingBySeqNo(sheet, styles);

            // 冻结前兩列
            sheet.CreateFreezePane(0, 2);
        }

        private static void ApplyConditionalFormattingBySeqNo(ISheet sheet, ResultStyles styles)
        {
            int lastRow = Math.Max(sheet.LastRowNum, 2);
            if (lastRow < 2) 
                return;

            var scf = sheet.SheetConditionalFormatting;

            // MOD($C3,2)=0
            var rule = scf.CreateConditionalFormattingRule("MOD($C3,2)=1");
            var pf = rule.CreatePatternFormatting();
            pf.FillPattern = FillPattern.SolidForeground;
            pf.FillBackgroundColor = styles.LightGreenIndex;

            var regions = new[]
            {
                new CellRangeAddress(2, lastRow, 0, Cols - 1) // Row 3..last, Col A..V
            };

            scf.AddConditionalFormatting(regions, rule);
        }

        private static void CreateCell(IRow row, int col, string value, ICellStyle style)
        {
            var c = row.CreateCell(col, CellType.String);
            c.SetCellValue(value);
            c.CellStyle = style;
        }

        private static void ApplyStyleToRowRange(IRow row, int startCol, int endCol, ICellStyle style)
        {
            for (int c = startCol; c <= endCol; c++)
            {
                var cell = row.GetCell(c) ?? row.CreateCell(c, CellType.String);
                cell.CellStyle = style;
            }
        }

        private sealed class ResultStyles
        {
            public short LightGreenIndex { get; }

            public ICellStyle GroupGrey { get; }
            public ICellStyle GroupBlue { get; }
            public ICellStyle GroupRed { get; }
            public ICellStyle GroupOrange { get; }

            public ICellStyle HeaderGrey { get; }
            public ICellStyle HeaderBlue { get; }
            public ICellStyle HeaderRed { get; }
            public ICellStyle HeaderOrange { get; }

            public ICellStyle Body { get; }
            public ICellStyle BodyDate { get; }

            public ResultStyles(IWorkbook wb)
            {
                LightGreenIndex = IndexedColors.LightGreen.Index;

                var bold = wb.CreateFont();
                bold.IsBold = true;

                GroupGrey = MakeGroup(wb, bold, IndexedColors.Grey25Percent);
                GroupBlue = MakeGroup(wb, bold, IndexedColors.LightCornflowerBlue);
                GroupRed = MakeGroup(wb, bold, IndexedColors.Rose);
                GroupOrange = MakeGroup(wb, bold, IndexedColors.LightOrange);

                HeaderGrey = MakeHeader(wb, bold, IndexedColors.Grey25Percent);
                HeaderBlue = MakeHeader(wb, bold, IndexedColors.LightCornflowerBlue);
                HeaderRed = MakeHeader(wb, bold, IndexedColors.Rose);
                HeaderOrange = MakeHeader(wb, bold, IndexedColors.LightOrange);

                Body = wb.CreateCellStyle();
                Body.Alignment = HorizontalAlignment.Left;
                Body.VerticalAlignment = VerticalAlignment.Center;
                SetBorderThin(Body);

                BodyDate = wb.CreateCellStyle();
                BodyDate.CloneStyleFrom(Body);
                var fmt = wb.CreateDataFormat();
                BodyDate.DataFormat = fmt.GetFormat("yyyy-mm-dd hh:mm:ss.000");
            }

            private static ICellStyle MakeGroup(IWorkbook wb, IFont font, IndexedColors fill)
            {
                var s = wb.CreateCellStyle();
                s.SetFont(font);
                s.Alignment = HorizontalAlignment.Center;
                s.VerticalAlignment = VerticalAlignment.Center;
                s.WrapText = false;
                SetBorderThin(s);
                SetFill(s, fill);
                return s;
            }

            private static ICellStyle MakeHeader(IWorkbook wb, IFont font, IndexedColors fill)
            {
                var s = wb.CreateCellStyle();
                s.SetFont(font);
                s.Alignment = HorizontalAlignment.Center;
                s.VerticalAlignment = VerticalAlignment.Center;
                s.WrapText = true;
                SetBorderThin(s);
                SetFill(s, fill);
                return s;
            }

            private static void SetBorderThin(ICellStyle s)
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
