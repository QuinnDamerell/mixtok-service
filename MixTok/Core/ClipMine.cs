using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace MixTok.Core
{
    public interface IClipMineAdder
    {
        void AddToClipMine(List<MixerClip> newClips, TimeSpan updateDuration);
    }

    public enum ClipMineSortTypes
    {
        ViewCount = 0,
        MixTokRank = 1,
    }

    public class ClipMine : IClipMineAdder
    {
        ClipCrawler m_crawler;
        Dictionary<string, MixerClip> m_clipMine = new Dictionary<string, MixerClip>();
        List<MixerClip> m_viewCountSortedList = new List<MixerClip>();
        List<MixerClip> m_mixTockSortedList = new List<MixerClip>();
        DateTime m_lastUpdateTime = DateTime.Now;
        TimeSpan m_lastUpdateDuration = new TimeSpan(0);

        public ClipMine()
        {
            m_crawler = new ClipCrawler(this);
        }

        public void AddToClipMine(List<MixerClip> newClips, TimeSpan updateDuration)
        {
            DateTime start = DateTime.Now;

            // Lock the dictionary so we make sure no one reads or writes while we are updating.
            lock(m_clipMine)
            {
                // Set all channels to offline and remove old clips.
                OfflineAndCleanUpClipMine();

                // Add all of the new clips.
                AddOrUpdateClips(newClips);

                // Update
                UpdateCookedData();

                m_lastUpdateTime = DateTime.Now;
                m_lastUpdateDuration = (m_lastUpdateTime - start) + updateDuration;
            }
        }

        // Needs to be called under lock!
        private void OfflineAndCleanUpClipMine()
        {
            List<string> toRemove = new List<string>();
            foreach(KeyValuePair<string, MixerClip> p in m_clipMine)
            {
                // Set the channel offline and the view count to 0.
                // When the currently online channel are added these will be
                // updated to the current values.
                p.Value.Channel.Online = false;
                p.Value.Channel.ViewersCurrent = 0;

                // If the clips is expired, remove it.
                if(DateTime.UtcNow > p.Value.ExpirationDate)
                {
                    toRemove.Add(p.Key);
                }
            }

            // Remove old clips
            foreach(string s in toRemove)
            {
                m_clipMine.Remove(s);
            }

            Logger.Info($"Mine cleanup done, removed {toRemove.Count} old clips.");
        }

        // Needs to be called under lock!
        private void AddOrUpdateClips(List<MixerClip> freshClips)
        {
            int added = 0;
            int updated = 0;
            foreach(MixerClip c in freshClips)
            {
                if(m_clipMine.ContainsKey(c.ContentId))
                {
                    // The clips already exists, update the clip and channel info
                    // from this newer data.
                    m_clipMine[c.ContentId].UpdateFromNewer(c);
                    updated++;
                }
                else
                {
                    // The clip doesn't exist, add it.
                    m_clipMine.Add(c.ContentId, c);
                    added++;
                }
            }
            Logger.Info($"Clip update done; {added} added, {updated} updated.");
        }
         
        // Needs to be called under lock!
        private void UpdateCookedData()
        {
            DateTime start = DateTime.Now;

            // Update the mixtock rank on all the clips we know of.
            // We do this for all clips since it effects offline channels.
            UpdateMixTokRanks();

            // Update the view count sorted list.
            lock (m_viewCountSortedList)
            {
                // To update the list, delete everything we had and rebuild it
                // based on the new mine.
                m_viewCountSortedList.Clear();
                foreach(KeyValuePair<string, MixerClip> p in m_clipMine)
                {
                    InsertSort(ref m_viewCountSortedList, p.Value, true);
                }
            }    

            // Update the mixtok rank sorted list.
            lock (m_mixTockSortedList)
            {
                // To update the list, delete everything we had and rebuild it
                // based on the new mine.
                m_mixTockSortedList.Clear();
                foreach (KeyValuePair<string, MixerClip> p in m_clipMine)
                {
                    InsertSort(ref m_mixTockSortedList, p.Value, false);
                }
            }

            Logger.Info($"Cooking data done: {DateTime.Now - start}");
        }

        private void InsertSort(ref List<MixerClip> list, MixerClip c, bool useViewCount)
        {
            for (int i = 0; i < list.Count; i++)
            {
                bool result = useViewCount ? (c.ViewCount > list[i].ViewCount) : (c.MixTokRank > list[i].MixTokRank);
                if (result)
                {
                    list.Insert(i, c);
                    return;
                }
            }
            list.Add(c);
        }

        public List<MixerClip> GetClips(ClipMineSortTypes sortType,
            int limit = 100,
            DateTime? fromTime = null, DateTime? toTime = null,    
            int? viewCoutMin = null,
            int? channelIdFilter = null, int? hypeZoneChannelId = null,
            bool? currentlyLive = null, bool? partnered = null,
            string gameTitle = null, int? gameId = null,
            string languageFilter = null)
        {
            // Get the pre-sorted list we want.
            List<MixerClip> list;
            switch(sortType)
            {
                default:
                case ClipMineSortTypes.ViewCount:
                    list = m_viewCountSortedList;
                    break;
                case ClipMineSortTypes.MixTokRank:
                    list = m_mixTockSortedList;
                    break;
            }

            List<MixerClip> output = new List<MixerClip>();
            // Lock the list so it doesn't change while we are using it.
            lock(list)
            {
                // Go through the current sorted list from the highest to the lowest.
                // Apply the filtering and then build the output list.
                int currentListPos = 0;
                while(output.Count < limit && currentListPos < list.Count)
                {
                    bool addToOutput = true;
                    MixerClip c = list[currentListPos];
                    currentListPos++;

                    if (channelIdFilter.HasValue)
                    {
                        // Check if this is the channel we want.
                        if (c.Channel.Id != channelIdFilter.Value)
                        {
                            addToOutput = false;
                        }
                    }
                    if(!String.IsNullOrWhiteSpace(gameTitle))
                    {
                        // Check if the game title has the current filter string.
                        if(c.GameTitle.IndexOf(gameTitle, 0, StringComparison.InvariantCultureIgnoreCase) == -1)
                        {
                            addToOutput = false;
                        }
                    }
                    if(fromTime.HasValue)
                    {
                        // Check if this is in the time range we want.
                        if(c.UploadDate < fromTime.Value)
                        {
                            addToOutput = false;
                        }
                    }
                    if (toTime.HasValue)
                    {
                        // Check if this is in the time range we want.
                        if (c.UploadDate > toTime.Value)
                        {
                            addToOutput = false;
                        }
                    }
                    if(viewCoutMin.HasValue)
                    {
                        if(c.ViewCount < viewCoutMin)
                        {
                            addToOutput = false;
                        }
                    }
                    if(partnered.HasValue)
                    {
                        if(partnered.Value != c.Channel.Partnered)
                        {
                            addToOutput = false;
                        }
                    }
                    if(currentlyLive.HasValue)
                    {
                        if(currentlyLive.Value != c.Channel.Online)
                        {
                            addToOutput = false;
                        }
                    }
                    if(hypeZoneChannelId.HasValue)
                    {
                        if(hypeZoneChannelId.Value != c.HypeZoneChannelId)
                        {
                            addToOutput = false;
                        }
                    }
                    if(!String.IsNullOrWhiteSpace(languageFilter))
                    {
                        if(!c.Channel.Language.Equals(languageFilter, StringComparison.OrdinalIgnoreCase))
                        {
                            addToOutput = false;
                        }
                    }

                    // Add if if we want.
                    if (addToOutput)
                    {
                        output.Add(c);
                    }
                }
            }
            return output;
        }
                     
        private void UpdateMixTokRanks()
        {
            // The min age a clip can be.
            TimeSpan s_minClipAge = new TimeSpan(0, 10, 0);

            // For each clip, update the rank
            DateTime nowUtc = DateTime.UtcNow;
            foreach (KeyValuePair<string, MixerClip> p in m_clipMine)
            {
                MixerClip clip = p.Value;

                // Compute the view rank
                double viewRank = (double)clip.ViewCount;

                // Decay the view rank by time
                TimeSpan age = (nowUtc - clip.UploadDate);

                // Clamp by the min age to give all clips some time
                // to pick up viewers.
                if(age < s_minClipAge)
                {
                    age = s_minClipAge;
                }
                double decayedRank = viewRank / (age.TotalDays * 2);

                clip.MixTokRank = decayedRank;
            }
        }

        public int GetClipsCount()
        {
            lock(m_clipMine)
            {
                return m_clipMine.Count;
            }
        }

        public Tuple<int, int> GetChannelCount()
        {
            lock(m_clipMine)
            {
                Dictionary<int, bool> channelMap = new Dictionary<int, bool>();
                foreach(KeyValuePair<string, MixerClip> p in m_clipMine)
                {
                    if(!channelMap.ContainsKey(p.Value.Channel.Id))
                    {
                        channelMap.Add(p.Value.Channel.Id, p.Value.Channel.Online);
                    }
                }
                int online = 0;
                foreach(KeyValuePair<int, bool> p in channelMap)
                {
                    if(p.Value)
                    {
                        online++;
                    }
                }
                return new Tuple<int, int>(channelMap.Count, online);
            }
        }

        public int ClipsCreatedInLastTime(TimeSpan ts)
        {
            lock (m_clipMine)
            {
                int count = 0;
                DateTime now = DateTime.UtcNow;
                foreach (KeyValuePair<string, MixerClip> p in m_clipMine)
                {
                    if(now - p.Value.UploadDate < ts)
                    {
                        count++;
                    }
                }
                return count;
            }
        }

        public DateTime GetLastUpdateTime()
        {
            return m_lastUpdateTime;
        }

        public TimeSpan GetLastUpdateDuration()
        {
            return m_lastUpdateDuration;
        }
    }
}
