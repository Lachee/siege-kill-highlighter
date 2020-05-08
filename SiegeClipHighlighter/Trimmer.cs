using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using System.Diagnostics;
using System.IO;
using Newtonsoft.Json;

namespace SiegeClipHighlighter
{
    public class Trimmer : IDisposable
    {
        public int Threads { get; set; } = 4;

        public string HighlightExecutable { get; }
        public double Preamble { get; set; } = 15;
        public double Postamble { get; set; } = 10;
        public double Padding { get; set; } = 10;

        /// <summary>
        /// Prefix for the clip files
        /// </summary>
        public string Prefix { get; set; }

        /// <summary>
        /// The source file
        /// </summary>
        public string SourceFile { get; private set; }

        /// <summary>
        /// The file the cropped version will be stored
        /// </summary>
        public string CroppedFile { get; private set; }

        /// <summary>
        /// Directory the resulting clips are stored
        /// </summary>
        public string ClipDirectory { get; private set; }

        /// <summary>
        /// Delete temporary files after use
        /// </summary>
        public bool DeleteTemporaryFiles { get; set; } = true;

        /// <summary>
        /// Folder temporary files are stored
        /// </summary>
        public string TemporaryFileDirectory { get; set; } = "temp";

        /// <summary>
        /// Name of the user we are trimming for
        /// </summary>
        public string Name { get; private set; }

        public Trimmer(string exe)
        {
            HighlightExecutable = exe;
            if (!File.Exists(exe))
                throw new Exception("Failed to find the highlight executable");
        }

        /// <summary>
        /// Generates highlights from the given input and trims them into individual clips
        /// </summary>
        /// <param name="input"></param>
        /// <param name="clipDirectory"></param>
        /// <param name="username"></param>
        public IReadOnlyList<Highlight> GenerateClips(string input, string clipDirectory, string username)
        {
            //Setup the files
            SourceFile      = input;
            ClipDirectory   = clipDirectory;
            CroppedFile     = Path.Combine(TemporaryFileDirectory, Prefix + "cropped.mp4");
            Name            = username;

            //Crop the video to the temporary path
            this.CropVideo();

            //Generate the highlight listing based of this
            var highlights = this.GenerateHighlights();
            GenerateClips(highlights, input, clipDirectory, false);

            //Return the higlights
            return highlights;
        }

        /// <summary>
        /// Generates clips from the given input and stores them in the clip directory.
        /// </summary>
        /// <param name="highlights"></param>
        /// <param name="input"></param>
        /// <param name="clipDirectory"></param>
        /// <param name="overwrite"></param>
        public void GenerateClips(IEnumerable<Highlight> highlights, string input, string clipDirectory, bool overwrite = false)
        {
            //Setupt he input files
            SourceFile = input;
            ClipDirectory = clipDirectory;

            List<Process> processes = new List<Process>();

            //Crop them all
            foreach (var highlight in highlights)
            {
                var process = this.TrimHighlight(highlight, false);
                if (process != null) processes.Add(process);
            }

            //Wait for them to finish
            foreach(var process in processes)
            {
                if (process != null) continue;

                //Wait for exit, one instance at a time.
                process.WaitForExit();
                process.Dispose();
            }
        }

        #region Highlight Manipulation
        /// <summary>
        /// Finds all the highlights in the video
        /// </summary>
        /// <param name="name"></param>
        /// <param name="videoPath"></param>
        /// <returns></returns>
        private List<Highlight> GenerateHighlights()
        {
            Log("Finding Highlights...");
            List<Highlight> responses = new List<Highlight>();
            var process = new Process
            {
                StartInfo = { 
                    FileName = HighlightExecutable, 
                    Arguments = $"{Name} {CroppedFile}",
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                },
                EnableRaisingEvents = true,
            };

            process.OutputDataReceived += (sender, e) => {

                if (e.Data == null) return;

                //Prepare the line
                string[] parts = e.Data.Split(';');
                var response = new Highlight();

                //Parse the parts
                foreach(var part in parts)
                {
                    var stubs = part.Split('=');
                    if (stubs.Length != 2) continue;

                    var variable = stubs[0].Trim();
                    var value = stubs[1].Trim();

                    switch (variable)
                    {
                        case "frame":
                            var frameStubs = value.Split('@');
                            response.frame = ulong.Parse(frameStubs[0]);
                            response.fps = double.Parse(frameStubs[1]);
                            //response.SetTime(response.frame / response.fps, Preamble, Postamble);
                            break;

                        case "levenshtein":
                            response.levenshtein = int.Parse(value);
                            break;

                        case "text":
                            response.text = value;
                            break;

                        case "time":
                            response.SetTime(double.Parse(value), Preamble, Postamble);
                            break;

                        default:
                            break;
                    }
                }

                if (response.frame != 0 && response.text != null)
                    responses.Add(response);
            };


            //Start the process and return the task
            process.Start();

            //Begin read the lines
            process.BeginOutputReadLine();

            //Wait for exit
            process.WaitForExit();

            //Dispose the process
            process.Dispose();

            //Merge the highlighs
            List<Highlight> merged = new List<Highlight>(responses.Count);
            MergeHighlighsNonAlloc(responses, merged);

            Log("Found {0} highlights (was {1})", merged.Count, responses.Count);

            //Return the frames
            return merged;
        }

