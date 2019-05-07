using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace MixTok.Core
{
    public class ClipCrawler
    {
        static int s_MinViewerCount = 2;

        IClipMineAdder m_adder;
        Thread m_updater;


        public ClipCrawler(IClipMineAdder adder)
        {
            m_adder = adder;
            m_updater = new Thread(UpdateThread);
            m_updater.Start();
        }

        private async void UpdateThread()
        {
            while(true)
            {
                try
                {
                    // Update
                    List<MixerClip> clips = await GetTockClips();
                    m_adder.AddToClipMine(clips);
                }
                catch(Exception e)
                {
                    Logger.Error($"Failed to update!", e);
                } 

                // After we successfully get clips,
                // update every 5 minutes
                Thread.Sleep(300 * 1000);
            }
        }
     
        private async Task<List<MixerClip>> GetTockClips()
        {
            // Get the online channels
            DateTime start = DateTime.Now;

            // We must limit how many channels we pull, so we will only get channels with at least 2 viewers.
            List<MixerChannel> channels = await MixerApis.GetOnlineChannels(s_MinViewerCount, null);
            Logger.Info($"Found {channels.Count} online channels in {(DateTime.Now - start)}");

            // Get the clips for the channels
            List<MixerClip> clips = new List<MixerClip>();
            start = DateTime.Now;
            int count = 0;
            foreach (var chan in channels)
            {
                try
                {
                    // Get the clips for this channel.
                    List<MixerClip>  channelClips = await MixerApis.GetClips(chan.Id);

                    // For each clip, attach the most recent channel object.
                    foreach(MixerClip c in channelClips)
                    {
                        c.Channel = chan;
                    }

                    // Add the clips to our output list.
                    clips.AddRange(channelClips);                   
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

            Logger.Info($"Found {count} clips in {(DateTime.Now - start)}");
            return clips;
        }
    }
}
