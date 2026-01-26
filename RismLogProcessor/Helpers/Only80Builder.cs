using RismLogProcessor.Models;

public static class Only80Builder
{
    public static List<LogRecord> BuildOnly80Pairs(List<LogRecord> sourceLog)
    {
        var list80 = sourceLog.Where(r => r.Parsed?.Prefix == "80").ToList();
        var listOk = sourceLog.Where(r => IsOkPayload(r.Payload)).ToList();

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

    private static bool IsOkPayload(string p)
    {
        if (p == null)
            return false;
        if (p.Length < 2)
            return false;
        if (!string.Equals(p.Substring(0, 2), "OK", StringComparison.OrdinalIgnoreCase))
            return false;

        for (int i = 2; i < p.Length; i++)
        {
            if (!char.IsWhiteSpace(p[i]))
                return false;
        }
        return true;
    }
}
