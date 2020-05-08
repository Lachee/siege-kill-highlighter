using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Text;

namespace SiegeClipHighlighter.Mixer
{
    public class VOD : MixerObject
    {
        [JsonProperty("baseUrl")]
        public string BaseURL { get; private set; }

        [JsonProperty("format")]
        public string Format { get; private set; }

        [JsonProperty("data")]
        public VodData? Data { get; private set; }

        [JsonProperty("recordingId")]
        public uint RecordingId { get; private set; }

        public struct VodData
        {
            public uint Width;
            public uint Height;
            public float Fps;
            public uint Bitrate;
        }
    }
}
