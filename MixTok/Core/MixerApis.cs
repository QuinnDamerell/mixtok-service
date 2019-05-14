using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;

namespace MixTok.Core
{
    public class MixerSocail
    {
        public string Twitter;
        public string Facebook;
        public string Youtube;
        public string Instagram;
        public string Player;
        public string Steam;
        public string Soundcloud;
        public string Patreon;
    }

    public class MixerUser
    {
        public MixerSocail Social;
        public bool Verified;
        public string Bio;

        public void UpdateFromNewer(MixerUser fresh)
        {
            Verified = fresh.Verified;
            Bio = fresh.Bio;
        }
    }

    public class MixerChannel
    {
        public int Id;
        public int ViewersCurrent;
        public bool Online;
        public bool Partnered;
        public int UserId;
        public string ChannelLogo;
        public bool VodsEnabled;
        public MixerUser User;

        [JsonProperty("languageId")]
        public string Language;

        [JsonProperty("token")]
        public string Name;

        public void UpdateFromNewer(MixerChannel fresh)
        {
            ViewersCurrent = fresh.ViewersCurrent;
            Online = fresh.Online;
            Partnered = fresh.Partnered;
            ChannelLogo = fresh.ChannelLogo;
            VodsEnabled = fresh.VodsEnabled;
            Language = fresh.Language;
            Name = fresh.Name;            
        }
    }

    public class ClipContent
    { 
        public string LocatorType;
        public string Uri;
    }

    public class MixerClip
    {
        public string Title;
        public double MixTokRank;
        public int ViewCount;
        public int TypeId;
        public int HypeZoneChannelId;
        public string ClipUrl;
        public string ShareableUrl;
        public string GameTitle;
        public string ContentId;
        public string ShareableId;
        public int ContentMaturity;
        public int DurationInSeconds;
        public DateTime UploadDate;
        public DateTime ExpirationDate;
        public List<ClipContent> ContentLocators;
        public MixerChannel Channel;
        public List<string> Tags;

        public void UpdateFromNewer(MixerClip fresh)
        {
            ViewCount = fresh.ViewCount;
            Channel.UpdateFromNewer(fresh.Channel);
        }
    }

    public class MixerType
    {
        public int Id;
        public string Name;
        public string Parent;
    }

    public class MixerApis
    {
        static HttpClient s_client = new HttpClient();
        static Dictionary<int, string> m_gameNameCache = new Dictionary<int, string>();

        public static async Task<List<MixerChannel>> GetOnlineChannels(int viwersInclusiveLimit = 5, string languageFilter = null)
        {
            List<MixerChannel> channels = new List<MixerChannel>();
            int i = 0;
            while (i < 1000)
            {
                try
                {
                    string response = await MakeMixerHttpRequest($"api/v1/channels?limit=100&page={i}&order=online:desc,viewersCurrent:desc&fields=token,id,viewersCurrent,online,userId,user,languageId,vodsEnabled,partnered");
                    List<MixerChannel> chan = JsonConvert.DeserializeObject<List<MixerChannel>>(response);
                    channels.AddRange(chan);

                    // Check if we hit the end.
                    if (chan.Count == 0)
                    {
                        break;
                    }

                    // Check if we are on channels that are under our viewer limit
                    if (chan[0].ViewersCurrent < viwersInclusiveLimit)
                    {
                        break;
                    }

                    // Check if we hit the end of online channels.
                    if (!chan[0].Online)
                    {
                        break;
                    }

                    // Sleep a little so we don't hit the API too hard.
                    await Task.Delay(10);
                }
                catch (Exception e)
                {
                    Logger.Error($"Failed to query channel API.", e);
                    break;
                }
                i++;
            }

            List<MixerChannel> final = new List<MixerChannel>();
            foreach(var chan in channels)
            {
                if(String.IsNullOrWhiteSpace(chan.Language))
                {
                    chan.Language = "unknown";
                }
                if(!String.IsNullOrWhiteSpace(languageFilter) && !chan.Language.Equals(languageFilter))
                {
                    continue;
                }
                if(chan.Online)
                {
                    chan.ChannelLogo = $"https://mixer.com/api/v1/users/{chan.UserId}/avatar";
                    final.Add(chan);
                }
            }
            return final;
        }

        public static async Task<List<MixerClip>> GetClips(int channelId)
        {
            string response = await MakeMixerHttpRequest($"api/v1/clips/channels/{channelId}");
            List<MixerClip> list = JsonConvert.DeserializeObject<List<MixerClip>>(response);

            // Add some meta data.
            foreach (MixerClip c in list)
            {
                // Add the game title
                c.GameTitle = await GetGameName(c.TypeId);

                // Pull out the HLS url.
                foreach (var con in c.ContentLocators)
                {
                    if (con.LocatorType.Equals("HlsStreaming"))
                    {
                        c.ClipUrl = con.Uri;
                        break;
                    }
                }

                // Create the deep link url.
                c.ShareableUrl = $"https://mixer.com/{channelId}?clip={c.ShareableId}";

                // Pull out the hypezone channel id if there is one.
                c.HypeZoneChannelId = 0;
                if (c.Tags != null && c.Tags.Count > 0)
                {
                    foreach(string s in c.Tags)
                    {
                        if(s.StartsWith("HZ-"))
                        {
                            int hypeChanId = 0;
                            string end = s.Substring(3);
                            if(int.TryParse(end, out hypeChanId))
                            {
                                c.HypeZoneChannelId = hypeChanId;
                            }
                        }
                    }
                }
            }
            return list;
        }

        public static async Task<string> GetGameName(int typeId)
        {
            // Check the cache
            lock(m_gameNameCache)
            {
                if(m_gameNameCache.ContainsKey(typeId))
                {
                    return m_gameNameCache[typeId];
                }
            }

            try
            {
                string response = await MakeMixerHttpRequest($"api/v1/types/{typeId}");
                string name = JsonConvert.DeserializeObject<MixerType>(response).Name;
                lock(m_gameNameCache)
                {
                    m_gameNameCache[typeId] = name;
                }
                return name;
            }
            catch(Exception e)
            {
                Logger.Error($"Failed to get game name for {typeId}: {e.Message}");
            }
            return "Unknown";
        }

        public static async Task<int> GetChannelId(string channelName)
        {
            try
            {
                string response = await MakeMixerHttpRequest($"api/v1/channels/{channelName}");
                return JsonConvert.DeserializeObject<MixerChannel>(response).Id;                
            }
            catch (Exception e)
            {
                Logger.Error($"Failed to get channel id for {channelName}: {e.Message}");
            }
            return 0;
        }

        public async static Task<string> MakeMixerHttpRequest(string url)
        {
            int rateLimitBackoff = 1;
            int i = 0;
            while (i < 1000)
            {
                HttpRequestMessage request = new HttpRequestMessage();
                request.RequestUri = new Uri($"https://mixer.com/{url}");

                HttpResponseMessage response = await s_client.SendAsync(request);
                if (response.StatusCode == (HttpStatusCode)429)
                {
                    // If we get rate limited wait for a while.
                    int backoffMs = 500 * (int)Math.Pow(rateLimitBackoff, 2);
                    Logger.Info($"URL backing off for {backoffMs}ms, URL:{url}");
                    rateLimitBackoff++;
                    await Task.Delay(backoffMs);

                    // And try again.
                    continue;
                }
                else if (response.StatusCode != HttpStatusCode.OK)
                {
                    throw new Exception($"Mixer backend returned status code {response.StatusCode}");
                }
                return await response.Content.ReadAsStringAsync();
            }
            return String.Empty;
        }
    }
}
