using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace MixTok.Core
{
    public class Util
    {
        public static string FormatTime(TimeSpan s)
        {
            if (s.TotalSeconds <= 60)
            {
                return $"{Math.Round(s.TotalSeconds, 0)} seconds";
            }
            if (s.TotalMinutes <= 60)
            {
                return $"{Math.Round(s.TotalMinutes, 0)}:{Math.Round((float)s.Seconds, 0).ToString().PadLeft(2, '0')} minutes";
            }
            return $"{Math.Round(s.TotalHours, 0)}:{Math.Round((float)s.Minutes, 0).ToString().PadLeft(2, '0')}:{Math.Round((float)s.Seconds, 0).ToString().PadLeft(2, '0')} hours";
        }

        public static string FormatInt(int num)
        {
            return String.Format("{0:n0}", num);
        }
    }
}
