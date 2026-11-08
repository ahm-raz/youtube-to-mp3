using System;
using System.Collections.Generic;
using System.IO;

namespace YouTubeToMp3
{
    // Simple key=value preferences saved to %LOCALAPPDATA%\YouTubeToMp3\settings.txt.
    sealed class Settings
    {
        static readonly string Dir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "YouTubeToMp3");
        static readonly string File_ = Path.Combine(Dir, "settings.txt");

        readonly Dictionary<string, string> map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        public static string DefaultFolder
        {
            get
            {
                return Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.MyMusic), "YouTubeToMp3");
            }
        }

        // Typed accessors with sensible defaults.
        public bool Video            { get { return GetBool("video", false); }           set { map["video"] = value ? "1" : "0"; } }
        public string AudioQuality   { get { return Get("audioQuality", "320"); }         set { map["audioQuality"] = value; } }
        public string VideoQuality   { get { return Get("videoQuality", "1080"); }        set { map["videoQuality"] = value; } }
        public string Folder         { get { return Get("folder", DefaultFolder); }       set { map["folder"] = value; } }
        public bool Playlist         { get { return GetBool("playlist", false); }         set { map["playlist"] = value ? "1" : "0"; } }
        public bool Fast             { get { return GetBool("fast", true); }              set { map["fast"] = value ? "1" : "0"; } }
        public bool EmbedThumbnail   { get { return GetBool("embedThumb", true); }        set { map["embedThumb"] = value ? "1" : "0"; } }
        public bool EmbedMetadata    { get { return GetBool("embedMeta", true); }         set { map["embedMeta"] = value ? "1" : "0"; } }
        public bool OpenWhenDone     { get { return GetBool("openWhenDone", true); }      set { map["openWhenDone"] = value ? "1" : "0"; } }
        public bool DarkMode         { get { return GetBool("dark", false); }             set { map["dark"] = value ? "1" : "0"; } }

        // Simultaneous item downloads for playlists (1 = one at a time / linear).
        public int Parallel
        {
            get { int n; return int.TryParse(Get("parallel", "1"), out n) ? Math.Max(1, Math.Min(5, n)) : 1; }
            set { map["parallel"] = Math.Max(1, Math.Min(5, value)).ToString(); }
        }

        public static Settings Load()
        {
            var s = new Settings();
            try
            {
                if (File.Exists(File_))
                    foreach (string line in File.ReadAllLines(File_))
                    {
                        int eq = line.IndexOf('=');
                        if (eq > 0) s.map[line.Substring(0, eq).Trim()] = line.Substring(eq + 1).Trim();
                    }
            }
            catch { /* fall back to defaults */ }
            return s;
        }

        public void Save()
        {
            try
            {
                Directory.CreateDirectory(Dir);
                var sb = new System.Text.StringBuilder();
                foreach (var kv in map) sb.Append(kv.Key).Append('=').Append(kv.Value).Append('\n');
                File.WriteAllText(File_, sb.ToString());
            }
            catch { /* non-fatal: preferences just won't persist */ }
        }

        string Get(string key, string dflt) { string v; return map.TryGetValue(key, out v) && v.Length > 0 ? v : dflt; }
        bool GetBool(string key, bool dflt) { string v; return map.TryGetValue(key, out v) ? v == "1" : dflt; }
    }
}
