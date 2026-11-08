using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Threading;
using System.Windows.Forms;

namespace YouTubeToMp3
{
    sealed class MainForm : Form
    {
        readonly Settings cfg = Settings.Load();
        Theme th;

        Panel header, main, settings;
        Label title, subtitle;
        Button gearBtn;

        // Main controls
        TextBox urlBox, folderBox;
        Button pasteBtn, mp3Btn, mp4Btn, browseBtn, downloadBtn;
        ComboBox qualityBox;
        CheckBox playlistBox;
        ProgressBar progress;
        Label qualityLabel, statusLabel;
        LinkLabel openLink, detailsLink;
        ListView itemsList;
        TextBox logBox;

        // Settings controls
        CheckBox fastChk, thumbChk, metaChk, openDoneChk, darkChk;
        ComboBox parallelBox;
        Label[] settingLabels;

        ListViewItem[] rows = new ListViewItem[0];
        int doneCount, totalCount;
        Thread worker;
        volatile bool running;
        bool logShown;

        const int HeaderH = 72;
        const int CollapsedH = 628, ExpandedH = 760;

        public MainForm()
        {
            th = cfg.DarkMode ? Theme.Dark() : Theme.Light();

            SuspendLayout();
            AutoScaleMode = AutoScaleMode.None;
            Text = "YouTube Downloader";
            Icon = AppIcon.Create();
            Font = new Font("Segoe UI", 9.5F);
            FormBorderStyle = FormBorderStyle.FixedSingle;
            MaximizeBox = false;
            StartPosition = FormStartPosition.CenterScreen;
            ClientSize = new Size(600, CollapsedH);

            BuildHeader();
            BuildMain();
            BuildSettings();
            Controls.Add(main);
            Controls.Add(settings);
            Controls.Add(header);

            ResumeLayout(false);
            ApplyTheme();
            ApplyConfigToUi();
            ShowSettings(false);
        }

        // ---------- Header ----------
        void BuildHeader()
        {
            header = new Panel { Dock = DockStyle.Top, Height = HeaderH };
            header.Paint += (s, e) =>
            {
                using (var b = new LinearGradientBrush(header.ClientRectangle, th.HeaderFrom, th.HeaderTo, 15F))
                    e.Graphics.FillRectangle(b, header.ClientRectangle);
            };
            title = new Label
            {
                Text = "YouTube Downloader", AutoSize = true, ForeColor = Color.White,
                Font = new Font("Segoe UI Semibold", 15F, FontStyle.Bold),
                Location = new Point(22, 13), BackColor = Color.Transparent,
            };
            subtitle = new Label
            {
                Text = "Convert to MP3 audio or MP4 video", AutoSize = true,
                ForeColor = Color.FromArgb(255, 235, 238), Font = new Font("Segoe UI", 9F),
                Location = new Point(24, 43), BackColor = Color.Transparent,
            };
            gearBtn = new Button
            {
                Text = "⚙", Font = new Font("Segoe UI", 15F), ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat, Size = new Size(44, 44), Location = new Point(540, 14),
                BackColor = Color.Transparent, Cursor = Cursors.Hand, TabStop = false,
            };
            gearBtn.FlatAppearance.BorderSize = 0;
            gearBtn.FlatAppearance.MouseOverBackColor = Color.FromArgb(40, 255, 255, 255);
            gearBtn.Click += (s, e) => ShowSettings(!settings.Visible);
            header.Controls.Add(title); header.Controls.Add(subtitle); header.Controls.Add(gearBtn);
        }

