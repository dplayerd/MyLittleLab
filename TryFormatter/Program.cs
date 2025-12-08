using System;
using System.Collections.Generic;
using System.Linq;
using TryFormatter.Attributes;


namespace TryFormatter
{
    internal partial class Program
    {
        static void Main(string[] args)
        {
            var orgColor = Console.ForegroundColor;

            try
            {
                Console.ForegroundColor = ConsoleColor.DarkGreen;
                WriteDoubleObject();

                Console.ForegroundColor = ConsoleColor.DarkMagenta;
                WriteObject();

                Console.ForegroundColor = ConsoleColor.DarkCyan;
                WriteArray();

                Console.ForegroundColor = ConsoleColor.DarkYellow;
                WriteDictionary();

                WriteException();

            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.DarkRed;
                string logfmt = LogfmtFormatter.ToLogFmt(ex);
                //Console.WriteLine(logfmt);

                var arrLines = logfmt.Split(Environment.NewLine);
                for (int i = 0; i < arrLines.Length; i++)
                {
                    Console.WriteLine($"{i.ToString("0000")}  {arrLines[i]}");
                }
            }


            Console.ForegroundColor = orgColor;
        }

        private static void WriteDictionary()
        {
            var dict = new Dictionary<string, object>()
            {
                { "Id", 10 },
                { "Name", "Charlie Delta" },
                { "IsActive", false },
                { "Score", 95.5 },
            };
            var logfmt = LogfmtFormatter.ToLogFmt(dict);
            var arrLines = logfmt.Split(Environment.NewLine);
            for (int i = 0; i < arrLines.Length; i++)
            {
                Console.WriteLine($"{i.ToString("0000")}  {arrLines[i]}");
            }
        }

        private static void WriteArray()
        {

            var arr = new List<string>() { "l1", "o2", "c3" };

            var logfmt = LogfmtFormatter.ToLogFmt(arr);

            var arrLines = logfmt.Split(Environment.NewLine);
            for (int i = 0; i < arrLines.Length; i++)
            {
                Console.WriteLine($"{i.ToString("0000")}  {arrLines[i]}");
            }
        }

        private static void WriteObject()
        {
            var user = new
            {
                Id = 5,
                Name = "Alice Bob",
                Active = true,
                Lines = new List<string>() { "l1", "o2", "c3" },
                Dict = new Dictionary<string, object>()
                {
                    { "Id", 10 },
                    { "Name", "Charlie Delta" },
                    { "IsActive", false },
                    { "Score", 95.5 },
                }
            };

            var logfmt = LogfmtFormatter.ToLogFmt(user);

            var arrLines = logfmt.Split(Environment.NewLine);
            for (int i = 0; i < arrLines.Length; i++)
            {
                Console.WriteLine($"{i.ToString("0000")}  {arrLines[i]}");
            }
            //Console.WriteLine(LogFmtSerializer.ToLogFmt(user));
        }

        private static void WriteDoubleObject()
        {
            var objMain = new LogMainContent
            {
                Time = DateTime.Now,
                TraceCode = "TRACE123456",
                Event = "UserLogin",
                DB_id = Guid.NewGuid().ToString(),
                Title = "User Login Event"
            };


            var obj1 = new TestClass()
            {
                X1 = null,
                X2 = null,
                Id = 5,
                Name = "Alice Bob",
                Active = true
            };

            var logfmt = LogfmtFormatter.ToLogFmt(objMain, obj1);


            Console.WriteLine($"0000  {logfmt}");
            //Console.WriteLine(LogFmtSerializer.ToLogFmt(user));
        }

        public static void WriteException()
        {
            Method2();
        }

        public static void Method2()
        {
            try
            {
                Method3();
            }
            catch (Exception ex)
            {
                throw new Exception("wow, inner exception", ex);
            }
        }

        public static void Method3()
        {
            throw new ArgumentException("some thing error");
        }
    }

    internal class TestClass
    {
        public int? X1 { get; set; }

        [LogFmtIgnoreWhenNull(false)]
        public int? X2 { get; set; }
        
        public int Id { get; set; }
        
        [LogFmtIgnore]
        public string Name { get; set; }

        [LogFmtName("Enabled")]
        public bool Active { get; set; }
    }
}
