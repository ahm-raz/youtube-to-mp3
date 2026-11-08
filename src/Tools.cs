using System;
using System.IO;
using System.Reflection;
using System.Threading;

namespace YouTubeToMp3
{
    // In the single-file build, yt-dlp.exe and ffmpeg.exe are embedded as resources.
    // On first run they are unpacked next to the user's data, so the app is "just one exe"
    // to copy around. If they are NOT embedded (a plain dev build), we fall back to the
    // files sitting next to the exe.
    static class Tools
    {
        static readonly object Gate = new object();

        public static readonly string Dir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "YouTubeToMp3", "tools");

        public static string YtDlp { get { return Path.Combine(Dir, "yt-dlp.exe"); } }
        public static string FfmpegDir { get { return Dir; } }

        // Extract embedded binaries if needed. Returns false (with 'missing' set) only when a
        // required tool is neither embedded nor found next to the exe.
        public static bool Ensure(out string missing)
        {
            missing = null;
            lock (Gate)
            {
                if (!Extract("yt-dlp.exe")) { missing = "yt-dlp.exe"; return false; }
                if (!Extract("ffmpeg.exe")) { missing = "ffmpeg.exe"; return false; }
                return true;
            }
        }

        // Writes the embedded resource to the tools dir if it is missing or a different size.
        // Returns true when the file ends up present (whether just written or already good,
        // including the dev-build case where it lives next to the exe).
        static bool Extract(string name)
        {
            var asm = Assembly.GetExecutingAssembly();
            string resName = ResourceName(asm, name);
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
                if (File.Exists(target) && new FileInfo(target).Length == src.Length)
                    return true; // already unpacked and current

                Directory.CreateDirectory(Dir);
                string tmp = target + "." + Guid.NewGuid().ToString("N") + ".tmp";
                using (var dst = new FileStream(tmp, FileMode.Create, FileAccess.Write, FileShare.None))
                    src.CopyTo(dst);
                ReplaceFile(tmp, target);
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

        // Resource names are prefixed with the assembly name by csc; match by suffix.
        static string ResourceName(Assembly asm, string name)
        {
            foreach (string r in asm.GetManifestResourceNames())
                if (r.Equals(name, StringComparison.OrdinalIgnoreCase) ||
                    r.EndsWith("." + name, StringComparison.OrdinalIgnoreCase))
                    return r;
            return null;
        }
    }
}
