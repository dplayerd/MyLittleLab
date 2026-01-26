using RismLogProcessor.Models;

public sealed class DbIdFixer
{
    public void FixDbIdFor76(List<LogRecord> sourceLog)
    {
        // 以 89 的 MatchKey -> DbId 建索引
        var map = new Dictionary<string, string>(StringComparer.Ordinal);

        foreach (var r in sourceLog)
        {
            if (r.Parsed?.Prefix != "89") continue;
            if (string.IsNullOrWhiteSpace(r.DbId)) continue;
            if (string.IsNullOrWhiteSpace(r.Parsed.MatchKey)) continue;

            if (!map.ContainsKey(r.Parsed.MatchKey))
                map[r.Parsed.MatchKey] = r.DbId;
        }

        // 對所有 76：只要能對到 89，就強制覆寫 DbId
        foreach (var r in sourceLog)
        {
            if (r.Parsed?.Prefix != "76") continue;
            if (string.IsNullOrWhiteSpace(r.Parsed.MatchKey)) continue;

            if (map.TryGetValue(r.Parsed.MatchKey, out var dbidFrom89))
            {
                r.DbId = dbidFrom89;
            }
        }
    }
}