        // ---------- Main view ----------
        void BuildMain()
        {
            main = new Panel { Location = new Point(0, HeaderH), Size = new Size(600, CollapsedH - HeaderH) };
            int x = 24, w = 552;

            var urlLabel = Caption("Video or playlist URL", x, 12);
            urlBox = Field(x, 34, 456, 30); urlBox.Font = new Font("Segoe UI", 10.5F);
            pasteBtn = Ghost("Paste", x + 468, 34, 84, 30);
            pasteBtn.Click += (s, e) => { if (Clipboard.ContainsText()) urlBox.Text = Clipboard.GetText().Trim(); };

            var fmtLabel = Caption("Format", x, 76);
            mp3Btn = Segment("♪  MP3  ·  Audio", x, 98, 268, 44);
            mp4Btn = Segment("▶  MP4  ·  Video", x + 284, 98, 268, 44);
            mp3Btn.Click += (s, e) => SetVideo(false);
            mp4Btn.Click += (s, e) => SetVideo(true);

            qualityLabel = Caption("Quality", x, 154);
            qualityBox = new ComboBox
            {
                Location = new Point(x, 176), Size = new Size(268, 30),
                DropDownStyle = ComboBoxStyle.DropDownList, FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 10F),
            };
            qualityBox.SelectedIndexChanged += (s, e) => PersistQuality();
            playlistBox = new CheckBox
            {
                Text = "  Download the whole playlist", Location = new Point(x + 288, 180),
                AutoSize = true, Cursor = Cursors.Hand, Tag = "toggle", Font = new Font("Segoe UI", 10F),
            };
            playlistBox.CheckedChanged += (s, e) => { cfg.Playlist = playlistBox.Checked; cfg.Save(); };

            var folderLabel = Caption("Save to folder", x, 218);
            folderBox = Field(x, 240, 452, 30);
            browseBtn = Ghost("Browse", x + 464, 240, 88, 30);
            browseBtn.Click += (s, e) => BrowseFolder();

            downloadBtn = new Button
            {
                Text = "Download", Location = new Point(x, 284), Size = new Size(w, 46),
                FlatStyle = FlatStyle.Flat, Font = new Font("Segoe UI Semibold", 12F, FontStyle.Bold),
                Cursor = Cursors.Hand,
            };
            downloadBtn.FlatAppearance.BorderSize = 0;
            downloadBtn.Click += (s, e) => Start();

            progress = new ProgressBar { Location = new Point(x, 340), Size = new Size(w, 6), Style = ProgressBarStyle.Continuous };
            statusLabel = new Label { Location = new Point(x, 352), AutoSize = true, Text = "Ready." };
            openLink = new LinkLabel { Text = "Open folder", AutoSize = true, Location = new Point(x + w - 76, 352), Visible = false };
            openLink.LinkClicked += (s, e) => OpenFolder();

            itemsList = new ListView
            {
                Location = new Point(x, 376), Size = new Size(w, 150), View = View.Details,
                FullRowSelect = true, HeaderStyle = ColumnHeaderStyle.Nonclickable, MultiSelect = false,
                Font = new Font("Segoe UI", 9F), Tag = "list",
            };
            itemsList.Columns.Add("#", 34);
            itemsList.Columns.Add("Title", 400);
            itemsList.Columns.Add("Status", 110);
            itemsList.DoubleClick += (s, e) => OpenFolder();

            detailsLink = new LinkLabel { Text = "Show log ▾", AutoSize = true, Location = new Point(x, 536) };
            detailsLink.LinkClicked += (s, e) => ToggleLog();
            logBox = new TextBox
            {
                Location = new Point(x, 560), Size = new Size(w, 120), Multiline = true, ReadOnly = true,
                ScrollBars = ScrollBars.Vertical, Font = new Font("Consolas", 8.5F), Visible = false,
                BorderStyle = BorderStyle.FixedSingle,
            };

            main.Controls.AddRange(new Control[]
            {
                urlLabel, urlBox, pasteBtn, fmtLabel, mp3Btn, mp4Btn, qualityLabel, qualityBox,
                playlistBox, folderLabel, folderBox, browseBtn, downloadBtn, progress, statusLabel,
                openLink, itemsList, detailsLink, logBox,
            });
        }

