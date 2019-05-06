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
    }

    public class MixerChannel
    {
        public int Id;
        public int ViewersCurrent;
        public bool Online;
        public int UserId;
        public string ChannelLogo;
        public bool VodsEnabled;
        public MixerUser User;

        [JsonProperty("languageId")]
        public string Language;

        [JsonProperty("token")]
        public string Name;

        public List<MixerClip> Clips = new List<MixerClip>();
    }

    public class ClipContent
    { 
        public string LocatorType;
        public string Uri;
    }

    public class MixerClip
    {
        public int TypeId;
        public string Title;
        public int ViewCount;
        public string ContentId;
        public string ShareableId;
        public DateTime UploadDate;
        public List<ClipContent> ContentLocators;
    }

    public class MixerApis
    {
        static HttpClient s_client = new HttpClient();

        public static async Task<List<MixerChannel>> GetOnlineChannels(int viwersInclusiveLimit = 5, string languageFilter = null)
        {
            List<MixerChannel> channels = new List<MixerChannel>();
            int i = 0;
            while (i < 1000)
            {
                try
                {
                    string response = await MakeMixerHttpRequest($"api/v1/channels?limit=100&page={i}&order=online:desc,viewersCurrent:desc&fields=token,id,viewersCurrent,online,userId,user,languageId,vodsEnabled");
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
                if(!String.IsNullOrWhiteSpace(languageFilter) && !String.IsNullOrWhiteSpace(chan.Language) && !chan.Language.Equals(languageFilter))
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
            return JsonConvert.DeserializeObject<List<MixerClip>>(response);
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
