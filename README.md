# YouTube Downloader (MP3 / MP4)

A small Windows desktop app that downloads a video as an **MP3** audio file or an **MP4** video —
single videos or whole playlists. It's a lightweight WinForms front-end over the standard
open-source tools **[yt-dlp](https://github.com/yt-dlp/yt-dlp)**,
**[ffmpeg](https://ffmpeg.org/)**, and **[aria2c](https://aria2.github.io/)**.

> Use it only for content you own or have the rights to download (your own uploads,
> Creative Commons, etc.). Downloading copyrighted material may violate a site's Terms of Service.

## ⬇️ Download (no install needed)

**[Download YouTubeToMp3.exe](https://github.com/ahm-raz/youtube-to-mp3/releases/latest/download/YouTubeToMp3.exe)** — one file, ~88 MB.

1. Click the link above and save the `.exe`.
2. Double-click it to run. (Windows SmartScreen may warn about an unknown publisher — click
   **More info → Run anyway**.)
3. Paste a URL, choose **MP3** or **MP4** and a quality, click **Download**. Done.

Nothing to install — yt-dlp, ffmpeg and aria2c are **built into the exe** and unpacked
automatically on first run. You can copy this single file to any Windows PC.

👉 New here? See **[HowToUse.md](HowToUse.md)** for a step-by-step walkthrough.

## Features

- **MP3 audio** (Best VBR / 320 / 256 / 192 / 128 kbps) or **MP4 video** (Best / 1080p / 720p / 480p / 360p)
- MP4 prefers **H.264 + AAC** for playback on any device
- **Playlists** — every item is listed with a live per-video status (queued → downloading % → ✓ done)
- **Fast downloads** via aria2c multi-connection (beats single-connection throttling)
- **Settings page** — fast mode, embed thumbnail/metadata, open-folder-when-done, **dark mode**,
  and **simultaneous downloads** (1 at a time, or up to 5 in parallel)
- Embeds thumbnail + metadata; remembers your preferences
- **Single self-contained `.exe`** — copy just the one file to another PC

## Build from source

Requires Windows with the .NET Framework C# compiler (ships with Windows). From the project folder:

```powershell
.\build.ps1          # downloads yt-dlp + ffmpeg, builds a single-file exe into .\bin
.\build.ps1 -Install # also installs to %LOCALAPPDATA%\Programs\YouTubeToMp3 + Start-menu shortcut, then launches
```

The build downloads `yt-dlp.exe`, `ffmpeg.exe` and `aria2c.exe`, gzip-compresses them, and embeds
them into `YouTubeToMp3.exe`. The resulting exe (~88 MB) is all you need to run or share.

## How it works

On first run the app unpacks the embedded tools to `%LOCALAPPDATA%\YouTubeToMp3\tools`. For a
playlist it first lists the items (`yt-dlp --flat-playlist --print …`) so the UI can show them,
then downloads each — one at a time or several in parallel — roughly like:

```
# MP3
yt-dlp --extract-audio --audio-format mp3 --audio-quality <q> -N 8 [--downloader aria2c] \
       --embed-thumbnail --embed-metadata --ffmpeg-location <dir> -o "<out>\%(title)s.%(ext)s" <url>

# MP4 (prefers H.264 + AAC, merged)
yt-dlp -f "bv*[vcodec^=avc1][height<=<h>]+ba[ext=m4a]/…" --merge-output-format mp4 … <url>
```

Files are saved to `Music\YouTubeToMp3` by default (changeable in the app).

## Project layout

```
src/Program.cs      entry point
src/MainForm.cs     the window: header, MP3/MP4 toggle, downloads list, settings page, theming
src/Converter.cs    enumerates playlist items, downloads (serial/parallel), parses progress
src/Settings.cs     saved preferences (%LOCALAPPDATA%\YouTubeToMp3\settings.txt)
src/Theme.cs        light / dark palettes
src/Tools.cs        unpacks the embedded, gzip'd yt-dlp + ffmpeg + aria2c on first run
src/AppIcon.cs      app icon, drawn in code
src/Native.cs       the couple of Win32 calls used
build.ps1           fetch tools, gzip, compile single-file exe, optional install
```
