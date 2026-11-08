# Builds a SINGLE-FILE YouTubeToMp3.exe with yt-dlp.exe and ffmpeg.exe embedded inside it.
# On another PC you just copy that one .exe; it unpacks the tools on first run.
# -Install also copies the exe to %LOCALAPPDATA%\Programs\YouTubeToMp3,
# adds a Start-menu shortcut, and launches it.
param([switch]$Install)
$ErrorActionPreference = 'Stop'
[Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12

$csc = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
$bin = Join-Path $PSScriptRoot 'bin'
New-Item -ItemType Directory -Force $bin | Out-Null

# --- 1. yt-dlp.exe (latest release) -- fetched first so it can be embedded ---
$ytDlp = Join-Path $bin 'yt-dlp.exe'
if (-not (Test-Path $ytDlp)) {
    "Downloading yt-dlp.exe ..."
    Invoke-WebRequest -Uri 'https://github.com/yt-dlp/yt-dlp/releases/latest/download/yt-dlp.exe' `
        -OutFile $ytDlp -UseBasicParsing
} else { "yt-dlp.exe already present" }

# --- 2. ffmpeg.exe (static essentials build) ---
$ffmpeg = Join-Path $bin 'ffmpeg.exe'
if (-not (Test-Path $ffmpeg)) {
    "Downloading ffmpeg (one-time, ~80 MB) ..."
    $zip = Join-Path $env:TEMP 'ffmpeg-essentials.zip'
    Invoke-WebRequest -Uri 'https://www.gyan.dev/ffmpeg/builds/ffmpeg-release-essentials.zip' `
        -OutFile $zip -UseBasicParsing
    $tmp = Join-Path $env:TEMP ('ffmpeg-' + [Guid]::NewGuid().ToString('N'))
    Expand-Archive -LiteralPath $zip -DestinationPath $tmp -Force
    $found = Get-ChildItem -LiteralPath $tmp -Recurse -Filter 'ffmpeg.exe' | Select-Object -First 1
    if (-not $found) { throw 'ffmpeg.exe not found in the downloaded archive' }
    Copy-Item -LiteralPath $found.FullName -Destination $ffmpeg -Force
    Remove-Item -LiteralPath $zip, $tmp -Recurse -Force -ErrorAction SilentlyContinue
} else { "ffmpeg.exe already present" }

# --- 3. Compile the WinForms app with both tools embedded as resources ---
$exe = Join-Path $bin 'YouTubeToMp3.exe'
"Compiling single-file exe (embedding yt-dlp + ffmpeg) ..."
& $csc /nologo /target:winexe /optimize+ /codepage:65001 "/out:$exe" `
    /r:System.Windows.Forms.dll /r:System.Drawing.dll `
    "/resource:$ytDlp,yt-dlp.exe" "/resource:$ffmpeg,ffmpeg.exe" `
    "/recurse:$(Join-Path $PSScriptRoot 'src\*.cs')"
if ($LASTEXITCODE -ne 0) { throw 'Build failed' }
$mb = [math]::Round((Get-Item $exe).Length / 1MB, 1)
"Built $exe ($mb MB, self-contained)"

if ($Install) {
    $dest = Join-Path $env:LOCALAPPDATA 'Programs\YouTubeToMp3'
    New-Item -ItemType Directory -Force $dest | Out-Null
    Copy-Item -LiteralPath $exe -Destination $dest -Force   # just the one file
    $installed = Join-Path $dest 'YouTubeToMp3.exe'

    # Start-menu shortcut so Windows search finds "YouTube to MP3".
    $shortcut = (New-Object -ComObject WScript.Shell).CreateShortcut(
        (Join-Path ([Environment]::GetFolderPath('Programs')) 'YouTube to MP3.lnk'))
    $shortcut.TargetPath = $installed
    $shortcut.WorkingDirectory = $dest
    $shortcut.Description = 'Convert a video to an MP3 audio file'
    $shortcut.Save()

    Start-Process $installed
    "Installed to $installed"
    "To use on another PC: copy just this one file -> $installed"
}
