using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace MixTok.Core
{
    public class TokClip
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

    public class Monitor
    {
        Thread m_updater;
        object m_lock = new object();
        List<TokClip> m_topClips = new List<TokClip>();
        TimeSpan c_maxClipAge = new TimeSpan(168, 0, 0); // 7 days.

        public Monitor()
        {
            m_updater = new Thread(UpdateThread);
            m_updater.Start();
        }

        public List<TokClip> GetClips(int limit = 100)
        {
            lock (m_lock)
            {
                int l = m_topClips.Count > limit ? limit : m_topClips.Count;
                return m_topClips.GetRange(0, l);
            }
        }

        private async void UpdateThread()
        {
            while(true)
            {
                try
                {
                    // Update
                    List<TokClip> clips = RankClips(await GetTockClips());
                    lock(m_lock)
                    {
                        m_topClips = clips;
                    }
                }
                catch(Exception e)
                {
                    Logger.Error($"Failed to update!", e);
                } 

                // Update every 60 seconds.
                Thread.Sleep(300 * 1000);
            }
        }

        private List<TokClip> RankClips(List<TokClip> input)
        {
            // Get the max view count.
            int viewCountAccum = 0;
            int maxViewCount = 0;
            foreach(var clip in input)
            {
                viewCountAccum += clip.Views;
                if(maxViewCount < clip.Views)
                {
                    maxViewCount = clip.Views;
                }
            }
            double avgViewCount = (double)viewCountAccum / (double)input.Count;
            
            List<TokClip> output = new List<TokClip>();
            DateTime start = DateTime.Now;
            foreach(var clip in input)
            {
                // Compute a rank for this clip.
                double viewRank = (double)clip.Views / (double)(100);
                TimeSpan age = c_maxClipAge - (start - clip.Created);
                double timeRank = (double)(age.TotalSeconds) / (c_maxClipAge.TotalSeconds);       
                if(timeRank < 0)
                {
                    timeRank = 0;
                }

                viewRank = Math.Clamp(viewRank, 0.0, 1.0);
                viewRank = viewRank / 2;
                timeRank = timeRank / 2;

                viewRank = Math.Pow(viewRank, 1.8);
                timeRank = Math.Pow(timeRank, 1.2);

                // Compute the rank
                clip.Rank = Math.Clamp(viewRank + timeRank, 0.0, 1.0);

                // Add the clip to the output
                InsertSort(ref output, clip);
            }

            return output;
        }

        private void InsertSort(ref List<TokClip> list, TokClip c)
        {
            for (int i = 0; i < list.Count; i++)
            {
                if (c.Rank > list[i].Rank)
                {
                    list.Insert(i, c);
                    return;
                }
            }
            list.Add(c);
        }

        private async Task<List<TokClip>> GetTockClips()
        {
            // Get the online channels
            DateTime start = DateTime.Now;
            List<MixerChannel> channels = await MixerApis.GetOnlineChannels(5, "en");
            Logger.Info($"Found {channels.Count} online channels in {(DateTime.Now - start)}");

            // Get the clips for the channels
            start = DateTime.Now;
            List<TokClip> clips = new List<TokClip>();
            int count = 0;
            foreach (var chan in channels)
            {
                try
                {
                    List<MixerClip> results = await MixerApis.GetClips(chan.Id);

                    // Conver them into clips
                    foreach (var res in results)
                    {
                        string clipUrl = null;
                        foreach(var con in res.ContentLocators)
                        {
                            if(con.LocatorType.Equals("HlsStreaming"))
                            {
                                clipUrl = con.Uri;
                                break;
                            }
                        }

                        if(String.IsNullOrWhiteSpace(clipUrl))
                        {
                            continue;
                        }

                        clips.Add(new TokClip()
                        {
                            TypeId = res.TypeId,
                            Title = res.Title,
                            ContentId = res.ContentId,
                            ShareableUrl = $"https://mixer.com/{chan.Id}?clip={res.ShareableId}",
                            Channel = chan,
                            Created = res.UploadDate,
                            Views = res.ViewCount,
                            ClipUrl = clipUrl
                        });
                    }
                }
                catch (Exception e)
                {
                    Logger.Error("Failed to get clips for channel " + chan.Name, e);
                }
                count++;
                if(count % 100 == 0)
                {
                    Logger.Info($"Got {count}/{channels.Count} channel clips...");
                }
            }

            Logger.Info($"Found {clips.Count} clips in {(DateTime.Now - start)}");
            return clips;
        }
    }
}
