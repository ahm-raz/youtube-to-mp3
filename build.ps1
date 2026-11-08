# Builds a SINGLE-FILE YouTubeToMp3.exe with yt-dlp.exe, ffmpeg.exe and aria2c.exe embedded
# (gzip-compressed) inside it. On another PC you just copy that one .exe; it unpacks the tools
# on first run. -Install also copies the exe to %LOCALAPPDATA%\Programs\YouTubeToMp3, adds a
# Start-menu shortcut, and launches it.
param([switch]$Install)
$ErrorActionPreference = 'Stop'
[Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12
Add-Type -AssemblyName System.IO.Compression.FileSystem

$csc = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
$bin = Join-Path $PSScriptRoot 'bin'
New-Item -ItemType Directory -Force $bin | Out-Null

function Get-Cached($zip, $url) {
    $ok = $false
    if (Test-Path $zip) { try { [IO.Compression.ZipFile]::OpenRead($zip).Dispose(); $ok = $true } catch { Remove-Item $zip -Force } }
    if (-not $ok) { Invoke-WebRequest -UseBasicParsing -Uri $url -OutFile $zip }
}
function Extract-Exe($zip, $name, $dest) {
    $tmp = Join-Path $env:TEMP ('x-' + [Guid]::NewGuid().ToString('N'))
    Expand-Archive -LiteralPath $zip -DestinationPath $tmp -Force
    $f = Get-ChildItem -LiteralPath $tmp -Recurse -Filter $name | Select-Object -First 1
    if (-not $f) { throw "$name not found in $zip" }
    Copy-Item -LiteralPath $f.FullName -Destination $dest -Force
    Remove-Item -LiteralPath $tmp -Recurse -Force -ErrorAction SilentlyContinue
}

# --- 1. yt-dlp.exe ---
$ytDlp = Join-Path $bin 'yt-dlp.exe'
if (-not (Test-Path $ytDlp)) {
    "Downloading yt-dlp.exe ..."
    Invoke-WebRequest -UseBasicParsing -Uri 'https://github.com/yt-dlp/yt-dlp/releases/latest/download/yt-dlp.exe' -OutFile $ytDlp
} else { "yt-dlp.exe already present" }

# --- 2. ffmpeg.exe (GitHub-hosted static build = fast & reliable) ---
$ffmpeg = Join-Path $bin 'ffmpeg.exe'
if (-not (Test-Path $ffmpeg)) {
    "Downloading ffmpeg ..."
    $zip = Join-Path $bin 'ffmpeg-dl.zip'
    Get-Cached $zip 'https://github.com/BtbN/FFmpeg-Builds/releases/download/latest/ffmpeg-master-latest-win64-gpl.zip'
    Extract-Exe $zip 'ffmpeg.exe' $ffmpeg
} else { "ffmpeg.exe already present" }

# --- 3. aria2c.exe (optional multi-connection downloader for speed) ---
$aria = Join-Path $bin 'aria2c.exe'
if (-not (Test-Path $aria)) {
    "Downloading aria2c ..."
    $zip = Join-Path $bin 'aria2-dl.zip'
    Get-Cached $zip 'https://github.com/aria2/aria2/releases/download/release-1.37.0/aria2-1.37.0-win-64bit-build1.zip'
    Extract-Exe $zip 'aria2c.exe' $aria
} else { "aria2c.exe already present" }

# --- 4. Gzip-compress the tools so the embedded exe stays small ---
function Gzip($srcPath) {
    $dst = "$srcPath.gz"
    $in = [IO.File]::OpenRead($srcPath); $out = [IO.File]::Create($dst)
    $gz = New-Object IO.Compression.GZipStream($out, [IO.Compression.CompressionLevel]::Optimal)
    try { $in.CopyTo($gz) } finally { $gz.Dispose(); $out.Dispose(); $in.Dispose() }
    Write-Host ("  $(Split-Path $srcPath -Leaf): {0} MB -> {1} MB" -f [math]::Round((Get-Item $srcPath).Length/1MB,1), [math]::Round((Get-Item $dst).Length/1MB,1))
    return $dst
}
"Compressing tools ..."
$ytGz = Gzip $ytDlp
$ffGz = Gzip $ffmpeg
$arGz = Gzip $aria

# --- 5. Compile the WinForms app with the compressed tools embedded as resources ---
$exe = Join-Path $bin 'YouTubeToMp3.exe'
"Compiling single-file exe ..."
& $csc /nologo /target:winexe /optimize+ /codepage:65001 "/out:$exe" `
    /r:System.Windows.Forms.dll /r:System.Drawing.dll `
    "/resource:$ytGz,yt-dlp.exe.gz" "/resource:$ffGz,ffmpeg.exe.gz" "/resource:$arGz,aria2c.exe.gz" `
    "/recurse:$(Join-Path $PSScriptRoot 'src\*.cs')"
if ($LASTEXITCODE -ne 0) { throw 'Build failed' }
"Built $exe ({0} MB, self-contained)" -f [math]::Round((Get-Item $exe).Length/1MB,1)

if ($Install) {
    $dest = Join-Path $env:LOCALAPPDATA 'Programs\YouTubeToMp3'
    New-Item -ItemType Directory -Force $dest | Out-Null
    Copy-Item -LiteralPath $exe -Destination $dest -Force
    $installed = Join-Path $dest 'YouTubeToMp3.exe'
    $shortcut = (New-Object -ComObject WScript.Shell).CreateShortcut(
        (Join-Path ([Environment]::GetFolderPath('Programs')) 'YouTube Downloader.lnk'))
    $shortcut.TargetPath = $installed
    $shortcut.WorkingDirectory = $dest
    $shortcut.Description = 'Convert videos to MP3 or MP4'
    $shortcut.Save()
    Start-Process $installed
    "Installed to $installed"
}
