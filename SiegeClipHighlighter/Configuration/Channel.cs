using System;
using System.Collections.Generic;
using System.Text;

namespace SiegeClipHighlighter.Configuration
{
    public class Channel
    {
        public uint ChannelId { get; set; } = 0;
        public DateTime LastRecording { get; set; } = DateTime.MinValue;
        public string SiegeName { get; set; } = "";

    }
}
