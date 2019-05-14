using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using MixTok.Core;

namespace MixTok.Controllers
{
    public class StatsResponse
    {
        public int IndexedClips;
        public int ChannelsWithClips;
        public int LiveChannelsWithClips;
        public string LastUpdate;
        public string LastUpdateDuration;
        public string LastBackup;
        public int ClipsCreatedInLastDay;
        public string CurrentStatus;
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
                LastUpdate = Util.FormatTime(DateTime.Now - Program.s_ClipMine.GetLastUpdateTime()),
                LastUpdateDuration = Util.FormatTime(Program.s_ClipMine.GetLastUpdateDuration()),
                LastBackup = Util.FormatTime(DateTime.Now - Program.s_ClipMine.GetLastBackupTime()),
                ClipsCreatedInLastDay = Program.s_ClipMine.ClipsCreatedInLastTime(new TimeSpan(24, 0, 0)),
                CurrentStatus = Program.s_ClipMine.GetStatus()
            };
        }
    }
}