        /// <summary>
        /// Merges highlights that are close together.
        /// </summary>
        /// <param name="sourceHighlights"></param>
        /// <param name="mergedHighlights"></param>
        private void MergeHighlighsNonAlloc(List<Highlight> sourceHighlights, List<Highlight> mergedHighlights)
        {
            Log("Merging {0} highlights...", sourceHighlights);
            foreach (var result in sourceHighlights)
            {
                //Skip finds
                if (result.text.Contains("found", StringComparison.OrdinalIgnoreCase))
                    continue;

                //Merge
                bool hasMerged = false;
                foreach (var other in mergedHighlights)
                {

                    //If our start is behind the other start, and our end is infront of the other end.
                    if ((result.startTime - Padding) <= other.startTime && (result.endTime + Padding) >= other.startTime)
                    {
                        //Shift the other start time to our start time.
                        other.startTime = result.startTime;
                        hasMerged = true;
                    }

                    //If our end is infront of the other end, and our startis behind it.
                    if ((result.endTime + Padding) >= other.endTime && (result.startTime - Padding) <= other.endTime)
                    {
                        //Shift the other end tiem to our end time
                        other.endTime = result.endTime;
                        hasMerged = true;
                    }
                }

                //Add to the highlight if we have not merged
                if (!hasMerged)
                    mergedHighlights.Add(result);
            }
        }
        #endregion

        #region Video Manipulation
        /// <summary>
        /// Crops the input file and saves it to the temporary file.
        /// </summary>
        private void CropVideo()
        {
            if (File.Exists(CroppedFile))
            {
                if (DeleteTemporaryFiles)
                {
                    File.Delete(CroppedFile);
                }else
                {
                    //Abort early because its already cropped
                    return;
                }
            }

            Log("Cropping {0} to {1}", SourceFile, CroppedFile);

            //ffprobe -v error -select_streams v:0 -show_entries stream=width,height -of csv=p=0 
            int width, height;
            using (var probe = Process.Start(new ProcessStartInfo("ffprobe", "-v error -select_streams v:0 -show_entries stream=width,height -of csv=p=0  " + SourceFile) { RedirectStandardOutput = true }))
            {
                string[] parts = probe.StandardOutput.ReadToEnd().Split(',');
                width = int.Parse(parts[0]);
                height = int.Parse(parts[1]);
            }

            //width = 1920;
            //height = 1080;
            int x, y, w, h;
            x = (int)Math.Round((1450 / 1920.0) * width);
            y = (int)Math.Round((283 / 1080.0) * height);

            w = (int)Math.Round((398 / 1920.0) * width);
            h = (int)Math.Round((53 / 1080.0) * height);

            var ffmpeg = Process.Start(new ProcessStartInfo("ffmpeg", $"-hide_banner -loglevel panic -i {SourceFile} -an -filter:v \"crop = {w}:{h}:{x}:{y}\" -threads {Threads} {CroppedFile}"));
            ffmpeg.WaitForExit();
        }

        /// <summary>
        /// Trims a highlight
        /// </summary>
        /// <param name="highlight"></param>
        /// <param name="overwrite"></param>
        private Process TrimHighlight(Highlight highlight, bool overwrite = false)
        {
            //Make sure the clip directory exists
            if (!Directory.Exists(ClipDirectory))
                Directory.CreateDirectory(ClipDirectory);

            //We dont need to do anything 
            if (!overwrite && !string.IsNullOrEmpty(highlight.clipFile) && File.Exists(highlight.clipFile))
                return null;

            //Prepare the arguments
            string rand = RandomString.AlphaNumeric(5);
            string output = $"{ClipDirectory}/{Prefix}{Name}-{highlight.frame}-{rand}.mp4";
            var arguments = new string[]
            {
                "-hide_banner -loglevel panic",
                "-ss " + highlight.startTime,       //HAS TO BE IN THIS ORDER
                "-t " + highlight.duration,
                "-i " + SourceFile,
                "-c copy",
                output
            };

            //Delete existing
            if (File.Exists(output)) File.Delete(output);

            //Start FFMPEG
            Log("Exporting Clip {0} | {1} -> {2} ({3}s) | {4}", output, highlight.startTime, highlight.endTime, highlight.duration, highlight.text);
            var argstr = string.Join(' ', arguments);
            var process = Process.Start(new ProcessStartInfo("ffmpeg", argstr));
            process.WaitForExit();
            return process;
        }

        #endregion

        /// <summary>
        /// Disposes of the trimmer, cleaning up its temporary file.
        /// </summary>
        public void Dispose()
        {
            if (DeleteTemporaryFiles && File.Exists(CroppedFile))
                File.Delete(CroppedFile);
        }

        /// <summary>
        /// Logs
        /// </summary>
        /// <param name="format"></param>
        /// <param name="args"></param>
        private void Log(string format, params object[] args)
        {
            Console.WriteLine(string.Format(format, args));
        }

        /// <summary>
        /// A individual highlight
        /// </summary>
        [JsonObject(ItemNullValueHandling = NullValueHandling.Ignore)]
        public class Highlight
        {
            /// <summary>
            /// Framerate of the highlight
            /// </summary>
            public double fps;

            /// <summary>
            /// Frame it started
            /// </summary>
            public ulong frame;

            /// <summary>
            /// Levenshtein value of the text
            /// </summary>
            public int levenshtein;

            /// <summary>
            /// The text itself
            /// </summary>
            public string text;

            /// <summary>
            /// Time it starts at
            /// </summary>
            public double time;

            /// <summary>
            /// Start Time
            /// </summary>
            public double startTime;

            /// <summary>
            /// End Time
            /// </summary>
            public double endTime;

            /// <summary>
            /// The file the clipping was saved too
            /// </summary>
            public string clipFile;

            /// <summary>
            /// Duration
            /// </summary>
            public double duration { get { return endTime - startTime; } }

            /// <summary>
            /// Sets the time with a preamble and postamble
            /// </summary>
            /// <param name="time"></param>
            /// <param name="preamble"></param>
            /// <param name="postamble"></param>
            public void SetTime(double time, double preamble, double postamble)
            {
                this.time = time;
                this.startTime = time - preamble;
                this.endTime = time + postamble;
            }
        }
    }
}