        // ---------- Settings view ----------
        void BuildSettings()
        {
            settings = new Panel { Location = new Point(0, HeaderH), Size = new Size(600, CollapsedH - HeaderH), Visible = false };
            int x = 24;
            var head = new Label { Text = "Settings", AutoSize = true, Location = new Point(x, 16), Font = new Font("Segoe UI Semibold", 13F, FontStyle.Bold) };

            fastChk     = Toggle("Fast downloads (aria2c multi-connection)", x, 58);
            thumbChk    = Toggle("Embed cover thumbnail into MP3 files", x, 106);
            metaChk     = Toggle("Embed metadata (title, artist, etc.)", x, 154);
            openDoneChk = Toggle("Open the folder when downloads finish", x, 202);
            darkChk     = Toggle("Dark mode", x, 250);

            var parLabel = Caption("Simultaneous downloads", x, 300);
            parallelBox = new ComboBox
            {
                Location = new Point(x, 322), Size = new Size(260, 30),
                DropDownStyle = ComboBoxStyle.DropDownList, FlatStyle = FlatStyle.Flat, Font = new Font("Segoe UI", 10F),
            };
            parallelBox.Items.AddRange(new object[]
            { "1 — one at a time (linear)", "2 at once", "3 at once", "4 at once", "5 at once" });
            parallelBox.SelectedIndexChanged += (s, e) => { cfg.Parallel = parallelBox.SelectedIndex + 1; cfg.Save(); };

            settingLabels = new[]
            {
                Hint("Opens many connections per file to beat throttling. Recommended.", x + 34, 82),
                Hint("Adds the video thumbnail as album art. MP3 only.", x + 34, 130),
                Hint("Writes song/video details into the file.", x + 34, 178),
                Hint("Jumps straight to your saved files.", x + 34, 226),
                Hint("Switch between light and dark appearance.", x + 34, 274),
                Hint("For playlists: how many videos to download at the same time.", x, 358),
            };

            fastChk.CheckedChanged     += (s, e) => { cfg.Fast = fastChk.Checked; cfg.Save(); };
            thumbChk.CheckedChanged    += (s, e) => { cfg.EmbedThumbnail = thumbChk.Checked; cfg.Save(); };
            metaChk.CheckedChanged     += (s, e) => { cfg.EmbedMetadata = metaChk.Checked; cfg.Save(); };
            openDoneChk.CheckedChanged += (s, e) => { cfg.OpenWhenDone = openDoneChk.Checked; cfg.Save(); };
            darkChk.CheckedChanged     += (s, e) => { cfg.DarkMode = darkChk.Checked; cfg.Save(); th = cfg.DarkMode ? Theme.Dark() : Theme.Light(); ApplyTheme(); };

            var back = Ghost("← Back", x, 392, 110, 38);
            back.Click += (s, e) => ShowSettings(false);
            var about = new Label { Text = "YouTube Downloader · powered by yt-dlp + ffmpeg + aria2c", AutoSize = true, Location = new Point(x, 446), Font = new Font("Segoe UI", 8.5F), Tag = "muted" };

            settings.Controls.Add(head);
            settings.Controls.AddRange(new Control[] { fastChk, thumbChk, metaChk, openDoneChk, darkChk, parLabel, parallelBox, back, about });
            foreach (var l in settingLabels) settings.Controls.Add(l);
        }

        // ---------- Styled control factories ----------
        Label Caption(string t, int x, int y) { return new Label { Text = t, AutoSize = true, Location = new Point(x, y), Font = new Font("Segoe UI Semibold", 9.5F, FontStyle.Bold), Tag = "muted" }; }
        Label Hint(string t, int x, int y) { return new Label { Text = t, AutoSize = true, Location = new Point(x, y), Font = new Font("Segoe UI", 8.5F), Tag = "muted" }; }
        TextBox Field(int x, int y, int w, int h) { return new TextBox { Location = new Point(x, y), Size = new Size(w, h), BorderStyle = BorderStyle.FixedSingle, Font = new Font("Segoe UI", 10F), Tag = "field" }; }
        Button Ghost(string t, int x, int y, int w, int h) { var b = new Button { Text = t, Location = new Point(x, y), Size = new Size(w, h), FlatStyle = FlatStyle.Flat, Cursor = Cursors.Hand, Tag = "ghost", Font = new Font("Segoe UI", 9.5F) }; b.FlatAppearance.BorderSize = 1; return b; }
        Button Segment(string t, int x, int y, int w, int h) { var b = new Button { Text = t, Location = new Point(x, y), Size = new Size(w, h), FlatStyle = FlatStyle.Flat, Cursor = Cursors.Hand, Font = new Font("Segoe UI Semibold", 10.5F, FontStyle.Bold), Tag = "segment" }; b.FlatAppearance.BorderSize = 1; return b; }
        CheckBox Toggle(string t, int x, int y) { return new CheckBox { Text = "  " + t, AutoSize = true, Location = new Point(x, y), Cursor = Cursors.Hand, Font = new Font("Segoe UI", 10F), Tag = "toggle" }; }

