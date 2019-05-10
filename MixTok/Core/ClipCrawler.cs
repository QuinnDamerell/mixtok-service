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
            m_updater.Start();
        }

        private async void UpdateThread()
        {
            while(true)
            {
                bool updateFailed = false;
                try
                {
                    // Update
                    DateTime start = DateTime.Now;
                    List<MixerClip> clips = await GetTockClips();

                    Program.s_ClipMine.SetStatus($"Indexing {clips.Count} new clips...");
                    m_adder.AddToClipMine(clips, DateTime.Now - start, false);
                    updateFailed = false;
                }
                catch(Exception e)
                {
                    Program.s_ClipMine.SetStatus($"<strong>Failed to update clips!</strong> "+e.Message + "; stack: "+e.StackTrace);
                    Logger.Error($"Failed to update!", e);
                    updateFailed = true;
                }

                // After we successfully get clips,
                // update every 5 minutes
                DateTime nextUpdate = DateTime.Now.AddMinutes(5);
                while(nextUpdate > DateTime.Now)
                {
                    string str = "Next update in ";
                    TimeSpan diff = nextUpdate - DateTime.Now;
                    if(diff.TotalMinutes > 0)
                    {
                        str += $"{Math.Round(diff.TotalMinutes, 2)} mins";
                    }
                    else
                    {
                        str += $"{Math.Round(diff.TotalSeconds, 2)} secs";
                    }
                    // Don't set the text if we are showing an error.
                    if (!updateFailed)
                    {
                        Program.s_ClipMine.SetStatus(str);
                    }
                    Thread.Sleep(5000);
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
            Logger.Info($"Found {channels.Count} online channels in {(DateTime.Now - start)}");
            Program.s_ClipMine.SetStatus($"Getting clip data 0/{channels.Count}...");

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
                if(count % 10 == 0)
                {
                    Logger.Info($"Got {count}/{channels.Count} channel clips...");
                    Program.s_ClipMine.SetStatus($"Getting clip data {count}/{channels.Count}...");                    
                }
            }

            Logger.Info($"Found {count} clips in {(DateTime.Now - start)}");
            return clips;
        }
    }
}
