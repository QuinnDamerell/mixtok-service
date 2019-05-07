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
            return new ContentResult
            {
                ContentType = "text/html",
                StatusCode = (int)HttpStatusCode.OK,
                Content = c_html
            };
        }

        static string c_html = @"
<html>
   <body>
      <h1>MixTok - ClipMine</h1>
      <a href=""api/v1/ClipMine""><h2>api/v1/ClipMine</h2 ></a>
      This API allows access to all clips on mixer.The caller can select any set of filters to apply to the returned results.
      <br />
      <br />
      All filters can be applied as get parameters to the REST API.
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
      <strong > SortType </strong > -int: [0 or 1](default 0) - The order in which results are sorted.
      <br /> 0 - ViewCount
      <br /> 1 - MixTokRank
      <br />
      <br />
      <strong > Limit </strong > -int(default 100) - The max number of results to be sent back.
      <br />
      <br />
      <strong > FromTime </strong > -string - No clips older than this time will be returned.
      <br /> Valid inputs are:
      <li > Absolute Time - Anything C# DateTime can parse</li>
      <li> Absolute Time - A Unix Timestamp</li >
      <li > Realitive Time - Times like `-1h`, `-2d`, `-4s`</li >
      <br />
      <strong > ToTime </strong > -string - No clips newer than this time will be returned.
      <br /> Valid inputs are:
      <li > Absolute Time - Anything C# DateTime can parse</li>
      <li> Absolute Time - A Unix Timestamp</li >
      <li > Realitive Time - Times like `-1h`, `-2d`, `-4s`</li >
      <br />
      <strong > ViewCoutMin </strong > -int - Limits the lowest clip view count to be returned.
      <br />
      <br />
      <strong > ChannelId </strong > -int - Only returns clips from a given channel
      <br />
      <br />
      <strong > ChannelName </strong > -string - Only returns clips from a given channel
      <br />
      <br />
      <strong > CurrentlyLive </strong > -bool - Only returns clips from channels that are live or not.
      <br />
      <br />
      <strong > Partnered </strong > -bool - Only returns clips from channels that are partnered or not.
      <br />
      <br />
      <strong > GameTitle </strong > -string - Only returns clips from games where the title contains the passed string.
      <br />
      <br />
      <strong > GameId </strong > -int - Only returns clips from game is the given game type id.
      <br />
      <br />
      <strong > HypeZoneChannelId </strong > -int - Only returns clips that were taken on the given HypeZone channelId. 
      <br />
   </body >
</html >
";
    }
}