using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace MixTok.Core
{
    public class Logger
    {
        public static void Info(string msg)
        {
            Console.WriteLine(msg);
        }

        public static void Error(string msg, Exception e = null)
        {
            Console.WriteLine(msg + "Ex: "+ (e == null ? "null": e.Message));
        }
    }
}
