using System;
using System.Collections.Generic;
using System.Linq;
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
        public IActionResult Get()
        {
            return Ok(
                @"<html>
    < body >
        < h1 > ClipMine </ h1 >
        < a herf = 'api/v1/ClipMine' ></ a >< h2 > api / v1 / ClipMine </ h2 ></ a >
        This API allows access to all clips on mixer.The caller can select any set of filters to apply to the returned results.
        < br />
        < br />
        All filters can be applied as get parameters to the REST API.
        < br />
        Any subset of filters can be applied together.
        < br />
        All filers are optional.If any filter is excluded it will not be considered.
        < br />
        < h3 > Filters </ h3 >
        < strong > SortType </ strong > -int: [0 or 1](default 0) - The order in which results are sorted.
        < br /> 0 - ViewCount
        < br /> 1 - MixTokRank
        < br />
        < br />
        < strong > Limit </ strong > -int(default 100) - The max number of results to be sent back.
        < br />
        < br />
        < strong > FromTime </ strong > -string - No clips older than this time will be returned.
        < br /> Valid inputs are:
        < li > Absolute Time - Anything C# DateTime can parse</li>
        <li> Absolute Time - A Unix Timestamp</ li >
 
         < li > Realitive Time - Times like `-1h`, `-2d`, `-4s`</ li >
    
            < br />
    
            < strong > ToTime </ strong > -string - No clips newer than this time will be returned.
        < br /> Valid inputs are:
        < li > Absolute Time - Anything C# DateTime can parse</li>
        <li> Absolute Time - A Unix Timestamp</ li >
 
         < li > Realitive Time - Times like `-1h`, `-2d`, `-4s`</ li >
    
            < br />
    
            < strong > ViewCoutMin </ strong > -int - Limits the lowest clip view count to be returned.
        < br />
        < br />
        < strong > ChannelId </ strong > -int - Only returns clips from a given channel
             < br />
     
             < br />
     
             < strong > ChannelName </ strong > -string - Only returns clips from a given channel
                  < br />
          
                  < br />
          
                  < strong > CurrentlyLive </ strong > -bool - Only returns clips from channels that are live or not.
               
                       < br />
               
                       < br />
               
                       < strong > Partnered </ strong > -bool - Only returns clips from channels that are partnered or not.
                    
                            < br />
                    
                            < br />
                    
                            < strong > GameTitle </ strong > -string - Only returns clips from games where the title contains the passed string.
                         
                                 < br />
                         
                                 < br />
                         
                                 < strong > GameId </ strong > -int - Only returns clips from game is the given game type id.
                              
                                      < br />
                              
                                      < br />
                              
                                      < strong > HypeZoneChannelId </ strong > -int - Only returns clips that were taken on the given HypeZone channelId. 
        < br />
    </ body >
</ html > ");
        }
    }
}