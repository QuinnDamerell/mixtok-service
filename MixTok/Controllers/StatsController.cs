using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace MixTok.Controllers
{
    public class StatsResponse
    {
        public int IndexedClips;
        public int ChannelsWithClips;
        public int LiveChannelsWithClips;
        public string LastUpdate;
        public string LastUpdateDuration;
        public int ClipsCreatedInLastDay;
    }

    [Route("api/v1/[controller]")]
    [ApiController]
    public class StatsController : ControllerBase
    {
        // GET: api/v1/stats
        [HttpGet]
        public StatsResponse Get()
        {
            Tuple<int, int> chan = Program.s_ClipMine.GetChannelCount();
            return new StatsResponse
            {
                ChannelsWithClips = chan.Item1,
                LiveChannelsWithClips = chan.Item2,
                IndexedClips = Program.s_ClipMine.GetClipsCount(),
                LastUpdate = FormatTime(DateTime.Now - Program.s_ClipMine.GetLastUpdateTime()),
                LastUpdateDuration = FormatTime(Program.s_ClipMine.GetLastUpdateDuration()),
                ClipsCreatedInLastDay = Program.s_ClipMine.ClipsCreatedInLastTime(new TimeSpan(24, 0, 0))
            };
        }

        private string FormatTime(TimeSpan s)
        {
            if (s.TotalSeconds <= 60)
            {
                return $"{Math.Round(s.TotalSeconds, 2)} seconds";
            }
            if (s.TotalMinutes <= 60)
            {
                return $"{Math.Round(s.TotalMinutes, 2)} minutes";
            }
            return $"{Math.Round(s.TotalHours, 2)} hours";
        }
    }
}