using System.Globalization;
using System.Text;
using CsvHelper;
using CsvHelper.Configuration;
// ===== Payload Parser (fixed-position, NO trim) =====
public sealed class PayloadParser
{
    public ParsedPayload Parse(string payload)
    {
        // payload 不能 Trim
        var p = payload ?? "";

        // OK 判斷：允許後面都是空白（但不 Trim 原字串）
        if (IsOkPayload(p))
            return new ParsedPayload { Prefix = "OK" };

        if (p.Length < 2)
            return new ParsedPayload { Prefix = p };

        var prefix = p.Substring(0, 2).ToLowerInvariant();

        if (prefix == "f6")
            return new ParsedPayload { Prefix = "f6" };

        if (prefix == "80")
            return new ParsedPayload { Prefix = "80" };

        if (prefix != "89" && prefix != "76")
            return new ParsedPayload { Prefix = prefix };

        // ===== 關鍵：固定位置解析 =====
        // 你給的規則：
        // - 76：從第27個字(1-based)開始取10個字 => Substring(26,10)
        // 推導出常見結構：
        // 89: [0..1]=89, [2..3]=Machine(2), [4..19]=Card(16), [20..23]=Effect(4), [24..33]=Time(10)
        // 76: [0..1]=76, [2..3]=Machine(2), [4..5]=??(2), [6..21]=Card(16), [22..25]=Effect(4), [26..35]=Time(10)

        try
        {
            if (prefix == "89")
            {
                var machine = SafeSub(p, 2, 2);
                var card = SafeSub(p, 4, 16);
                var effect = SafeSub(p, 20, 4);
                var time = SafeSub(p, 24, 10);

                var matchKey = BuildMatchKey(machine, card, effect, time);
                return new ParsedPayload
                {
                    Prefix = "89",
                    MachineNo = machine,
                    CardNo = card,
                    Effect = effect,
                    SendTimeToken = time,
                    MatchKey = matchKey
                };
            }
            else // 76
            {
                var machine = SafeSub(p, 2, 2);
                var card = SafeSub(p, 6, 16);
                var effect = SafeSub(p, 22, 4);

                // 你指定：第27字開始取10字
                var time = SafeSub(p, 26, 10);

                var matchKey = BuildMatchKey(machine, card, effect, time);
                return new ParsedPayload
                {
                    Prefix = "76",
                    MachineNo = machine,
                    CardNo = card,
                    Effect = effect,
                    SendTimeToken = time,
                    MatchKey = matchKey
                };
            }
        }
        catch
        {
            // 解析失敗仍回 prefix
            return new ParsedPayload { Prefix = prefix };
        }
    }

    private static bool IsOkPayload(string p)
    {
        if (p.Length < 2) return false;
        if (!string.Equals(p.Substring(0, 2), "OK", StringComparison.OrdinalIgnoreCase)) return false;

        // 後面允許全空白（space/tab）
        for (int i = 2; i < p.Length; i++)
        {
            if (!char.IsWhiteSpace(p[i])) return false;
        }
        return true;
    }

    private static string SafeSub(string s, int start, int len)
    {
        if (start < 0 || len <= 0) return "";
        if (s.Length < start + len) return "";
        return s.Substring(start, len);
    }

    private static string BuildMatchKey(string machine, string card, string effect, string time10)
    {
        if (string.IsNullOrEmpty(machine) ||
            string.IsNullOrEmpty(card) ||
            string.IsNullOrEmpty(effect) ||
            string.IsNullOrEmpty(time10))
            return "";

        return $"{machine}|{card}|{effect}|{time10}";
    }
}
