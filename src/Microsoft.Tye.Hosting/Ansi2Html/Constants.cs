using System.Collections.Generic;

namespace Microsoft.Tye.Hosting.Ansi2Html
{
    public static class Constants
    {
        public const string Red = "#800000";
        public const string Black = "#000000";
        public const string Green = "#008000";
        public const string Yellow = "#808000";
        public const string Blue = "#000080";
        public const string Purple = "#800080";
        public const string Cyan = "#008080";
        public const string LightGray = "#c0c0c0";
        public const string DarkGray = "#808080";
        public const string BrightRed = "#ff0000";
        public const string BrightGreen = "#00ff00";
        public const string BrightYellow = "#ffff00";
        public const string BrightBlue = "#0000ff";
        public const string BrightPurple = "#ff00ff";
        public const string BrightCyan = "#00ffff";
        public const string White = "#ffffff";

        public static Dictionary<string, string> ColorMap = new Dictionary<string, string>()
        {
            { "0", Black }, // Black
            { "1", Red }, // Red
            { "2", Green }, // Green
            { "3", Yellow }, // Yellow
            { "4", Blue }, // Blue
            { "5", Purple }, // Purple
            { "6", Cyan }, // Cyan
            { "7", LightGray }, // Light Gray
            { "8", DarkGray }, // Dark Gray
            { "9", BrightRed }, // Bright Red
            { "10", BrightGreen }, // Bright Green
            { "11", BrightYellow }, // Bright Yellow
            { "12", BrightBlue }, // Bright Blue
            { "13", BrightPurple }, // Bright Purple
            { "14", BrightCyan }, // Bright Cyan
            { "15", White } // White
        };

        public static class SelectGraphicRenditionParameters
        {
            public const int Reset = 0;

            public static HashSet<int> SetForeground = new HashSet<int>()
            {
                30, 31, 32, 33, 34, 35, 36, 37,38
            };

        }
    }
}
