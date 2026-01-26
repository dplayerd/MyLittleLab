using NPOI.SS.UserModel;
using RismLogProcessor.Models;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace RismLogProcessor.ExcelHelpers
{
    public sealed class SummarySheetWriter
    {
        public void WriteSummarySheet(IWorkbook wb, List<LogRecord> only89, List<LogRecord> only76)
        {
            // Recreate Summary
            var old = wb.GetSheet("Summary");
            if (old != null) wb.RemoveSheetAt(wb.GetSheetIndex(old));
            var sheet = wb.CreateSheet("Summary");

            var styles = new Styles(wb);

            // Header: Label, Duration, Counting, Avg, Min, Max
            var header = sheet.CreateRow(0);
            Set(header, 0, "Label", styles.Header);
            Set(header, 1, "Duration", styles.Header);
            Set(header, 2, "Counting", styles.Header);
            Set(header, 3, "Avg", styles.Header);
            Set(header, 4, "Min", styles.Header);
            Set(header, 5, "Max", styles.Header);

            // 76 lookup: DbId -> last 76
            var last76ByDbId = only76
                .Where(x => !string.IsNullOrEmpty(x.DbId) && x.Time.HasValue)
                .GroupBy(x => x.DbId)
                .ToDictionary(
                    g => g.Key,
                    g => g.OrderByDescending(x => x.Attempt ?? -1)
                          .ThenByDescending(x => x.Time ?? DateTime.MinValue)
                          .First()
                );

            // DbId count for gating (Count == Attempt)
            var dbIdCount = only89
                .Where(x => !string.IsNullOrEmpty(x.DbId))
                .GroupBy(x => x.DbId)
                .ToDictionary(g => g.Key, g => g.Count());

            // Collect valid seconds
            var items = new List<Item>();
            foreach (var r89 in only89)
            {
                if (string.IsNullOrEmpty(r89.DbId) || !r89.Time.HasValue || !r89.Attempt.HasValue) continue;
                if (!dbIdCount.TryGetValue(r89.DbId, out var cnt)) continue;

                bool isLastAttempt = (cnt == r89.Attempt.Value);
                if (!isLastAttempt) continue;

                if (!last76ByDbId.TryGetValue(r89.DbId, out var r76)) continue;
                if (!r76.Time.HasValue) continue;

                var sec = (int)Math.Round((r76.Time.Value - r89.Time.Value).TotalSeconds);
                if (sec < 0) continue;

                items.Add(new Item
                {
                    Label = r89.Label ?? "",
                    Seconds = sec,
                    Range = BuildRange(sec)
                });
            }

            var byLabel = items
                .GroupBy(x => x.Label)
                .OrderBy(g => g.Key, StringComparer.Ordinal)
                .ToList();

            int rowIdx = 1; // start after header
            int firstDataRowExcel = 2; // Excel row number for rowIdx=1

            // Keep track of subtotal cells for grand total formula
            var subtotalCountCellsExcel = new List<int>(); // store Excel row number where subtotal count is written (column C)

            foreach (var labelGroup in byLabel)
            {
                var label = labelGroup.Key;

                var rangeStats = labelGroup
                    .GroupBy(x => x.Range)
                    .Select(g => new
                    {
                        Range = g.Key,
                        Count = g.Count(),
                        Avg = g.Average(z => z.Seconds),
                        Min = g.Min(z => z.Seconds),
                        Max = g.Max(z => z.Seconds),
                        Low = ParseRangeLow(g.Key)
                    })
                    .OrderBy(x => x.Low)
                    .ThenBy(x => x.Range, StringComparer.Ordinal)
                    .ToList();

                bool firstRowOfLabel = true;

                // For subtotal formula: remember the first and last data row (in this label block) in Excel coordinates
                int blockStartExcelRow = -1;
                int blockEndExcelRow = -1;

                foreach (var s in rangeStats)
                {
                    var r = sheet.CreateRow(rowIdx);
                    int excelRow = rowIdx + 1; // because NPOI rowIdx is 0-based, Excel is 1-based

                    if (firstRowOfLabel)
                    {
                        // Label appears only once, with a light fill to visually separate groups
                        Set(r, 0, label, styles.LabelCell);
                        firstRowOfLabel = false;
                    }
                    else
                    {
                        Set(r, 0, "", styles.BodyText);
                    }

                    Set(r, 1, s.Range, styles.BodyText);
                    Set(r, 2, s.Count, styles.BodyInt);
                    Set(r, 3, Round0(s.Avg), styles.BodyInt);
                    Set(r, 4, s.Min, styles.BodyInt);
                    Set(r, 5, s.Max, styles.BodyInt);

                    // borders for entire row cells (A..F)
                    ApplyRowBorders(r, styles);

                    if (blockStartExcelRow == -1) blockStartExcelRow = excelRow;
                    blockEndExcelRow = excelRow;

                    rowIdx++;
                }

                // Subtotal row: "Subtotal" next to it, Counting is a formula SUM(Cstart:Cend)
                {
                    var subtotalRow = sheet.CreateRow(rowIdx);
                    int excelRow = rowIdx + 1;

                    // A blank, B "Subtotal"
                    Set(subtotalRow, 0, "", styles.SubtotalText);
                    Set(subtotalRow, 1, "Subtotal", styles.SubtotalText);

                    // C formula
                    var cCell = subtotalRow.CreateCell(2, CellType.Numeric);
                    cCell.CellStyle = styles.SubtotalInt;
                    if (blockStartExcelRow != -1 && blockEndExcelRow != -1)
                        cCell.SetCellFormula($"SUM(C{blockStartExcelRow}:C{blockEndExcelRow})");
                    else
                        cCell.SetCellValue(0);

                    // D/E/F blank
                    Set(subtotalRow, 3, "", styles.SubtotalText);
                    Set(subtotalRow, 4, "", styles.SubtotalText);
                    Set(subtotalRow, 5, "", styles.SubtotalText);

                    ApplyRowBorders(subtotalRow, styles, isSubtotalOrTotal: true);

                    subtotalCountCellsExcel.Add(excelRow);
                    rowIdx++;
                }

                // Blank line
                rowIdx++;
            }

            // Grand Total row: "Grand Total" and Counting formula sum(subtotals)
            {
                var totalRow = sheet.CreateRow(rowIdx);
                int excelRow = rowIdx + 1;

                Set(totalRow, 0, "Grand Total", styles.TotalText);
                Set(totalRow, 1, "", styles.TotalText);

                var cCell = totalRow.CreateCell(2, CellType.Numeric);
                cCell.CellStyle = styles.TotalInt;

                if (subtotalCountCellsExcel.Count > 0)
                {
                    // =SUM(C7,C12,...) (sum of subtotal rows)
                    var parts = subtotalCountCellsExcel.Select(r => $"C{r}");
                    cCell.SetCellFormula("SUM(" + string.Join(",", parts) + ")");
                }
                else
                {
                    cCell.SetCellValue(0);
                }

                Set(totalRow, 3, "", styles.TotalText);
                Set(totalRow, 4, "", styles.TotalText);
                Set(totalRow, 5, "", styles.TotalText);

                ApplyRowBorders(totalRow, styles, isSubtotalOrTotal: true);
                rowIdx++;
            }

            // Column widths (approx)
            sheet.SetColumnWidth(0, 28 * 256); // Label
            sheet.SetColumnWidth(1, 14 * 256); // Duration
            sheet.SetColumnWidth(2, 10 * 256); // Counting
            sheet.SetColumnWidth(3, 10 * 256); // Avg
            sheet.SetColumnWidth(4, 10 * 256); // Min
            sheet.SetColumnWidth(5, 10 * 256); // Max

            sheet.CreateFreezePane(0, 1);
        }

        private static int Round0(double v) => (int)Math.Round(v, MidpointRounding.AwayFromZero);

        private static string BuildRange(int seconds)
        {
            int lo = (seconds / 30) * 30;
            int hi = ((seconds + 29) / 30) * 30;
            return lo.ToString("000", CultureInfo.InvariantCulture) + "~" + hi.ToString("000", CultureInfo.InvariantCulture);
        }

        private static int ParseRangeLow(string range)
        {
            if (string.IsNullOrEmpty(range)) return int.MaxValue;
            var parts = range.Split('~');
            if (parts.Length == 0) return int.MaxValue;
            return int.TryParse(parts[0], out var v) ? v : int.MaxValue;
        }

        private static void Set(IRow row, int col, string value, ICellStyle style)
        {
            var cell = row.GetCell(col) ?? row.CreateCell(col, CellType.String);
            cell.SetCellValue(value ?? "");
            cell.CellStyle = style;
        }

        private static void Set(IRow row, int col, int value, ICellStyle style)
        {
            var cell = row.GetCell(col) ?? row.CreateCell(col, CellType.Numeric);
            cell.SetCellValue(value);
            cell.CellStyle = style;
        }

        private static void ApplyRowBorders(IRow row, Styles styles, bool isSubtotalOrTotal = false)
        {
            // Ensure A..F exist and apply border styles; subtotal/total use different fill already
            for (int c = 0; c <= 5; c++)
            {
                var cell = row.GetCell(c) ?? row.CreateCell(c, CellType.String);
                if (cell.CellStyle == null) cell.CellStyle = styles.BodyText;

                // Borders are part of each style already; this just ensures cells exist.
            }
        }

        private sealed class Item
        {
            public string Label { get; set; } = "";
            public string Range { get; set; } = "";
            public int Seconds { get; set; }
        }

        private sealed class Styles
        {
            public ICellStyle Header { get; }
            public ICellStyle BodyText { get; }
            public ICellStyle BodyInt { get; }

            public ICellStyle LabelCell { get; }

            public ICellStyle SubtotalText { get; }
            public ICellStyle SubtotalInt { get; }

            public ICellStyle TotalText { get; }
            public ICellStyle TotalInt { get; }

            public Styles(IWorkbook wb)
            {
                var fmt = wb.CreateDataFormat();

                var bold = wb.CreateFont();
                bold.IsBold = true;

                // Header: light gray fill
                Header = wb.CreateCellStyle();
                Header.SetFont(bold);
                Header.Alignment = HorizontalAlignment.Center;
                Header.VerticalAlignment = VerticalAlignment.Center;
                Header.WrapText = true;
                SetFill(Header, IndexedColors.Grey25Percent);
                SetBorderThin(Header);

                // Body
                BodyText = wb.CreateCellStyle();
                BodyText.Alignment = HorizontalAlignment.Left;
                BodyText.VerticalAlignment = VerticalAlignment.Center;
                SetBorderThin(BodyText);

                BodyInt = wb.CreateCellStyle();
                BodyInt.Alignment = HorizontalAlignment.Right;
                BodyInt.VerticalAlignment = VerticalAlignment.Center;
                BodyInt.DataFormat = fmt.GetFormat("0");
                SetBorderThin(BodyInt);

                // Label cell (group marker): light blue fill
                LabelCell = wb.CreateCellStyle();
                LabelCell.CloneStyleFrom(BodyText);
                LabelCell.SetFont(bold);
                SetFill(LabelCell, IndexedColors.PaleBlue);
                SetBorderThin(LabelCell);

                // Subtotal: light blue fill + bold
                SubtotalText = wb.CreateCellStyle();
                SubtotalText.CloneStyleFrom(BodyText);
                SubtotalText.SetFont(bold);
                SetFill(SubtotalText, IndexedColors.PaleBlue);
                SetBorderThin(SubtotalText);

                SubtotalInt = wb.CreateCellStyle();
                SubtotalInt.CloneStyleFrom(BodyInt);
                SubtotalInt.SetFont(bold);
                SetFill(SubtotalInt, IndexedColors.PaleBlue);
                SetBorderThin(SubtotalInt);

                // Total: a bit stronger blue/gray
                TotalText = wb.CreateCellStyle();
                TotalText.CloneStyleFrom(BodyText);
                TotalText.SetFont(bold);
                SetFill(TotalText, IndexedColors.LightTurquoise);
                SetBorderThin(TotalText);

                TotalInt = wb.CreateCellStyle();
                TotalInt.CloneStyleFrom(BodyInt);
                TotalInt.SetFont(bold);
                SetFill(TotalInt, IndexedColors.LightTurquoise);
                SetBorderThin(TotalInt);
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
