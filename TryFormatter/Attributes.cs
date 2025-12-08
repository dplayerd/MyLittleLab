using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TryFormatter.Attributes
{
    /// <summary> LogFmt 名稱屬性 </summary>
    [AttributeUsage(AttributeTargets.Property | AttributeTargets.Field)]
    public class LogFmtNameAttribute : Attribute
    {
        public string Name { get; }

        public LogFmtNameAttribute(string name)
        {
            Name = name;
        }
    }

    /// <summary> LogFmt 忽略屬性 </summary>
    [AttributeUsage(AttributeTargets.Property | AttributeTargets.Field)]
    public class LogFmtIgnoreAttribute : Attribute
    {
    }

    /// <summary> LogFmt 忽略 null 屬性 </summary>
    [AttributeUsage(AttributeTargets.Property | AttributeTargets.Field)]
    public class LogFmtIgnoreWhenNullAttribute : Attribute
    {
        public bool IgnoreNull { get; }

        /// <summary> 預設為 true（null 不輸出） </summary>
        /// <param name="ignoreNull"></param>
        public LogFmtIgnoreWhenNullAttribute(bool ignoreNull = true)
        {
            IgnoreNull = ignoreNull;
        }
    }
}
