using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;

namespace YouTubeToMp3
{
    // Drives yt-dlp.exe (+ ffmpeg, optional aria2c) to download a single video or a whole
    // playlist, one item at a time or several at once, reporting per-item progress.
    sealed class Converter
    {
        public event Action<int, int, string> ItemStarted;   // index(1..n), total, title
        public event Action<int, double> ItemProgress;       // index, percent 0..100
        public event Action<int, string> ItemStage;          // index, stage label
        public event Action<int, bool, string> ItemFinished; // index, ok, output-path-or-error
        public event Action<string> Log;

        static readonly Regex PercentRe = new Regex(@"(\d{1,3}(?:\.\d+)?)%", RegexOptions.Compiled);
        static readonly Regex DestRe = new Regex(@"\[(?:ExtractAudio|download)\] Destination: (.+)", RegexOptions.Compiled);
        static readonly Regex MergeRe = new Regex("\\[Merger\\] Merging formats into \"(.+)\"", RegexOptions.Compiled);

        readonly string ytDlp = Tools.YtDlp;
        readonly string ffmpegDir = Tools.FfmpegDir;

        public sealed class Job
        {
            public string Url;
            public string OutputDir;
            public bool Video;        // false = MP3 audio, true = MP4 video
            public string Quality;    // audio bitrate or video max-height; "0" = best
            public bool Playlist;
            public bool Fast;         // route downloads through aria2c (multi-connection)
            public bool EmbedThumbnail = true;
            public bool EmbedMetadata = true;
            public int Parallel = 1;  // simultaneous item downloads
        }

        public sealed class Item
        {
            public int Index;
            public string Url;
            public string Title;
            public bool Ok;
            public string Output;
        }

        public bool ToolsPresent(out string missing) { return Tools.Ensure(out missing); }

        // Discover the items to download without fetching media. For a single video this returns
        // one entry; for a playlist, one per video (with titles) so the UI can list them up front.
        public List<Item> Enumerate(Job j)
        {
            var list = new List<Item>();
            if (!j.Playlist)
            {
                list.Add(new Item { Index = 1, Url = j.Url, Title = "Video" });
                return list;
            }

            var args = new StringBuilder();
            args.Append("--flat-playlist --ignore-errors --no-warnings --yes-playlist ");
            args.Append("--print \"%(url)s\\t%(id)s\\t%(title)s\" ");
            args.Append('"').Append(j.Url).Append('"');

            int exit = RunStream(args.ToString(), line =>
            {
                if (string.IsNullOrWhiteSpace(line) || !line.Contains("\t")) return;
                string[] f = line.Split('\t');
                string url = f[0].Trim();
                string id = f.Length > 1 ? f[1].Trim() : "";
                string title = f.Length > 2 ? f[2].Trim() : "Video";
                if (url.Length == 0 && id.Length > 0) url = "https://www.youtube.com/watch?v=" + id;
                if (url.Length == 0) return;
                list.Add(new Item { Index = list.Count + 1, Url = url, Title = title });
            }, onlyStdout: true);

            // Fallback: couldn't enumerate (private list, odd extractor) — treat the URL as one item.
            if (list.Count == 0)
                list.Add(new Item { Index = 1, Url = j.Url, Title = "Video" });
            return list;
        }

        // Download every item, up to j.Parallel at a time. Returns the number that failed (0 = all ok).
        public int RunJob(Job j, List<Item> items)
        {
            int maxPar = Math.Max(1, Math.Min(j.Parallel, items.Count));
            var queue = new ConcurrentQueue<Item>(items);
            int failures = 0;

            var workers = new List<Thread>();
            for (int k = 0; k < maxPar; k++)
            {
                var t = new Thread(() =>
                {
                    Item it;
                    while (queue.TryDequeue(out it))
                    {
                        Raise(ItemStarted, it.Index, items.Count, it.Title);
                        int code;
                        try { code = DownloadOne(it, j); }
                        catch (Exception ex) { code = -1; Raise(Log, "ERROR " + ex.Message); }
                        it.Ok = (code == 0);
                        if (!it.Ok) Interlocked.Increment(ref failures);
                        Raise(ItemFinished, it.Index, it.Ok, it.Ok ? (it.Output ?? "") : "failed");
                    }
                });
                t.IsBackground = true;
                workers.Add(t);
                t.Start();
            }
            foreach (var w in workers) w.Join();
            return failures;
        }

