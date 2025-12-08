using System.Collections;
using System.Reflection;
using System.Text;
using TryFormatter.Attributes;


namespace TryFormatter
{
    /// <summary>
    /// 將任意物件（包含 Exception）格式化為 Logfmt 格式的工具類別。
    /// 支援：Object、Dictionary、List、Exception（含 inner exception）
    /// 並自動展開集合，使輸出保持為 flat key-value。
    /// <para> AI 寫的，能運作就別太在意 </para>
    /// <para> 給予多個物件時，若欄位名稱重覆，不特別處理。 </para>
    /// </summary>
    public static class LogfmtFormatter
    {
        /// <summary>
        /// 將指定物件序列化為 logfmt 格式字串。
        /// 支援以下類型：
        /// - Exception：遞迴展開 inner exception
        /// - Object：展開 public property 和 field
        /// - Dictionary：key_subkey=value
        /// - List / IEnumerable：key_0=value, key_1=value
        /// <para> 給予多個物件時，若欄位名稱重覆，不特別處理。 </para>
        /// </summary>
        /// <param name="mainObject">要序列化的物件</param>
        /// <returns>符合 logfmt 格式的字串</returns>
        public static string ToLogFmt(object mainObject, params object[] subValues)
        {
            if (mainObject == null)
                return "";

            var sb = new StringBuilder();

            // 特殊處理 Exception
            if (mainObject is Exception ex)
            {
                SerializeException(ex, sb, "ex");
            }
            else
            {
                SerializeValue(mainObject, sb, null);
            }

            // 處理附加的其他物件
            foreach (var item in subValues)
            {
                sb.Append($"{ToLogFmt(item)}");
            }


            return sb.ToString().Replace(Environment.NewLine, " ---- ").Trim();
        }

        // ---------------------------------------------------------
        // Value Dispatcher：統一分發處理值的方式
        // ---------------------------------------------------------

        /// <summary>
        /// 判斷傳入的值屬於哪種類型並使用適當序列化方式。
        /// </summary>
        private static void SerializeValue(object value, StringBuilder sb, string prefix)
        {
            switch (value)
            {
                case null:
                    return;

                case IDictionary dict:
                    SerializeDictionary(dict, sb, prefix);
                    return;

                case IEnumerable enumerable when value is not string:
                    SerializeList(enumerable, sb, prefix);
                    return;

                default:
                    SerializeObject(value, sb, prefix);
                    return;
            }
        }

        // ---------------------------------------------------------
        // Object Serializer
        // ---------------------------------------------------------

        /// <summary>
        /// 序列化一般物件（反射 public property 與 field）。
        /// </summary>
        private static void SerializeObject(object obj, StringBuilder sb, string prefix)
        {
            var type = obj.GetType();

            // 基本型別 => 直接輸出
            if (IsSimpleType(type))
            {
                AppendKeyValue(sb, prefix, obj, forceOutput: false);
                return;
            }

            // Properties
            foreach (var prop in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                if (!prop.CanRead) continue;

                // 屬性是否應該忽略
                if (ShouldIgnore(prop, prop.PropertyType, out bool ignoreNull, out bool forceOutputNull))
                    continue;

                var val = prop.GetValue(obj);
                var key = CombineKey(prefix, prop.Name, prop);

                // 處理 null 行為
                if (HandleNullValue(sb, key, val, ignoreNull, forceOutputNull))
                    continue;

                SerializeValue(val, sb, key);
            }

            // Fields
            foreach (var field in type.GetFields(BindingFlags.Public | BindingFlags.Instance))
            {
                if (ShouldIgnore(field, field.FieldType, out bool ignoreNull, out bool forceOutputNull))
                    continue;

                var val = field.GetValue(obj);
                var key = CombineKey(prefix, field.Name, field);

                if (HandleNullValue(sb, key, val, ignoreNull, forceOutputNull))
                    continue;

                SerializeValue(val, sb, key);
            }
        }


        // ---------------------------------------------------------
        // Dictionary Serializer
        // ---------------------------------------------------------

        /// <summary>
        /// 將 Dictionary 展開成 key_subkey=value
        /// </summary>
        private static void SerializeDictionary(IDictionary dict, StringBuilder sb, string prefix)
        {
            foreach (var key in dict.Keys)
            {
                string subKey = CombineKey(prefix, key.ToString());
                SerializeValue(dict[key], sb, subKey);
            }
        }

        // ---------------------------------------------------------
        // List / IEnumerable Serializer
        // ---------------------------------------------------------

        /// <summary>
        /// 將 List / IEnumerable 展開成 key_0=value key_1=value ...
        /// </summary>
        private static void SerializeList(IEnumerable list, StringBuilder sb, string prefix)
        {
            int index = 0;

            foreach (var item in list)
            {
                string key = CombineKey(prefix, index.ToString());
                SerializeValue(item, sb, key);
                index++;
            }
        }

        // ---------------------------------------------------------
        // Exception Serializer
        // ---------------------------------------------------------

        /// <summary>
        /// 將 Exception 與 inner exception 逐層展開。
        /// 例：
        /// ex_type=System.Exception
        /// ex_message="Error"
        /// ex_inner_1_message="Inner error"
        /// </summary>
        private static void SerializeException(Exception ex, StringBuilder sb, string prefix, int level = 0)
        {
            // 最外層 ex，用 inner_1, inner_2 代表下一層
            string p = level == 0 ? prefix : $"{prefix}_inner_{level}";

            // Write exception details
            if (ex.GetType() != typeof(Exception))
                AppendKeyValue(sb, $"{p}_type", ex.GetType().FullName);

            AppendKeyValue(sb, $"{p}_message", ex.Message);
            AppendKeyValue(sb, $"{p}_stack", ex.StackTrace);

            //AppendKeyValue(sb, $"{p}_source", ex.Source);
            //AppendKeyValue(sb, $"{p}_hresult", ex.HResult);

            // Exception.Data
            if (ex.Data != null)
            {
                foreach (var key in ex.Data.Keys)
                {
                    AppendKeyValue(sb, $"{p}_data_{key}", ex.Data[key]);
                }
            }

            // inner exception recursion
            if (ex.InnerException != null)
            {
                SerializeException(ex.InnerException, sb, prefix, level + 1);
            }
        }

