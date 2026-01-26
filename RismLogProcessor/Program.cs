using RismLogProcessor.Models;

namespace RismLogProcessor
{
    public static class Program
    {
        public static int Main(string[] args)
        {
            try
            {
                var baseDir = AppContext.BaseDirectory;
                var projectDir = FindProjectDir(baseDir);
                var sourceDir = Path.Combine(projectDir, "Source");
                var outputDir = Path.Combine(projectDir, "Output");
                Directory.CreateDirectory(outputDir);

                if (!Directory.Exists(sourceDir))
                {
                    Console.Error.WriteLine($"[ERROR] 找不到 Source 資料夾：{sourceDir}");
                    return 1;
                }

                var csvFiles = Directory.GetFiles(sourceDir, "*.csv", SearchOption.TopDirectoryOnly)
                                        .OrderBy(f => f, StringComparer.OrdinalIgnoreCase)
                                        .ToList();

                if (csvFiles.Count == 0)
                {
                    Console.Error.WriteLine($"[ERROR] Source 資料夾沒有任何 CSV：{sourceDir}");
                    return 1;
                }

                Console.WriteLine($"找到 {csvFiles.Count} 份 CSV：");
                foreach (var f in csvFiles) 
                    Console.WriteLine($" - {Path.GetFileName(f)}");

                // 1) 讀取合併 SourceLog（Label=檔名）
                var reader = new CsvLogReader();
                var sourceLog = new List<LogRecord>();
                foreach (var file in csvFiles)
                {
                    var label = Path.GetFileNameWithoutExtension(file);
                    sourceLog.AddRange(reader.Read(file, label));
                }

                // 2) Payload 解析（重要：不 Trim Payload，固定位置 substring）
                var parser = new PayloadParser();
                foreach (var r in sourceLog)
                    r.Parsed = parser.Parse(r.Payload);

                // 3) 修補 DbId：用 89 的 DbId 回填/覆寫 76 的 DbId
                var fixer = new DbIdFixer();
                fixer.FixDbIdFor76(sourceLog);

                // 4) 分類 Only*
                var only89 = sourceLog.Where(r => r.Parsed?.Prefix == "89").ToList();
                var only76 = sourceLog.Where(r => r.Parsed?.Prefix == "76").ToList();
                var onlyF6 = sourceLog.Where(r => r.Parsed?.Prefix == "f6").ToList();

                // Only80：只收 80 命令 + 能對上 80 的 OK（依 SeqNo / DbId）
                var only80 = Only80Builder.BuildOnly80Pairs(sourceLog);

                // 5) 輸出 Excel
                var outPath = Path.Combine(outputDir, "RawLogs.xlsx");
                var writer = new ExcelReportWriter();
                writer.Write(outPath, sourceLog, only89, only76, onlyF6, only80);

                Console.WriteLine();
                Console.WriteLine($"完成：{outPath}");
                Console.WriteLine($"SourceLog: {sourceLog.Count}");
                Console.WriteLine($"Only89   : {only89.Count}");
                Console.WriteLine($"Only76   : {only76.Count}");
                Console.WriteLine($"OnlyF6   : {onlyF6.Count}");
                Console.WriteLine($"Only80   : {only80.Count} (已過濾 OK，只保留能配對者)");

                return 0;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine("[FATAL] " + ex);
                return 2;
            }
        }

        private static string FindProjectDir(string startDir)
        {
            var dir = new DirectoryInfo(startDir);
            for (int i = 0; i < 8 && dir != null; i++)
            {
                if (dir.GetFiles("*.csproj").Any()) return dir.FullName;
                dir = dir.Parent;
            }
            return startDir;
        }
    }
}