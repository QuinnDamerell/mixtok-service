using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace MixTok.Core
{
    public class ClipCrawler
    {
        int MinViewerCount = 2;

        IClipMineAdder m_adder;
        Thread m_updater;


        public ClipCrawler(IClipMineAdder adder)
        {
            // For local testing, set the min view count to be higher.
            var test = Environment.GetEnvironmentVariables();
            string var = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT");
            if(!String.IsNullOrWhiteSpace(var) && var == "Development")
            {
                MinViewerCount = 500;
            }

            m_adder = adder;
            m_updater = new Thread(UpdateThread);
            m_updater.Priority = ThreadPriority.BelowNormal;
            m_updater.Start();
        }

        private async void UpdateThread()
        {
            while(true)
            {
                try
                {
                    // Update
                    DateTime start = DateTime.Now;
                    List<MixerClip> clips = await GetTockClips();

                    m_adder.AddToClipMine(clips, DateTime.Now - start, false);
                }
                catch(Exception e)
                {
                    Program.s_ClipMine.SetStatus($"<strong>Failed to update clips!</strong> "+e.Message + "; stack: "+e.StackTrace, new TimeSpan(0, 0, 30));
                    Logger.Error($"Failed to update!", e);
                }

                // After we successfully get clips,
                // update every 5 minutes
                DateTime nextUpdate = DateTime.Now.AddMinutes(5);
                while(nextUpdate > DateTime.Now)
                {
                    // Don't set the text if we are showing an error.
                    Program.s_ClipMine.SetStatus($"Next update in {Util.FormatTime(nextUpdate - DateTime.Now)}");
                    Thread.Sleep(500);
                }
            }
        }
     
        private async Task<List<MixerClip>> GetTockClips()
        {
            // Get the online channels
            DateTime start = DateTime.Now;

            Program.s_ClipMine.SetStatus($"Finding online channels...");

            // We must limit how many channels we pull, so we will only get channels with at least 2 viewers.
            List<MixerChannel> channels = await MixerApis.GetOnlineChannels(MinViewerCount, null);
            Logger.Info($"Found {channels.Count} online channels in {Util.FormatTime(DateTime.Now - start)}");
            Program.s_ClipMine.SetStatus($"Found {Util.FormatInt(channels.Count)} online channels in {Util.FormatTime(DateTime.Now - start)}", new TimeSpan(0, 0, 10));

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
                if(count % 5 == 0)
                {
                    //Logger.Info($"Got {count}/{channels.Count} channel clips...");
                    Program.s_ClipMine.SetStatus($"Getting clip data [{Util.FormatInt(count)}/{Util.FormatInt(channels.Count)}]...");                    
                }
            }

            Logger.Info($"Found {count} clips in {(DateTime.Now - start)}");
            Program.s_ClipMine.SetStatus($"Found {Util.FormatInt(count)} clips in {Util.FormatTime(DateTime.Now - start)}", new TimeSpan(0, 0, 10));

            return clips;
        }
    }
}
