using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace MixTok.Core
{
    public interface IClipMineAdder
    {
        void AddToClipMine(List<MixerClip> newClips, TimeSpan updateDuration, bool isRestore);
    }

    public enum ClipMineSortTypes
    {
        ViewCount = 0,
        MixTokRank = 1,
        MostRecent = 2
    }

    public class ClipMine : IClipMineAdder
    {
        // This value is used for the save / restore.
        // If anything in any of the objects change, this should be updated.
        const int c_databaseVersion = 1;

        Historian m_historian;
        ClipCrawler m_crawler;
        Dictionary<string, MixerClip> m_clipMine = new Dictionary<string, MixerClip>();
        LinkedList<MixerClip> m_viewCountSortedList = new LinkedList<MixerClip>();
        LinkedList<MixerClip> m_mixTockSortedList = new LinkedList<MixerClip>();
        LinkedList<MixerClip> m_mostRecentList = new LinkedList<MixerClip>();
        DateTime m_lastUpdateTime = DateTime.Now;
        TimeSpan m_lastUpdateDuration = new TimeSpan(0);
        DateTime m_lastDatabaseBackup = DateTime.MinValue;
        string m_status;

        public ClipMine()
        {
            m_historian = new Historian();
        }

        public void Start()
        {
            // Start a worker
            Thread worker = new Thread(() =>
            {
                // Ask the historian to try to restore our in
                // memory database from a previous database.
                m_historian.AttemptToRestore(this, c_databaseVersion);

                // Now start the normal miner.
                m_crawler = new ClipCrawler(this);
            });
            worker.Start();
        }

        public void AddToClipMine(List<MixerClip> newClips, TimeSpan updateDuration, bool isRestore)
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

                // Check if we should write our current database as a backup.
                if(!isRestore && DateTime.Now - m_lastDatabaseBackup > new TimeSpan(0, 30, 0))
                {
                    m_historian.BackupCurrentDb(m_clipMine, this, c_databaseVersion);
                    m_lastDatabaseBackup = DateTime.Now;
                }
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
                foreach (KeyValuePair<string, MixerClip> p in m_clipMine)
                {
                    InsertSort(ref m_viewCountSortedList, p.Value, ClipMineSortTypes.ViewCount);
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
                    InsertSort(ref m_mixTockSortedList, p.Value, ClipMineSortTypes.MixTokRank);
                }
            }

            // Update the mixtok rank sorted list.
            lock (m_mostRecentList)
            {
                // To update the list, delete everything we had and rebuild it
                // based on the new mine.
                m_mostRecentList.Clear();
                foreach (KeyValuePair<string, MixerClip> p in m_clipMine)
                {
                    InsertSort(ref m_mostRecentList, p.Value, ClipMineSortTypes.MostRecent);
                }
            }            

            Logger.Info($"Cooking data done: {DateTime.Now - start}");
        }

        private void InsertSort(ref LinkedList<MixerClip> list, MixerClip c, ClipMineSortTypes type)
        {
            LinkedListNode<MixerClip> node = list.First;
            while(node != null)
            {
                bool result = false;
                switch(type)
                {
                    case ClipMineSortTypes.MostRecent:
                        result = c.UploadDate > node.Value.UploadDate;
                        break;
                    case ClipMineSortTypes.MixTokRank:
                        result = c.MixTokRank > node.Value.MixTokRank;
                        break;
                    default:
                    case ClipMineSortTypes.ViewCount:
                        result = c.ViewCount > node.Value.ViewCount;
                        break;
                }
                if (result)
                {
                    list.AddBefore(node, c);
                    return;
                }
                node = node.Next;
            }
            list.AddLast(c);
        }

        public List<MixerClip> GetClips(ClipMineSortTypes sortType,
            int limit = 100,
            DateTime? fromTime = null, DateTime? toTime = null,    
            int? ViewCountMin = null,
            int? channelIdFilter = null, string channelName = null, int? hypeZoneChannelId = null,
            bool? currentlyLive = null, bool? partnered = null,
            string gameTitle = null, int? gameId = null,
            string languageFilter = null)
        {
            // Get the pre-sorted list we want.
            LinkedList<MixerClip> list;
            switch(sortType)
            {
                default:
                case ClipMineSortTypes.ViewCount:
                    list = m_viewCountSortedList;
                    break;
                case ClipMineSortTypes.MixTokRank:
                    list = m_mixTockSortedList;
                    break;
                case ClipMineSortTypes.MostRecent:
                    list = m_mostRecentList;
                    break;
            }

            List<MixerClip> output = new List<MixerClip>();
            // Lock the list so it doesn't change while we are using it.
            lock(list)
            {
                // Go through the current sorted list from the highest to the lowest.
                // Apply the filtering and then build the output list.
                LinkedListNode<MixerClip> node = list.First;
                while(output.Count < limit && node != null)
                {
                    // Get the node and advance here, becasue this will continue early
                    // if the search filters it out.
                    MixerClip c = node.Value;
                    node = node.Next;

                    if(channelIdFilter.HasValue)
                    {
                        // Check if this is the channel we want.
                        if (c.Channel.Id != channelIdFilter.Value)
                        {
                            continue;
                        }
                    }
                    if(!String.IsNullOrWhiteSpace(channelName))
                    {
                        // Check if the channel name has the current filter.
                        if(c.Channel.Name.IndexOf(channelName, 0, StringComparison.InvariantCultureIgnoreCase) == -1)
                        {
                            continue;
                        }
                    }
                    if(!String.IsNullOrWhiteSpace(gameTitle))
                    {
                        // Check if the game title has the current filter string.
                        if(c.GameTitle.IndexOf(gameTitle, 0, StringComparison.InvariantCultureIgnoreCase) == -1)
                        {
                            continue;
                        } 
                    }
                    if(gameId.HasValue)
                    {
                        if(c.TypeId != gameId)
                        {
                            continue;
                        }
                    }
                    if(fromTime.HasValue)
                    {
                        // Check if this is in the time range we want.
                        if(c.UploadDate < fromTime.Value)
                        {
                            continue;
                        }
                    }
                    if (toTime.HasValue)
                    {
                        // Check if this is in the time range we want.
                        if (c.UploadDate > toTime.Value)
                        {
                            continue;
                        }
                    }
                    if(ViewCountMin.HasValue)
                    {
                        if(c.ViewCount < ViewCountMin)
                        {
                            continue;
                        }
                    }
                    if(partnered.HasValue)
                    {
                        if(partnered.Value != c.Channel.Partnered)
                        {
                            continue;
                        }
                    }
                    if(currentlyLive.HasValue)
                    {
                        if(currentlyLive.Value != c.Channel.Online)
                        {
                            continue;
                        }
                    }
                    if(hypeZoneChannelId.HasValue)
                    {
                        if(hypeZoneChannelId.Value != c.HypeZoneChannelId)
                        {
                            continue;
                        }
                    }
                    if(!String.IsNullOrWhiteSpace(languageFilter))
                    {
                        if(!c.Channel.Language.Equals(languageFilter, StringComparison.OrdinalIgnoreCase))
                        {
                            continue;
                        }
                    }

                    // If we got to then end this didn't get filtered.
                    // So add it.
                    output.Add(c);                    
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

        public DateTime GetLastBackupTime()
        {
            return m_lastDatabaseBackup;
        }

        public void SetStatus(string str)
        {
            m_status = str;
        }

        public string GetStatus()
        {
            return m_status;
        }
    }
}
