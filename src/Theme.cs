using System.Drawing;

namespace YouTubeToMp3
{
    // Two palettes (light/dark) plus the shared accent. Kept in one place so the whole UI
    // re-colors from a single switch.
    sealed class Theme
    {
        public Color Window, Card, Text, Muted, Border, Accent, AccentHover, AccentText, FieldBg, HeaderFrom, HeaderTo;

        public static readonly Color Brand = Color.FromArgb(220, 38, 38);       // red
        public static readonly Color BrandHover = Color.FromArgb(185, 28, 28);

        public static Theme Light()
        {
            return new Theme
            {
                Window = Color.FromArgb(245, 246, 248),
                Card = Color.White,
                Text = Color.FromArgb(31, 36, 48),
                Muted = Color.FromArgb(107, 114, 128),
                Border = Color.FromArgb(226, 229, 234),
                Accent = Brand,
                AccentHover = BrandHover,
                AccentText = Color.White,
                FieldBg = Color.White,
                HeaderFrom = Color.FromArgb(225, 29, 72),
                HeaderTo = Color.FromArgb(190, 18, 60),
            };
        }

        public static Theme Dark()
        {
            return new Theme
            {
                Window = Color.FromArgb(21, 23, 28),
                Card = Color.FromArgb(30, 33, 40),
                Text = Color.FromArgb(232, 234, 237),
                Muted = Color.FromArgb(154, 160, 166),
                Border = Color.FromArgb(42, 46, 55),
                Accent = Color.FromArgb(239, 68, 68),
                AccentHover = Color.FromArgb(220, 38, 38),
                AccentText = Color.White,
                FieldBg = Color.FromArgb(38, 42, 51),
                HeaderFrom = Color.FromArgb(159, 18, 57),
                HeaderTo = Color.FromArgb(120, 14, 43),
            };
        }
    }
}