        // ---------------------------------------------------------
        // Helpers
        // ---------------------------------------------------------

        /// <summary>
        /// 判斷是否為簡單型別（可直接轉字串輸出）。
        /// </summary>
        private static bool IsSimpleType(Type type)
        {
            return type.IsPrimitive
                || type.IsEnum
                || type == typeof(string)
                || type == typeof(decimal)
                || type == typeof(DateTime)
                || type == typeof(Guid);
        }

        /// <summary>
        /// key 合併 prefix 與欄位名，並確保符合 logfmt 格式。
        /// prefix=null 時直接回傳處理過的 key。
        /// </summary>
        private static string CombineKey(string prefix, string key, MemberInfo member = null)
        {
            // 若有 LogFmtName 則覆蓋 key
            var customName = member?.GetCustomAttribute<LogFmtNameAttribute>()?.Name;
            if (!string.IsNullOrWhiteSpace(customName))
                key = customName;

            // 正規化
            key = NormalizeKey(key);
            key = ToCamelCase(key);

            if (string.IsNullOrWhiteSpace(prefix))
                return key;

            prefix = NormalizeKey(prefix);
            prefix = ToCamelCase(prefix);

            return $"{prefix}_{key}";
        }

        /// <summary> camelCase 支援方法 </summary>
        /// <param name="s"></param>
        /// <returns></returns>
        private static string ToCamelCase(string s)
        {
            if (string.IsNullOrEmpty(s))
                return s;

            if (char.IsLower(s[0]))
                return s;

            return char.ToLower(s[0]) + s.Substring(1);
        }

        /// <summary>
        /// 移除 key 中不合法的 logfmt 字元，只保留 a-zA-Z0-9_-。
        /// </summary>
        private static string NormalizeKey(string key)
        {
            return string.Concat(key.Where(c =>
                char.IsLetterOrDigit(c) || c == '_' || c == '-'));
        }

        /// <summary>
        /// 在字串後面附加 key=value 格式內容，並自動處理字串跳脫。
        /// </summary>
        private static void AppendKeyValue(StringBuilder sb, string key, object value, bool forceOutput = false)
        {
            if (value == null && !forceOutput)
                return;

            string val =
                (value is null) 
                    // 強制輸出 key=""
                    ? @"""""" 
                    : FormatValue(value);

            sb.Append($"{key}={val} ");
        }


        /// <summary>
        /// 格式化 value，使其符合 logfmt。
        /// 字串包含空白或特殊字元時會自動加引號。
        /// </summary>
        private static string FormatValue(object value)
        {
            string str = value switch
            {
                string s => s,
                DateTime dt => dt.ToString("yyyy-MM-dd HH:mm:ss.fff"),
                DateOnly dOnly => dOnly.ToString("yyyy-MM-dd"),
                TimeOnly tOnly => tOnly.ToString("HH:mm:ss.fff"),
                bool b => b ? "true" : "false",
                _ => value.ToString()
            };

            if (NeedsQuoting(str))
                return $"\"{EscapeQuotes(str)}\"";

            return str;
        }

        /// <summary> 是否應該忽略 </summary>
        /// <param name="member"></param>
        /// <param name="type"></param>
        /// <param name="ignoreNull"></param>
        /// <param name="forceOutputNull"></param>
        /// <returns></returns>
        private static bool ShouldIgnore(MemberInfo member, Type type,
    out bool ignoreNull, out bool forceOutputNull)
        {
            ignoreNull = true;
            forceOutputNull = false;

            // LogFmtIgnore => 完全不輸出
            if (member.GetCustomAttribute<LogFmtIgnoreAttribute>() != null)
                return true;

            // LogFmtIgnoreWhenNull
            var nullAttr = member.GetCustomAttribute<LogFmtIgnoreWhenNullAttribute>();
            if (nullAttr != null)
            {
                ignoreNull = nullAttr.IgnoreNull;
                forceOutputNull = !nullAttr.IgnoreNull;
            }

            return false;
        }

        /// <summary> 處理 Null 的邏輯 </summary>
        /// <param name="sb"></param>
        /// <param name="key"></param>
        /// <param name="val"></param>
        /// <param name="ignoreNull"></param>
        /// <param name="forceOutputNull"></param>
        /// <returns></returns>
        private static bool HandleNullValue(StringBuilder sb, string key, object val, bool ignoreNull, bool forceOutputNull)
        {
            if (val != null)
                return false; // 不是 null => 正常處理

            if (ignoreNull)
                return true; // null 直接略過欄位

            if (forceOutputNull)
            {
                AppendKeyValue(sb, key, null, forceOutput: true);
                return true;
            }

            return true;
        }


        /// <summary>
        /// 判斷字串是否需要加引號。
        /// logfmt 規範：包含空白、= 或 " 時需加引號。
        /// </summary>
        private static bool NeedsQuoting(string s)
        {
            return s.Any(c => char.IsWhiteSpace(c) || c == '"' || c == '=');
        }

        /// <summary>
        /// 跳脫字串中的雙引號。
        /// </summary>
        private static string EscapeQuotes(string s)
        {
            return s.Replace("\"", "\\\"");
        }
    }

}
