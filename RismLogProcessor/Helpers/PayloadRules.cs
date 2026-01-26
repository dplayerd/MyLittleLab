using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RismLogProcessor.Helpers
{
    internal class PayloadRules
    {
        public static bool IsOkPayload(string p)
        {
            if (p == null)
                return false;
            if (p.Length < 2)
                return false;
            if (!string.Equals(p.Substring(0, 2), "OK", StringComparison.OrdinalIgnoreCase))
                return false;
            // 後面可以全是空白
            for (int i = 2; i < p.Length; i++)
            {
                if (p[i] != ' ')
                    return false;
            }
            return true;
        }
    }
}
