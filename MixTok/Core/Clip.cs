using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace MixTok.Core
{
    public class Clip
    {
        public double Rank;
        public MixerChannel Channel;

        public int Views;
        public int TypeId;
        public string Title;
        public string ClipUrl;
        public string ContentId;
        public DateTime Created;
        public string ShareableUrl;
    }
}