        // ---------- Theming ----------
        void ApplyTheme()
        {
            BackColor = th.Window; main.BackColor = th.Window; settings.BackColor = th.Window;
            foreach (Control c in AllControls(this))
            {
                if (c == header || c == title || c == subtitle || c == gearBtn) continue;
                string tag = c.Tag as string;
                if (c is LinkLabel) { var ll = (LinkLabel)c; ll.LinkColor = th.Accent; ll.ActiveLinkColor = th.AccentHover; }
                else if (tag == "muted") c.ForeColor = th.Muted;
                else if (tag == "field") { c.BackColor = th.FieldBg; c.ForeColor = th.Text; }
                else if (tag == "ghost") StyleGhost((Button)c);
                else if (tag == "toggle") c.ForeColor = th.Text;
                else if (tag == "list") { c.BackColor = th.FieldBg; c.ForeColor = th.Text; }
                else if (c is Label) c.ForeColor = th.Text;
            }
            qualityBox.BackColor = th.FieldBg; qualityBox.ForeColor = th.Text;
            parallelBox.BackColor = th.FieldBg; parallelBox.ForeColor = th.Text;
            StyleAccent(downloadBtn);
            StyleSegments();
            header.Invalidate(); Invalidate();
        }

        void StyleAccent(Button b) { b.BackColor = th.Accent; b.ForeColor = th.AccentText; b.FlatAppearance.BorderSize = 0; b.FlatAppearance.MouseOverBackColor = th.AccentHover; Round(b, 10); }
        void StyleGhost(Button b) { b.BackColor = th.Card; b.ForeColor = th.Text; b.FlatAppearance.BorderColor = th.Border; b.FlatAppearance.MouseOverBackColor = th.Window; Round(b, 8); }
        void StyleSegments() { StyleSegment(mp3Btn, !cfg.Video); StyleSegment(mp4Btn, cfg.Video); }
        void StyleSegment(Button b, bool on)
        {
            if (on) { b.BackColor = th.Accent; b.ForeColor = th.AccentText; b.FlatAppearance.BorderSize = 0; }
            else { b.BackColor = th.Card; b.ForeColor = th.Muted; b.FlatAppearance.BorderSize = 1; b.FlatAppearance.BorderColor = th.Border; }
            Round(b, 10);
        }

        static void Round(Control c, int r)
        {
            var p = new GraphicsPath(); int w = c.Width, h = c.Height, d = r * 2;
            p.AddArc(0, 0, d, d, 180, 90); p.AddArc(w - d, 0, d, d, 270, 90);
            p.AddArc(w - d, h - d, d, d, 0, 90); p.AddArc(0, h - d, d, d, 90, 90);
            p.CloseFigure(); c.Region = new Region(p);
        }

        IEnumerable<Control> AllControls(Control root)
        {
            foreach (Control c in root.Controls) { yield return c; foreach (Control d in AllControls(c)) yield return d; }
        }

        // ---------- State ----------
        void ApplyConfigToUi()
        {
            folderBox.Text = cfg.Folder;
            playlistBox.Checked = cfg.Playlist;
            fastChk.Checked = cfg.Fast;
            thumbChk.Checked = cfg.EmbedThumbnail;
            metaChk.Checked = cfg.EmbedMetadata;
            openDoneChk.Checked = cfg.OpenWhenDone;
            darkChk.Checked = cfg.DarkMode;
            parallelBox.SelectedIndex = cfg.Parallel - 1;
            SetVideo(cfg.Video);
        }

