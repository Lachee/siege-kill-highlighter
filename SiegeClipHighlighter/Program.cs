using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Newtonsoft.Json;
using SiegeClipHighlighter.Configuration;
using SiegeClipHighlighter.Mixer;

namespace SiegeClipHighlighter
{
    class Program
    {
        const string SETTINGS_FILENAME = "settings.json";
        const string FUCKING_TESSERACT = @"";

        //ID of games
        //https://dev.mixer.com/rest/index.html#types_get
        //https://mixer.com/api/v1/types?where=name:eq:Tom%20Clancy%27s%20Rainbow%20Six%20Siege

        private static HttpClient downloader;
        private static MixerClient mixer;
        private static Settings settings;
        private static Stopwatch stopwatch;

        private static string clipDirectory = "";

        private static byte[] downloadBuffer = new byte[16384];

        /// <summary>
        /// Saves the settings
        /// </summary>
        static void SaveSettings()
        {
            var json = JsonConvert.SerializeObject(settings, Formatting.Indented);
            File.WriteAllText(SETTINGS_FILENAME, json);
        }

        static void Main(string[] args)
        {
            var settingsJson = File.Exists(SETTINGS_FILENAME) ? File.ReadAllText(SETTINGS_FILENAME) : "{}";
            settings = JsonConvert.DeserializeObject<Settings>(settingsJson);
            SaveSettings();

            clipDirectory = settings.ClipDirectory + "/" + DateTime.UtcNow.ToFileTimeUtc();
            Directory.CreateDirectory(clipDirectory);

            stopwatch = new Stopwatch();
            mixer = new MixerClient();
            downloader = new HttpClient();

            foreach (var keypair in settings.Channels)
            {
                Console.WriteLine("Processing " + keypair.Key);
                stopwatch.Reset();
                ProcessChannel(keypair.Value).Wait();
                Console.WriteLine("Took {0} minutes", stopwatch.ElapsedMilliseconds / 1000 / 60);
            }    

        }

        static async Task ProcessChannel(Channel channel)
        {
            //Get the records
            Console.WriteLine("Processing Channel " + channel.ChannelId);
            var records = await mixer.GetRecordingsAsync(channel.ChannelId, settings.GameType, channel.LastRecording);

            foreach (var record in records)
            {
                //Get the file
                string url = record.GetMP4Url();
                string path = Path.Combine(settings.TempDirectory, record.ID + ".source.mp4");
                Console.WriteLine("Channel Video {0} : {1}", record.ID, path);

                try
                {
                    //If the file doesn't exist download it
                    if (!File.Exists(path))
                    {
                        Console.WriteLine("Downloading....");
                        var downloadStopWatch = Stopwatch.StartNew();
                        ulong downloadedBytes = 0;
                        using (var stream = await downloader.GetStreamAsync(url))
                        {
                            using (var file = new FileStream(path, FileMode.OpenOrCreate, FileAccess.Write))
                            {
                                int read;
                                while ((read = await stream.ReadAsync(downloadBuffer, 0, downloadBuffer.Length)) > 0)
                                {
                                    downloadedBytes += (ulong)read;
                                    await file.WriteAsync(downloadBuffer, 0, read);
                                }
                            }
                        }
                        Console.WriteLine("Downloaded {0}MB, took {2}s", downloadedBytes / 1024 / 1024, downloadStopWatch.Elapsed.TotalSeconds);
                    }

                    //Updated our last recorded channel
                    channel.LastRecording = record.CreatedAt;

                    //Perform the clipping
                    using (var trimmer = new Trimmer(settings.Tesseract) { 
                        Prefix                  = record.ID + ".", 
                        DeleteTemporaryFiles    = settings.DeleteTemporaryFiles, 
                        TemporaryFileDirectory  = settings.TempDirectory 
                    })
                    {
                        var highlights = trimmer.GenerateClips(path, clipDirectory, channel.SiegeName);
                        var json = JsonConvert.SerializeObject(highlights);
                        File.WriteAllText(clipDirectory + "/" + record.ID + ".json", json);
                    }

                    //Save the settings once we are done.
                    SaveSettings();
                }
                finally
                {
                    //Delete the temporary file, fuck storing your shit gameplay amirite
                    if (settings.DeleteTemporaryFiles && File.Exists(path))
                        File.Delete(path);
                }
            }
            Console.WriteLine("DONE");

            //await Task.Run(() => Downloader.Download(records.FirstOrDefault().GetMP4Url(), "potato", 4));
        }

        static void dumb() { 
            //var games = client.GetRecordingsAsync(55075068, 20097).Result;
            //Console.WriteLine(games[0].GetMP4Url());


            string input = "D:/source_kills.mp4";
            string output = "clips";
            string username = "KommadantKlink";
            string jsonFile = "trimmings-" + username + ".json";

            using (var trimmer = new Trimmer(FUCKING_TESSERACT))
            {
                var highlights = trimmer.GenerateClips(input, output, username);

                /*
                if (File.Exists(jsonFile))
                {
                    Console.WriteLine("Finding Existing Highlights");
                    var json = File.ReadAllText(jsonFile);
                    var highlights = JsonConvert.DeserializeObject<Trimmer.Highlight[]>(json);
                    trimmer.GenerateClips(highlights, input, output, true);
                }
                else
                {
                    Console.WriteLine("Generating New Highlights");
                    var highlights = trimmer.GenerateClips(input, output, username);
                    var json = JsonConvert.SerializeObject(highlights);
                    File.WriteAllText("trimmings-" + username + ".json", json);
                }
                */
            }

            Console.WriteLine("DONE");
        }
    }
}
