using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace MixTok.Core
{
    public interface IClipMineAdder
    {
        void AddToClipMine(List<MixerChannel> newClips);
    }

    public class ClipMine : IClipMineAdder
    {
        ClipCrawler m_crawler;
        Dictionary<int, MixerChannel> m_channelMine = new Dictionary<int, MixerChannel>();

        public ClipMine()
        {
            m_crawler = new ClipCrawler(this);
        }

        public void AddToClipMine(List<MixerChannel> newChannels)
        {
            //
            // Add the new clips to the mine.
            //
            int newChannelCount = 0;
            int newClipCount = 0;
            // Add the clips to channel mine
            foreach(MixerChannel chan in newChannels)
            {
                if(m_channelMine.ContainsKey(chan.Id))
                {
                    // The channel already exists.
                    // Update the channel data

                    // Update or add clip data

                }
                else
                {
                    // New channel!
                    m_channelMine.Add(chan.Id, chan);
                    newChannelCount++;
                    newClipCount = chan.Clips.Count;
                }
            }
            Logger.Info($"{newChannelCount} channels added to the mine and {newClipCount} new clips found!");

            // 
            // Cleanup old data
            //

            // 
            // Cook the data
            //




        }

        //public class TokClip
        //{
        //    public double Rank;
        //    public MixerChannel Channel;

        //    public int Views;
        //    public int TypeId;
        //    public string Title;
        //    public string ClipUrl;
        //    public string ContentId;
        //    public DateTime Created;
        //    public string ShareableUrl;
        //}

        //                            // Convert them into clips
        //                    foreach (var res in results)
        //                    {
        //                        string clipUrl = null;
        //                        foreach(var con in res.ContentLocators)
        //                        {
        //                            if(con.LocatorType.Equals("HlsStreaming"))
        //                            {
        //                                clipUrl = con.Uri;
        //                                break;
        //                            }
        //}

        //                        if(String.IsNullOrWhiteSpace(clipUrl))
        //                        {
        //                            continue;
        //                        }

        //                        clips.Add(new TokClip()
        //{
        //    TypeId = res.TypeId,
        //                            Title = res.Title,
        //                            ContentId = res.ContentId,
        //                            ShareableUrl = $"https://mixer.com/{chan.Id}?clip={res.ShareableId}",
        //                            Channel = chan,
        //                            Created = res.UploadDate,
        //                            Views = res.ViewCount,
        //                            ClipUrl = clipUrl
        //                        });

        //TimeSpan c_maxClipAge = new TimeSpan(168, 0, 0); // 7 days.

        //public List<TokClip> GetClips(int limit = 100)
        //{
        //    lock (m_lock)
        //    {
        //        int l = m_topClips.Count > limit ? limit : m_topClips.Count;
        //        return m_topClips.GetRange(0, l);
        //    }
        //}


        //private List<TokClip> RankClips(List<TokClip> input)
        //{
        //    // Get the max view count.
        //    int viewCountAccum = 0;
        //    int maxViewCount = 0;
        //    foreach (var clip in input)
        //    {
        //        viewCountAccum += clip.Views;
        //        if (maxViewCount < clip.Views)
        //        {
        //            maxViewCount = clip.Views;
        //        }
        //    }
        //    double avgViewCount = (double)viewCountAccum / (double)input.Count;

        //    List<TokClip> output = new List<TokClip>();
        //    DateTime start = DateTime.Now;
        //    foreach (var clip in input)
        //    {
        //        // Compute a rank for this clip.
        //        double viewRank = (double)clip.Views / (double)(100);
        //        TimeSpan age = c_maxClipAge - (start - clip.Created);
        //        double timeRank = (double)(age.TotalSeconds) / (c_maxClipAge.TotalSeconds);
        //        if (timeRank < 0)
        //        {
        //            timeRank = 0;
        //        }

        //        viewRank = Math.Clamp(viewRank, 0.0, 1.0);
        //        viewRank = viewRank / 2;
        //        timeRank = timeRank / 2;

        //        viewRank = Math.Pow(viewRank, 1.8);
        //        timeRank = Math.Pow(timeRank, 1.2);

        //        // Compute the rank
        //        clip.Rank = Math.Clamp(viewRank + timeRank, 0.0, 1.0);

        //        // Add the clip to the output
        //        InsertSort(ref output, clip);
        //    }

        //    return output;
        //}

        //private void InsertSort(ref List<TokClip> list, TokClip c)
        //{
        //    for (int i = 0; i < list.Count; i++)
        //    {
        //        if (c.Rank > list[i].Rank)
        //        {
        //            list.Insert(i, c);
        //            return;
        //        }
        //    }
        //    list.Add(c);
        //}

    }
}
