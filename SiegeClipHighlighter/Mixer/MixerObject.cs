using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Text;

namespace SiegeClipHighlighter.Mixer
{
    public class MixerObject
    {
        [JsonProperty("id")]
        public uint ID { get; private set; }
    }
}
