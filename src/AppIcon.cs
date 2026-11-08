using System;
using System.Drawing;
using System.Drawing.Drawing2D;

namespace YouTubeToMp3
{
    // A red "play button" icon drawn in code, so the app ships with no image files.
    static class AppIcon
    {
        public static Icon Create()
        {
            using (var bmp = new Bitmap(32, 32))
            {
                using (var g = Graphics.FromImage(bmp))
                using (var fill = new SolidBrush(Color.FromArgb(220, 38, 38)))
                using (var white = new SolidBrush(Color.White))
                {
                    g.SmoothingMode = SmoothingMode.AntiAlias;
                    g.Clear(Color.Transparent);
                    using (var path = RoundedRect(1, 5, 30, 22, 6))
                        g.FillPath(fill, path);
                    // white triangle (play)
                    g.FillPolygon(white, new[]
                    {
                        new PointF(13F, 10F), new PointF(13F, 22F), new PointF(23F, 16F),
                    });
                }
                IntPtr handle = bmp.GetHicon();
                try { return (Icon)Icon.FromHandle(handle).Clone(); }
                finally { Native.DestroyIcon(handle); }
            }
        }

        static GraphicsPath RoundedRect(float x, float y, float w, float h, float r)
        {
            var p = new GraphicsPath();
            float d = r * 2;
            p.AddArc(x, y, d, d, 180, 90);
            p.AddArc(x + w - d, y, d, d, 270, 90);
            p.AddArc(x + w - d, y + h - d, d, d, 0, 90);
            p.AddArc(x, y + h - d, d, d, 90, 90);
            p.CloseFigure();
            return p;
        }
    }
}
