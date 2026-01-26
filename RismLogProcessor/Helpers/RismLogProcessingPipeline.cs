using RismLogProcessor.ExcelHelpers;
using RismLogProcessor.Models;

namespace RismLogProcessor.Helpers
{
    /// <summary>
    /// 把 Program.cs 裡的處理流程抽成可重用、可測試的 Pipeline。
    /// </summary>
    public sealed class RismLogProcessingPipeline
    {
        private readonly CsvLogReader _reader;
        private readonly PayloadParser _parser;
        private readonly DbIdFixer _fixer;
        private readonly ExcelReportWriter _excelWriter;

        public RismLogProcessingPipeline(
            CsvLogReader? reader = null,
            PayloadParser? parser = null,
            DbIdFixer? fixer = null,
            ExcelReportWriter? excelWriter = null)
        {
            _reader = reader ?? new CsvLogReader();
            _parser = parser ?? new PayloadParser();
            _fixer = fixer ?? new DbIdFixer();
            _excelWriter = excelWriter ?? new ExcelReportWriter();
        }

        /// <summary>
        /// 從專案資料夾執行：讀 Source/*.csv -> 處理 -> 輸出 Output/RawLogs.xlsx
        /// </summary>
        public PipelineResult RunFromProjectDir(
            string projectDir,
            string sourceFolderName = "Source",
            string outputFolderName = "Output",
            string outputFileName = "RawLogs.xlsx")
        {
            var sourceDir = Path.Combine(projectDir, sourceFolderName);
            var outputDir = Path.Combine(projectDir, outputFolderName);
            Directory.CreateDirectory(outputDir);

            if (!Directory.Exists(sourceDir))
                throw new DirectoryNotFoundException($"找不到 Source 資料夾：{sourceDir}");

            var csvFiles = Directory.GetFiles(sourceDir, "*.csv", SearchOption.TopDirectoryOnly)
                .OrderBy(f => f, StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (csvFiles.Count == 0)
                throw new InvalidOperationException($"Source 資料夾沒有任何 CSV：{sourceDir}");

            var outPath = Path.Combine(outputDir, outputFileName);
            return Run(csvFiles, outPath);
        }

        /// <summary>
        /// 允許你直接給 CSV 檔案清單與輸出路徑（更好測試，也利於未來 CLI 參數化）。
        /// </summary>
        public PipelineResult Run(IReadOnlyList<string> csvFiles, string outXlsxPath)
        {
            if (csvFiles == null || csvFiles.Count == 0)
                throw new ArgumentException("csvFiles 不可為空", nameof(csvFiles));

            // 1) 讀取合併 SourceLog（Label=檔名）
            var sourceLog = new List<LogRecord>(capacity: 1024);
            foreach (var file in csvFiles)
            {
                var label = Path.GetFileNameWithoutExtension(file);
                sourceLog.AddRange(_reader.Read(file, label));
            }

            // 2) Payload 解析（重要：不 Trim Payload）
            foreach (var r in sourceLog)
                r.Parsed = _parser.Parse(r.Payload);

            // 3) 修補 DbId：用 89 的 DbId 回填/覆寫 76 的 DbId
            _fixer.FixDbIdFor76(sourceLog);

            // 4) 分類 Only*
            var only89 = sourceLog.Where(r => r.Parsed?.Prefix == "89").ToList();
            var only76 = sourceLog.Where(r => r.Parsed?.Prefix == "76").ToList();
            var onlyF6 = sourceLog.Where(r => r.Parsed?.Prefix == "f6").ToList();

            // Only80：只收 80 命令 + 能對上 80 的 OK（依 SeqNo / DbId）
            var only80 = BuildOnly80Pairs(sourceLog);

            // 5) 輸出 Excel
            _excelWriter.Write(outXlsxPath, sourceLog, only89, only76, onlyF6, only80);

            return new PipelineResult(
                outXlsxPath,
                sourceLog,
                only89,
                only76,
                onlyF6,
                only80);
        }


        private static List<LogRecord> BuildOnly80Pairs(List<LogRecord> sourceLog)
        {
            var list80 = sourceLog.Where(r => r.Parsed?.Prefix == "80").ToList();
            var listOk = sourceLog.Where(r => PayloadRules.IsOkPayload(r.Payload)).ToList();

            // 建索引加速：
            // 1) SeqNo -> OK list
            var okBySeq = listOk
                .Where(r => r.SeqNo.HasValue)
                .GroupBy(r => r.SeqNo!.Value)
                .ToDictionary(g => g.Key, g => g.ToList());

            // 2) DbId -> OK list（若 OK 那邊也有 DbId）
            var okByDbId = listOk
                .Where(r => !string.IsNullOrEmpty(r.DbId))
                .GroupBy(r => r.DbId)
                .ToDictionary(g => g.Key, g => g.ToList(), StringComparer.Ordinal);

            var usedOk = new HashSet<LogRecord>();
            var result = new List<LogRecord>();

            foreach (var r80 in list80)
            {
                LogRecord? matchedOk = null;

                // 優先 SeqNo
                if (r80.SeqNo.HasValue && okBySeq.TryGetValue(r80.SeqNo.Value, out var oks1))
                {
                    matchedOk = oks1.FirstOrDefault(x => !usedOk.Contains(x));
                }

                // 其次 DbId（你說可以用 DbId 來對應）
                if (matchedOk == null && !string.IsNullOrEmpty(r80.DbId) && okByDbId.TryGetValue(r80.DbId, out var oks2))
                {
                    matchedOk = oks2.FirstOrDefault(x => !usedOk.Contains(x));
                }

                // 加入 80；若有配對 OK，再加入 OK
                result.Add(r80);
                if (matchedOk != null)
                {
                    result.Add(matchedOk);
                    usedOk.Add(matchedOk);
                }
            }

            return result;
        }
    }

    public sealed record PipelineResult(
        string OutputPath,
        List<LogRecord> SourceLog,
        List<LogRecord> Only89,
        List<LogRecord> Only76,
        List<LogRecord> OnlyF6,
        List<LogRecord> Only80);
}