        void SetVideo(bool video)
        {
            cfg.Video = video; cfg.Save();
            qualityBox.BeginUpdate(); qualityBox.Items.Clear();
            if (video) { qualityBox.Items.AddRange(new object[] { "Best available", "1080p (Full HD)", "720p (HD)", "480p", "360p" }); qualityBox.SelectedIndex = VideoIndex(cfg.VideoQuality); }
            else { qualityBox.Items.AddRange(new object[] { "Best (VBR)", "320 kbps", "256 kbps", "192 kbps", "128 kbps" }); qualityBox.SelectedIndex = AudioIndex(cfg.AudioQuality); }
            qualityBox.EndUpdate();
            thumbChk.Enabled = !video;
            StyleSegments();
        }

        static int AudioIndex(string q) { switch (q) { case "0": return 0; case "320": return 1; case "256": return 2; case "192": return 3; case "128": return 4; default: return 1; } }
        static int VideoIndex(string q) { switch (q) { case "0": return 0; case "1080": return 1; case "720": return 2; case "480": return 3; case "360": return 4; default: return 1; } }
        string CurrentQuality()
        {
            int i = Math.Max(0, qualityBox.SelectedIndex);
            if (cfg.Video) { string[] v = { "0", "1080", "720", "480", "360" }; return v[i]; }
            string[] a = { "0", "320", "256", "192", "128" }; return a[i];
        }
        void PersistQuality()
        {
            if (qualityBox.SelectedIndex < 0) return;
            if (cfg.Video) cfg.VideoQuality = CurrentQuality(); else cfg.AudioQuality = CurrentQuality();
            cfg.Save();
        }

        void ShowSettings(bool on) { settings.Visible = on; main.Visible = !on; subtitle.Text = on ? "Preferences" : "Convert to MP3 audio or MP4 video"; }

        void ToggleLog()
        {
            logShown = !logShown; logBox.Visible = logShown;
            detailsLink.Text = logShown ? "Hide log ▴" : "Show log ▾";
            ClientSize = new Size(600, logShown ? ExpandedH : CollapsedH);
            main.Size = new Size(600, ClientSize.Height - HeaderH);
            settings.Size = main.Size;
        }

        void BrowseFolder()
        {
            using (var dlg = new FolderBrowserDialog { SelectedPath = folderBox.Text })
                if (dlg.ShowDialog(this) == DialogResult.OK) { folderBox.Text = dlg.SelectedPath; cfg.Folder = dlg.SelectedPath; cfg.Save(); }
        }

        void OpenFolder()
        {
            try { Directory.CreateDirectory(folderBox.Text); Process.Start("explorer.exe", "\"" + folderBox.Text + "\""); } catch { }
        }

