# YouTube to MP3

A small Windows desktop app that converts a video to an MP3 audio file. It's a lightweight
WinForms front-end over the standard open-source tools **[yt-dlp](https://github.com/yt-dlp/yt-dlp)**
(download) and **[ffmpeg](https://ffmpeg.org/)** (encode).

> Use it only for content you own or have the rights to download (your own uploads,
> Creative Commons, etc.). Downloading copyrighted material may violate a site's Terms of Service.

## ⬇️ Download (no install needed)

**[Download YouTubeToMp3.exe](https://github.com/ahm-raz/youtube-to-mp3/releases/latest/download/YouTubeToMp3.exe)** — one file, ~100 MB.

1. Click the link above and save the `.exe`.
2. Double-click it to run. (Windows SmartScreen may warn about an unknown publisher — click
   **More info → Run anyway**.)
3. Paste a video URL, pick a quality, click **Convert to MP3**. Done.

Nothing to install — yt-dlp and ffmpeg are **built into the exe** and unpacked automatically on
first run. You can copy this single file to any Windows PC.

👉 New here? See **[HowToUse.md](HowToUse.md)** for a step-by-step walkthrough.

## Features

- Paste a URL, pick a quality, click **Convert to MP3**
- Quality presets: Best (VBR), 320 / 256 / 192 / 128 kbps
- Fast downloads (8 concurrent fragments)
- Optional whole-playlist download
- Embeds thumbnail + metadata into the MP3
- Live progress bar and log
- **Single self-contained `.exe`** — copy just the one file to another PC

## Build from source

Requires Windows with the .NET Framework C# compiler (ships with Windows). From the project folder:

```powershell
.\build.ps1          # downloads yt-dlp + ffmpeg, builds a single-file exe into .\bin
.\build.ps1 -Install # also installs to %LOCALAPPDATA%\Programs\YouTubeToMp3 + Start-menu shortcut, then launches
```

The build embeds `yt-dlp.exe` and `ffmpeg.exe` into `YouTubeToMp3.exe`. The resulting exe
(~100 MB) is all you need to run or share.

## How it works

On first run the app unpacks the embedded tools to
`%LOCALAPPDATA%\YouTubeToMp3\tools`, then runs:

```
yt-dlp --extract-audio --audio-format mp3 --audio-quality <q> -N 8 \
       --embed-thumbnail --embed-metadata --ffmpeg-location <dir> -o "<out>\%(title)s.%(ext)s" <url>
```

MP3s are saved to `Music\YouTubeToMp3` by default (changeable in the app).

## Project layout

```
src/Program.cs      entry point
src/MainForm.cs     the window (URL, quality, progress, log)
src/Converter.cs    drives yt-dlp, parses progress
src/Tools.cs        unpacks the embedded yt-dlp + ffmpeg on first run
src/AppIcon.cs      app icon, drawn in code
src/Native.cs       the couple of Win32 calls used
build.ps1           fetch tools, compile single-file exe, optional install
```