        int DownloadOne(Item it, Job j)
        {
            Directory.CreateDirectory(j.OutputDir);
            var args = new StringBuilder();
            args.Append("--no-mtime --newline --no-part --no-playlist ");
            args.Append("-N 8 ");

            bool useAria = j.Fast && File.Exists(Tools.Aria2c);
            if (useAria)
            {
                args.Append("--downloader aria2c ");
                args.Append("--downloader-args \"aria2c:-x16 -s16 -k1M --summary-interval=1 --console-log-level=warn\" ");
            }

            if (j.Video)
            {
                // Prefer H.264(avc1)+AAC(m4a) for the widest device compatibility; then any mp4+m4a;
                // then the best video+audio; then any single file — all capped at the chosen height.
                if (j.Quality == "0")
                    args.Append("-f \"bv*[vcodec^=avc1]+ba[ext=m4a]/bv*[ext=mp4]+ba[ext=m4a]/bv*+ba/b\" ");
                else
                {
                    string h = j.Quality;
                    args.Append("-f \"bv*[vcodec^=avc1][height<=").Append(h).Append("]+ba[ext=m4a]/bv*[ext=mp4][height<=")
                        .Append(h).Append("]+ba[ext=m4a]/bv*[height<=").Append(h).Append("]+ba/b[height<=").Append(h).Append("]/b\" ");
                }
                args.Append("--merge-output-format mp4 ");
                if (j.EmbedMetadata) args.Append("--embed-metadata ");
            }
            else
            {
                string aq = j.Quality == "0" ? "0" : j.Quality + "K";
                args.Append("--extract-audio --audio-format mp3 ");
                args.Append("--audio-quality ").Append(aq).Append(' ');
                if (j.EmbedThumbnail) args.Append("--embed-thumbnail ");
                if (j.EmbedMetadata) args.Append("--embed-metadata ");
            }

            args.Append("--ffmpeg-location \"").Append(ffmpegDir).Append("\" ");
            args.Append("-o \"").Append(Path.Combine(j.OutputDir, "%(title)s.%(ext)s")).Append("\" ");
            args.Append('"').Append(it.Url).Append('"');

            return RunStream(args.ToString(), line => HandleLine(it, line), onlyStdout: false);
        }

        void HandleLine(Item it, string line)
        {
            if (string.IsNullOrEmpty(line)) return;

            var dest = DestRe.Match(line);
            if (dest.Success)
            {
                string f = dest.Groups[1].Value.Trim();
                if (f.EndsWith(".mp3", StringComparison.OrdinalIgnoreCase) ||
                    f.EndsWith(".mp4", StringComparison.OrdinalIgnoreCase))
                    it.Output = f;
                string name = SafeName(f);
                if (name != null && (it.Title == "Video" || string.IsNullOrEmpty(it.Title))) it.Title = name;
            }
            var merge = MergeRe.Match(line);
            if (merge.Success) it.Output = merge.Groups[1].Value.Trim();

            if (line.StartsWith("[download]") || line.StartsWith("[#"))
            {
                var m = PercentRe.Match(line);
                double pct;
                if (m.Success && double.TryParse(m.Groups[1].Value, System.Globalization.NumberStyles.Float,
                        System.Globalization.CultureInfo.InvariantCulture, out pct))
                    Raise(ItemProgress, it.Index, pct);
            }
            else if (line.StartsWith("[ExtractAudio]")) Raise(ItemStage, it.Index, "Converting to MP3…");
            else if (line.StartsWith("[Merger]")) Raise(ItemStage, it.Index, "Merging…");

            Raise(Log, line);
        }

        static string SafeName(string path)
        {
            try { return Path.GetFileNameWithoutExtension(path); } catch { return null; }
        }

        // Runs yt-dlp with the given args, delivering each stdout (and, unless onlyStdout, stderr)
        // line to 'onLine'. Returns the exit code.
        int RunStream(string arguments, Action<string> onLine, bool onlyStdout)
        {
            var psi = new ProcessStartInfo
            {
                FileName = ytDlp,
                Arguments = arguments,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding = Encoding.UTF8,
            };
            psi.EnvironmentVariables["PATH"] = Tools.Dir + ";" + Environment.GetEnvironmentVariable("PATH");

            using (var p = new Process { StartInfo = psi })
            {
                p.OutputDataReceived += (s, e) => { if (e.Data != null) onLine(e.Data); };
                p.ErrorDataReceived += (s, e) => { if (e.Data != null && !onlyStdout) onLine(e.Data); };
                p.Start();
                p.BeginOutputReadLine();
                p.BeginErrorReadLine();
                p.WaitForExit();
                return p.ExitCode;
            }
        }

        static void Raise(Action<string> ev, string a) { var h = ev; if (h != null) h(a); }
        static void Raise(Action<int, double> ev, int a, double b) { var h = ev; if (h != null) h(a, b); }
        static void Raise(Action<int, string> ev, int a, string b) { var h = ev; if (h != null) h(a, b); }
        static void Raise(Action<int, int, string> ev, int a, int b, string c) { var h = ev; if (h != null) h(a, b, c); }
        static void Raise(Action<int, bool, string> ev, int a, bool b, string c) { var h = ev; if (h != null) h(a, b, c); }
    }
}
