using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace MixTok.Controllers
{
    [Route("/")]
    [ApiController]
    public class HomeController : ControllerBase
    {
        [HttpGet]
        public ContentResult Get()
        {
            string output = string.Copy(c_html);
            var tup = Program.s_ClipMine.GetChannelCount();
            output = output.Replace("{channel}", tup.Item1 + String.Empty);
            output = output.Replace("{onlineChannels}", tup.Item2 + String.Empty);
            output = output.Replace("{clips}", Program.s_ClipMine.GetClipsCount() + String.Empty);
            output = output.Replace("{lastUpdate}", FormatTime(DateTime.Now - Program.s_ClipMine.GetLastUpdateTime()));
            output = output.Replace("{lastUpdateDuration}", FormatTime(Program.s_ClipMine.GetLastUpdateDuration()));
            output = output.Replace("{last24hrClips}", Program.s_ClipMine.ClipsCreatedInLastTime(new TimeSpan(24, 0, 0)) + String.Empty);

            return new ContentResult
            {
                ContentType = "text/html",
                StatusCode = (int)HttpStatusCode.OK,
                Content = output
            };
        }

        const string c_html = @"
<html>
   <body>
      <h1>MixTok - ClipMine</h1>
      <h3>Current Status:</h3>
      <strong>Clips:</strong> {clips}<br />
      <strong>Clips Created in last 24hrs:</strong> {last24hrClips}<br />
      <strong>Channels With Clips:</strong> {channel}<br />
      <strong>Live Channels With Clips:</strong> {onlineChannels}<br />
      <strong>Last Updated:</strong> {lastUpdate} ago<br />
      <strong>Last Update Duration:</strong> {lastUpdateDuration}
      <h3>API: <a href=""api/v1/ClipMine"">api/v1/ClipMine</a></h3>
      This API allows access to all clips currently on mixer. The caller can select any set of filters to apply to the returned results.
      <br />
      <br />
      All filters can be applied as URL get parameters to the REST API.
      <br />
      Any subset of filters can be applied together.
      <br />
      All filers are optional.If any filter is excluded it will not be considered.
      <h3 > Examples </h3 >
      View count sorted, only live channels, not partnered, clipped game matches ""fort""
      <br /><a href=""api/v1/ClipMine?sortType=0&CurrentlyLive=true&Partnered=false&GameTitle=fort"">api/v1/ClipMine?sortType=0&CurrentlyLive=true&Partnered=false&GameTitle=fort</a>
      <br/>      
      <br/>      
      MixTok Rank Sorted, only live channels, no more than an hour old
      <br /><a href=""api/v1/ClipMine?sortType=1&CurrentlyLive=true&FromTime=1h"">api/v1/ClipMine?sortType=1&CurrentlyLive=true&FromTime=1h</a>
      <h3 > Filters </h3 >
      <strong > SortType </strong > - int: [0 or 1](default 0) - The order in which results are sorted.
      <br /> 0 - ViewCount
      <br /> 1 - MixTokRank
      <br />
      <br />
      <strong > Limit </strong > - int(default 100) - The max number of results to be sent back.
      <br />
      <br />
      <strong > FromTime </strong > - string - No clips older than this time will be returned.
      <br /> Valid inputs are:
      <li > Absolute Time - Anything C# DateTime can parse</li>
      <li> Absolute Time - A Unix Timestamp</li >
      <li > Realitive Time - Times like `-1h`, `-2d`, `-4s`</li >
      <br />
      <strong > ToTime </strong > - string - No clips newer than this time will be returned.
      <br /> Valid inputs are:
      <li > Absolute Time - Anything C# DateTime can parse</li>
      <li> Absolute Time - A Unix Timestamp</li >
      <li > Realitive Time - Times like `-1h`, `-2d`, `-4s`</li >
      <br />
      <strong > ViewCoutMin </strong > - int - Limits the lowest clip view count to be returned.
      <br />
      <br />
      <strong > Language </strong > - string - Limits the languages returend. Examples: ""en"", ""es"", etc.
      <br />
      <br />
      <strong > ChannelId </strong > - int - Only returns clips from a given channel
      <br />
      <br />
      <strong > ChannelName </strong > -string - Only returns clips from a given channel
      <br />
      <br />
      <strong > CurrentlyLive </strong > - bool - Only returns clips from channels that are live or not.
      <br />
      <br />
      <strong > Partnered </strong > - bool - Only returns clips from channels that are partnered or not.
      <br />
      <br />
      <strong > GameTitle </strong > - string - Only returns clips from games where the title contains the passed string.
      <br />
      <br />
      <strong > GameId </strong > - int - Only returns clips from game is the given game type id.
      <br />
      <br />
      <strong > HypeZoneChannelId </strong > - int - Only returns clips that were taken on the given HypeZone channelId. 
      <br />
   </body >
</html >
";

        private string FormatTime(TimeSpan s)
        {
            if(s.TotalSeconds <= 60)
            {
                return $"{Math.Round(s.TotalSeconds, 2)} seconds";
            }
            if(s.TotalMinutes <= 60)
            {
                return $"{Math.Round(s.TotalMinutes, 2)} minutes";
            }
            return $"{Math.Round(s.TotalHours, 2)} hours";
        }
    }
}