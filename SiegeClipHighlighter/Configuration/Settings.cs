using System;
using System.Collections.Generic;
using System.Text;

namespace SiegeClipHighlighter.Configuration
{
    public class Settings
    {
        public uint GameType { get; set; } = 20097;
        public string Tesseract = @"../../../../SiegeClipHighlighter.Tesseract/Release/SiegeClipHighlighter.Tesseract.exe";
        public Dictionary<string, Channel> Channels { get; set; }
        public string ClipDirectory = "clips";
        public string TempDirectory = "temp";
        public bool DeleteTemporaryFiles = false;
    }
}
