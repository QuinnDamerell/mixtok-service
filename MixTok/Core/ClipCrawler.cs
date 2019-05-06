using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace MixTok.Core
{
    public class ClipCrawler
    {
        IClipMineAdder m_adder;
        Thread m_updater;


        public ClipCrawler(IClipMineAdder adder)
        {
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
                    List<MixerChannel> channels = await GetTockClips();
                    m_adder.AddToClipMine(channels);
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
     
        private async Task<List<MixerChannel>> GetTockClips()
        {
            // Get the online channels
            DateTime start = DateTime.Now;

            // We must limit how many channels we pull, so we will only get channels with at least 2 viewers.
            List<MixerChannel> channels = await MixerApis.GetOnlineChannels(2, null);
            Logger.Info($"Found {channels.Count} online channels in {(DateTime.Now - start)}");

            // Get the clips for the channels
            start = DateTime.Now;
            int count = 0;
            foreach (var chan in channels)
            {
                try
                {
                    chan.Clips = await MixerApis.GetClips(chan.Id);                    
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
            return channels;
        }
    }
}
