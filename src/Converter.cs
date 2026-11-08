using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;

namespace YouTubeToMp3
{
    // Drives yt-dlp.exe (with ffmpeg.exe alongside) to pull a video's audio and encode it to MP3.
    sealed class Converter
    {
        // Progress percent 0..100 while downloading; messages for the log; raised on a worker thread.
        public event Action<double> Progress;
        public event Action<string> Log;
        public event Action<string> StageChanged;

        static readonly Regex PercentRe = new Regex(@"(\d{1,3}(?:\.\d+)?)%", RegexOptions.Compiled);
        static readonly Regex DestRe = new Regex(@"\[(?:ExtractAudio|download)\] Destination: (.+)", RegexOptions.Compiled);

        readonly string ytDlp;
        readonly string ffmpegDir;

        public Converter()
        {
            ytDlp = Tools.YtDlp;
            ffmpegDir = Tools.FfmpegDir;
        }

        // Unpacks the embedded binaries on first run; returns false (with 'missing' set) if a
        // required tool cannot be made available.
        public bool ToolsPresent(out string missing)
        {
            return Tools.Ensure(out missing);
        }

        public string LastOutputFile { get; private set; }

        // bitrate: e.g. "320", "256", "192", "128", or "0" for best VBR.
        // Returns the process exit code (0 = success). Throws only if the process cannot start.
        public int Run(string url, string outputDir, string bitrate, bool playlist)
        {
            LastOutputFile = null;
            Directory.CreateDirectory(outputDir);

            string quality = bitrate == "0" ? "0" : bitrate + "K";
            var args = new StringBuilder();
            args.Append("--no-mtime --newline --no-part ");
            args.Append("-N 8 ");                               // concurrent fragments for speed
            args.Append("--extract-audio --audio-format mp3 "); // ffmpeg encodes to MP3
            args.Append("--audio-quality ").Append(quality).Append(' ');
            args.Append("--embed-thumbnail --embed-metadata ");
            args.Append(playlist ? "--yes-playlist " : "--no-playlist ");
            args.Append("--ffmpeg-location \"").Append(ffmpegDir).Append("\" ");
            args.Append("-o \"").Append(Path.Combine(outputDir, "%(title)s.%(ext)s")).Append("\" ");
            args.Append('"').Append(url).Append('"');

            var psi = new ProcessStartInfo
            {
                FileName = ytDlp,
                Arguments = args.ToString(),
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding = Encoding.UTF8,
            };

            using (var p = new Process { StartInfo = psi })
            {
                p.OutputDataReceived += (s, e) => HandleLine(e.Data);
                p.ErrorDataReceived += (s, e) => HandleLine(e.Data);
                p.Start();
                p.BeginOutputReadLine();
                p.BeginErrorReadLine();
                p.WaitForExit();
                return p.ExitCode;
            }
        }

        void HandleLine(string line)
        {
            if (string.IsNullOrEmpty(line)) return;

            var dest = DestRe.Match(line);
            if (dest.Success)
            {
                string f = dest.Groups[1].Value.Trim();
                if (f.EndsWith(".mp3", StringComparison.OrdinalIgnoreCase))
                    LastOutputFile = f;
            }

            if (line.StartsWith("[download]"))
            {
                var m = PercentRe.Match(line);
                if (m.Success)
                {
                    double pct;
                    if (double.TryParse(m.Groups[1].Value, System.Globalization.NumberStyles.Float,
                            System.Globalization.CultureInfo.InvariantCulture, out pct))
                        Raise(Progress, pct);
                }
            }
            else if (line.StartsWith("[ExtractAudio]"))
            {
                Raise(StageChanged, "Converting to MP3…");
            }

            Raise(Log, line);
        }

        static void Raise<T>(Action<T> ev, T value) { var h = ev; if (h != null) h(value); }
    }
}
