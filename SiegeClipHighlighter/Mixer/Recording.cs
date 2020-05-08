using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Newtonsoft.Json;

namespace SiegeClipHighlighter.Mixer
{
    public class Recording : MixerObject
    {
        [JsonProperty("channelId")]
        public uint ChannelId { get; private set; }
        [JsonProperty("state")]
        public string State { get; private set; }
        [JsonProperty("totalViews")]
        public uint TotalViews { get; private set; }
        [JsonProperty("expiresAt")]
        public DateTime ExpiresAt { get; private set; }
        [JsonProperty("vods")]
        public IReadOnlyList<VOD> Vods { get; private set; }
        [JsonProperty("viewed")]
        public bool? Viewed { get; private set; }
        [JsonProperty("name")]
        public string Name { get; private set; }
        [JsonProperty("typeId")]
        public uint TypeId { get; private set; }
        [JsonProperty("duration")]
        public double Duration { get; private set; }
        [JsonProperty("seen")]
        public bool? Seen { get; private set; }
        [JsonProperty("contentId")]
        public string ContentId { get; private set; }
        [JsonProperty("createdAt")]
        public DateTime CreatedAt { get; private set; }

        /// <summary>
        /// gets the source.mp4 file.
        /// </summary>
        /// <returns></returns>
        public string GetMP4Url() {
            return Vods.Where(v => v.Format == "raw").Select(v => v.BaseURL + "source.mp4").FirstOrDefault();
        }
    }
}