        // ---------- Download ----------
        void Start()
        {
            if (running) return;
            string url = urlBox.Text.Trim();
            if (url.Length == 0) { MessageBox.Show(this, "Paste a video or playlist URL first.", "YouTube Downloader", MessageBoxButtons.OK, MessageBoxIcon.Information); return; }

            cfg.Folder = folderBox.Text.Trim(); cfg.Save();
            var job = new Converter.Job
            {
                Url = url, OutputDir = cfg.Folder, Video = cfg.Video, Quality = CurrentQuality(),
                Playlist = playlistBox.Checked, Fast = cfg.Fast, EmbedThumbnail = cfg.EmbedThumbnail,
                EmbedMetadata = cfg.EmbedMetadata, Parallel = cfg.Parallel,
            };

            var converter = new Converter();
            converter.ItemStarted  += (i, total, ttl) => BeginInvoke((MethodInvoker)(() => { SetRow(i, ttl, "Downloading…"); }));
            converter.ItemProgress += (i, pct) => BeginInvoke((MethodInvoker)(() => SetRowStatus(i, pct.ToString("0") + "%")));
            converter.ItemStage    += (i, st) => BeginInvoke((MethodInvoker)(() => SetRowStatus(i, st)));
            converter.ItemFinished += (i, ok, outp) => BeginInvoke((MethodInvoker)(() =>
            {
                SetRowStatus(i, ok ? "✓ Done" : "✕ Failed");
                doneCount++;
                progress.Value = totalCount > 0 ? (int)(100.0 * doneCount / totalCount) : 0;
                statusLabel.Text = doneCount + " of " + totalCount + " done";
            }));
            converter.Log += line => BeginInvoke((MethodInvoker)(() => AppendLog(line)));

            SetRunning(true);
            itemsList.Items.Clear(); logBox.Clear(); progress.Value = 0;
            openLink.Visible = false; doneCount = 0; totalCount = 0;
            statusLabel.Text = "Preparing…";

            worker = new Thread(() =>
            {
                string err = null, missing = null; int failures = 0; int count = 0;
                try
                {
                    BeginInvoke((MethodInvoker)(() => statusLabel.Text = "Preparing tools (first run may take a moment)…"));
                    if (!converter.ToolsPresent(out missing)) throw new FileNotFoundException(missing + " could not be prepared.");

                    BeginInvoke((MethodInvoker)(() => statusLabel.Text = "Reading playlist…"));
                    var items = converter.Enumerate(job);
                    count = items.Count;
                    BeginInvoke((MethodInvoker)(() => PopulateList(items)));

                    failures = converter.RunJob(job, items);
                }
                catch (Exception ex) { err = ex.Message; }

                int fin = failures;
                BeginInvoke((MethodInvoker)(() =>
                {
                    SetRunning(false);
                    if (err != null) statusLabel.Text = "Error: " + err;
                    else
                    {
                        progress.Value = 100; openLink.Visible = true;
                        int ok = count - fin;
                        statusLabel.Text = fin == 0 ? ("All done — " + ok + " of " + count + " saved.")
                                                    : (ok + " of " + count + " saved, " + fin + " failed (see log).");
                        if (cfg.OpenWhenDone && ok > 0) OpenFolder();
                    }
                }));
            });
            worker.IsBackground = true; worker.Start();
        }

        void PopulateList(List<Converter.Item> items)
        {
            totalCount = items.Count; doneCount = 0;
            itemsList.BeginUpdate();
            itemsList.Items.Clear();
            rows = new ListViewItem[items.Count];
            foreach (var it in items)
            {
                var lvi = new ListViewItem(new[] { it.Index.ToString(), it.Title, "Queued" });
                rows[it.Index - 1] = lvi;
                itemsList.Items.Add(lvi);
            }
            itemsList.EndUpdate();
            statusLabel.Text = "0 of " + items.Count + " done";
        }

        void SetRow(int index, string title, string status)
        {
            if (index < 1 || index > rows.Length || rows[index - 1] == null) return;
            var r = rows[index - 1];
            if (!string.IsNullOrEmpty(title) && title != "Video") r.SubItems[1].Text = title;
            r.SubItems[2].Text = status;
            r.EnsureVisible();
        }
        void SetRowStatus(int index, string status)
        {
            if (index < 1 || index > rows.Length || rows[index - 1] == null) return;
            rows[index - 1].SubItems[2].Text = status;
        }

        void SetRunning(bool on)
        {
            running = on;
            downloadBtn.Enabled = !on; downloadBtn.Text = on ? "Downloading…" : "Download";
            urlBox.Enabled = folderBox.Enabled = browseBtn.Enabled = qualityBox.Enabled =
                playlistBox.Enabled = mp3Btn.Enabled = mp4Btn.Enabled = pasteBtn.Enabled = gearBtn.Enabled = !on;
        }

        void AppendLog(string line)
        {
            if (logBox.TextLength > 40000) logBox.Clear();
            logBox.AppendText(line + Environment.NewLine);
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if (running)
            {
                var r = MessageBox.Show(this, "A download is still running. Quit anyway?", "YouTube Downloader", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
                if (r != DialogResult.Yes) { e.Cancel = true; return; }
            }
            base.OnFormClosing(e);
        }
    }
}
