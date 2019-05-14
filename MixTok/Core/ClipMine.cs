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
        ReaderWriterLockSlim m_clipMineLock = new ReaderWriterLockSlim(LockRecursionPolicy.SupportsRecursion);
        Dictionary<string, MixerClip> m_clipMine = new Dictionary<string, MixerClip>();
        object m_viewCountSortedLock = new object(); // We use these lock objects since we swap the list obejcts.
        object m_mixTockSortedLock = new object();
        object m_mostRecentSortedLock = new object();
        LinkedList<MixerClip> m_viewCountSortedList = new LinkedList<MixerClip>();
        LinkedList<MixerClip> m_mixTockSortedList = new LinkedList<MixerClip>();
        LinkedList<MixerClip> m_mostRecentList = new LinkedList<MixerClip>();
        DateTime m_lastUpdateTime = DateTime.Now;
        TimeSpan m_lastUpdateDuration = new TimeSpan(0);
        DateTime m_lastDatabaseBackup = DateTime.MinValue;
        string m_status;
        TimeSpan m_statusDuration = new TimeSpan(0);
        DateTime m_statusDurationSet = DateTime.MinValue;

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

            SetStatus($"Indexing {newClips.Count} new clips...");

            {
                // Lock the dictionary for writing so we make sure no one reads or writes while we are updating.
                m_clipMineLock.EnterWriteLock();

                // Set all channels to offline and remove old clips.
                OfflineAndCleanUpClipMine();

                // Add all of the new clips.
                AddOrUpdateClips(newClips);

                m_clipMineLock.ExitWriteLock();
            }

            {
                // The cooking operations are all ready-only, so use the read only lock.
                m_clipMineLock.EnterReadLock();

                // Update
                UpdateCookedData();

                m_clipMineLock.ExitReadLock();
            }

            m_lastUpdateTime = DateTime.Now;
            m_lastUpdateDuration = (m_lastUpdateTime - start) + updateDuration;

            // Check if we should write our current database as a backup.
            if(!isRestore && DateTime.Now - m_lastDatabaseBackup > new TimeSpan(0, 30, 0))
            {
                m_historian.BackupCurrentDb(m_clipMine, this, c_databaseVersion);
                m_lastDatabaseBackup = DateTime.Now;
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
            SetStatus($"Updating MixTok ranks...");

            // Update the mixtock rank on all the clips we know of.
            // We do this for all clips since it effects offline channels.
            UpdateMixTokRanks();

            // Update the view count sorted list.
            // First build a temp list outside of lock and then swap them.
            LinkedList<MixerClip> temp = BuildList(m_clipMine, ClipMineSortTypes.ViewCount, "view count");
            lock (m_viewCountSortedLock)
            {
                m_viewCountSortedList = temp;
            }

            // Update the mixtok sorted list.
            temp = BuildList(m_clipMine, ClipMineSortTypes.MixTokRank, "Mixtok rank");
            lock (m_mixTockSortedLock)
            {
                m_mixTockSortedList = temp;
            }
            
            // Update the most recent list.
            temp = BuildList(m_clipMine, ClipMineSortTypes.MostRecent, "most recent");
            lock (m_mostRecentSortedLock)
            {
                m_mostRecentList = temp;
            }

            SetStatus($"Cooking data done:  {Util.FormatTime(DateTime.Now - start)}", new TimeSpan(0, 0, 10));
            Logger.Info($"Cooking data done: {DateTime.Now - start}");
        }

        private LinkedList<MixerClip> BuildList(Dictionary<string, MixerClip> db, ClipMineSortTypes type, string indexType)
        {
            int count = 0;
            LinkedList<MixerClip> tempList = new LinkedList<MixerClip>();
            foreach (KeyValuePair<string, MixerClip> p in db)
            {
                InsertSort(ref tempList, p.Value, type);

                // Do to issues with API perf while we are sorting, we need to manually yield the thread to 
                // give the APIs time to process.
                count++;
                if (count % 1000 == 0)
                {
                    SetStatus($"Cooking {indexType} [{String.Format("{0:n0}", count)}/{String.Format("{0:n0}", db.Count)}]...");
                    Thread.Sleep(5);
                }
            }
            return tempList;
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
            object lockObj;
            switch(sortType)
            {
                default:
                case ClipMineSortTypes.ViewCount:
                    list = m_viewCountSortedList;
                    lockObj = m_viewCountSortedLock;
                    break;
                case ClipMineSortTypes.MixTokRank:
                    list = m_mixTockSortedList;
                    lockObj = m_mixTockSortedLock;
                    break;
                case ClipMineSortTypes.MostRecent:
                    list = m_mostRecentList;
                    lockObj = m_mostRecentSortedLock;
                    break;
            }

            List<MixerClip> output = new List<MixerClip>();
            // Lock the list so it doesn't change while we are using it.
            lock(lockObj)
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
                double decayedRank = viewRank / (Math.Pow(age.TotalDays, 1.5));

                clip.MixTokRank = decayedRank;
            }
        }

        public int GetClipsCount()
        {
            int result = 0;
            if(m_clipMineLock.TryEnterReadLock(5))
            {
                result = m_clipMine.Count;
                m_clipMineLock.ExitReadLock();
            }
            return result;
        }

        public Tuple<int, int> GetChannelCount()
        {
            Tuple<int, int> result = new Tuple<int, int>(0,0);
            if (m_clipMineLock.TryEnterReadLock(5))
            {
                Dictionary<int, bool> channelMap = new Dictionary<int, bool>();
                foreach (KeyValuePair<string, MixerClip> p in m_clipMine)
                {
                    if (!channelMap.ContainsKey(p.Value.Channel.Id))
                    {
                        channelMap.Add(p.Value.Channel.Id, p.Value.Channel.Online);
                    }
                }
                int online = 0;
                foreach (KeyValuePair<int, bool> p in channelMap)
                {
                    if (p.Value)
                    {
                        online++;
                    }
                }
                result = new Tuple<int, int>(channelMap.Count, online);

                m_clipMineLock.ExitReadLock();
            }
            return result;
        }

        public int ClipsCreatedInLastTime(TimeSpan ts)
        {
            int result = 0;
            if (m_clipMineLock.TryEnterReadLock(5))
            {
                DateTime now = DateTime.UtcNow;
                foreach (KeyValuePair<string, MixerClip> p in m_clipMine)
                {
                    if (now - p.Value.UploadDate < ts)
                    {
                        result++;
                    }
                }
                m_clipMineLock.ExitReadLock();
            }
            return result;   
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

        public void SetStatus(string str, TimeSpan? duration = null)
        {
            // Check if we have a lingering message
            if(!duration.HasValue)
            {
                if((DateTime.Now - m_statusDurationSet) < m_statusDuration)
                {
                    return;
                }
            }

            m_status = str;

            // Set the new duration if not.
            if(duration.HasValue)
            {
                m_statusDurationSet = DateTime.Now;
                m_statusDuration = duration.Value;
            }
        }

        public string GetStatus()
        {
            return m_status;
        }
    }
}
