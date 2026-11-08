# 🎬 YouTube Downloader

Save any YouTube video as an **MP3** (just the audio — music, podcasts) or an **MP4** (the full
video) on your Windows PC. Works for a single video or a whole playlist.

No setup, no sign-up, no ads. Just one file — open it and go.

> Please only download videos you own or have permission to download (your own uploads,
> Creative Commons, royalty-free music, etc.).

---

## ⬇️ Get it

**[Click here to download the app](https://github.com/ahm-raz/youtube-to-mp3/releases/latest/download/YouTubeToMp3.exe)** (one file, about 88 MB)

Save it anywhere you like — your Desktop is fine. Need help installing? → **[SETUP.md](SETUP.md)**

## ▶️ How to use it (30 seconds)

1. **Open** the file you downloaded (`YouTubeToMp3.exe`).
   - The first time, Windows might show a blue "Windows protected your PC" box. Click
     **More info**, then **Run anyway**. (This appears for any small app — it's safe to continue.)
2. **Copy** the link of the video you want (from your browser's address bar).
3. **Paste** it into the app (there's a Paste button).
4. Choose **MP3** (audio) or **MP4** (video), and pick the quality.
5. Click the big **Download** button.

That's it. Your file is saved in your **Music → YouTubeToMp3** folder. Click **Open folder** in
the app to find it instantly.

## ✨ What it can do

- 🎵 **MP3 audio** or 🎥 **MP4 video** — your choice
- 📃 **Whole playlists** — it lists every video and shows which ones are done
- ⚡ **Fast** — downloads in multiple pieces at once
- ⚙️ **Settings** (the gear button) you can turn on for yourself:
  - **Dark mode**
  - Open the folder automatically when a download finishes
  - Add cover art and song info to MP3s
  - Download several videos at the same time
- 💾 **One file** — copy it to any other Windows PC and it just works. It remembers your choices.

## ❓ Common questions

**Where did my file go?** → Your **Music → YouTubeToMp3** folder. Click **Open folder** in the app.

**A video says "Failed"** → It may be private or unavailable. Other videos in a playlist keep going.

**Is it free?** → Yes, completely.

**Can I put it on another computer?** → Yes — just copy the one file over.

**How do I install it?** → See **[SETUP.md](SETUP.md)**

**Need more help using it?** → See the step-by-step guide: **[HowToUse.md](HowToUse.md)**

---

<details>
<summary>🛠️ For developers (building from source)</summary>

A lightweight C# WinForms app that wraps the open-source tools
[yt-dlp](https://github.com/yt-dlp/yt-dlp), [ffmpeg](https://ffmpeg.org/) and
[aria2c](https://aria2.github.io/). The build downloads those three tools, gzip-compresses them,
and embeds them inside a single `.exe` that unpacks them on first run.

```powershell
.\build.ps1          # downloads the tools, builds bin\YouTubeToMp3.exe (~88 MB)
.\build.ps1 -Install # also installs to %LOCALAPPDATA%\Programs + Start-menu shortcut, then launches
```

Source layout:

```
src/Program.cs    entry point
src/MainForm.cs   the window: header, MP3/MP4 toggle, downloads list, settings, theming
src/Converter.cs  lists playlist items, downloads (one at a time or in parallel), tracks progress
src/Settings.cs   saved preferences (%LOCALAPPDATA%\YouTubeToMp3\settings.txt)
src/Theme.cs      light / dark palettes
src/Tools.cs      unpacks the embedded yt-dlp + ffmpeg + aria2c on first run
src/AppIcon.cs    app icon, drawn in code
src/Native.cs     the few Win32 calls used
build.ps1         fetch tools, compress, compile single-file exe, optional install
```

MP4 prefers H.264 + AAC for broad device compatibility. Files save to `Music\YouTubeToMp3`.

</details>
