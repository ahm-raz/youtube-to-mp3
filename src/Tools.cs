using System;
using System.IO;
using System.IO.Compression;
using System.Reflection;
using System.Threading;

namespace YouTubeToMp3
{
    // In the single-file build, yt-dlp.exe and ffmpeg.exe are embedded (gzip-compressed) as
    // resources. On first run they are decompressed next to the user's data, so the app is
    // "just one exe" to copy around. If they are NOT embedded (a plain dev build), we fall back
    // to the files sitting next to the exe.
    static class Tools
    {
        static readonly object Gate = new object();

        public static readonly string Dir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "YouTubeToMp3", "tools");

        public static string YtDlp { get { return Path.Combine(Dir, "yt-dlp.exe"); } }
        public static string FfmpegDir { get { return Dir; } }
        public static string Aria2c { get { return Path.Combine(Dir, "aria2c.exe"); } }

        // Extract embedded binaries if needed. Returns false (with 'missing' set) only when a
        // required tool is neither embedded nor found next to the exe. aria2c is optional: it
        // only speeds downloads up, so a failure to unpack it is not fatal.
        public static bool Ensure(out string missing)
        {
            missing = null;
            lock (Gate)
            {
                if (!Extract("yt-dlp.exe")) { missing = "yt-dlp.exe"; return false; }
                if (!Extract("ffmpeg.exe")) { missing = "ffmpeg.exe"; return false; }
                try { Extract("aria2c.exe"); } catch { /* optional speed-up */ }
                return true;
            }
        }

        // Unpacks the embedded resource for 'name' (preferring a "<name>.gz" compressed copy) to
        // the tools dir, but only when it is missing or a different build than last time.
        static bool Extract(string name)
        {
            var asm = Assembly.GetExecutingAssembly();
            bool gz;
            string resName = ResourceName(asm, name, out gz);
            string target = Path.Combine(Dir, name);

            if (resName == null)
            {
                // Not embedded: dev build. Accept a copy sitting beside the exe.
                string beside = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, name);
                if (File.Exists(beside)) return CopyBeside(beside, target);
                return File.Exists(target);
            }

            using (var src = asm.GetManifestResourceStream(resName))
            {
                if (src == null) return File.Exists(target);

                // The resource's (compressed) length is a cheap signature for "is this still the
                // build I last unpacked?" — stored alongside the target so we skip repeat work.
                string marker = target + ".src";
                string sig = src.Length.ToString();
                if (File.Exists(target) && File.Exists(marker) &&
                    File.ReadAllText(marker) == sig)
                    return true;

                Directory.CreateDirectory(Dir);
                string tmp = target + "." + Guid.NewGuid().ToString("N") + ".tmp";
                using (var dst = new FileStream(tmp, FileMode.Create, FileAccess.Write, FileShare.None))
                {
                    if (gz)
                        using (var z = new GZipStream(src, CompressionMode.Decompress))
                            z.CopyTo(dst);
                    else
                        src.CopyTo(dst);
                }
                ReplaceFile(tmp, target);
                File.WriteAllText(marker, sig);
                return true;
            }
        }

        static bool CopyBeside(string beside, string target)
        {
            if (File.Exists(target) && new FileInfo(target).Length == new FileInfo(beside).Length)
                return true;
            Directory.CreateDirectory(Dir);
            File.Copy(beside, target, true);
            return true;
        }

        static void ReplaceFile(string tmp, string target)
        {
            // An old copy may still be locked by a previous run; retry briefly.
            for (int i = 0; ; i++)
            {
                try
                {
                    if (File.Exists(target)) File.Delete(target);
                    File.Move(tmp, target);
                    return;
                }
                catch (IOException)
                {
                    if (i >= 10) { File.Copy(tmp, target, true); try { File.Delete(tmp); } catch { } return; }
                    Thread.Sleep(100);
                }
                catch (UnauthorizedAccessException)
                {
                    if (i >= 10) throw;
                    Thread.Sleep(100);
                }
            }
        }

        // Resource names are prefixed with the assembly name by csc; match by suffix. Prefers a
        // gzip-compressed "<name>.gz" resource, falling back to a raw "<name>".
        static string ResourceName(Assembly asm, string name, out bool gz)
        {
            string raw = null;
            foreach (string r in asm.GetManifestResourceNames())
            {
                if (Matches(r, name + ".gz")) { gz = true; return r; }
                if (Matches(r, name)) raw = r;
            }
            gz = false;
            return raw;
        }

        static bool Matches(string resource, string name)
        {
            return resource.Equals(name, StringComparison.OrdinalIgnoreCase) ||
                   resource.EndsWith("." + name, StringComparison.OrdinalIgnoreCase);
        }
    }
}
