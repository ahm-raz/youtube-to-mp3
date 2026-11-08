using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Threading;
using System.Windows.Forms;

namespace YouTubeToMp3
{
    sealed class MainForm : Form
    {
        readonly TextBox urlBox;
        readonly TextBox folderBox;
        readonly ComboBox qualityBox;
        readonly CheckBox playlistBox;
        readonly Button convertBtn;
        readonly Button folderBtn;
        readonly Button openBtn;
        readonly ProgressBar progress;
        readonly Label status;
        readonly TextBox logBox;

        Thread worker;
        volatile bool running;

        static readonly string DefaultDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.MyMusic), "YouTubeToMp3");

        public MainForm()
        {
            SuspendLayout();
            AutoScaleDimensions = new SizeF(96F, 96F);
            AutoScaleMode = AutoScaleMode.Dpi;
            Text = "YouTube to MP3";
            Icon = AppIcon.Create();
            Font = new Font("Segoe UI", 9F);
            BackColor = Color.White;
            FormBorderStyle = FormBorderStyle.FixedSingle;
            MaximizeBox = false;
            StartPosition = FormStartPosition.CenterScreen;
            ClientSize = new Size(560, 440);

            Color muted = Color.FromArgb(100, 100, 110);

            AddLabel("Video URL", 22, 16, muted);
            urlBox = new TextBox { Location = new Point(22, 38), Size = new Size(516, 26), Font = new Font("Segoe UI", 10F) };
            urlBox.Text = "";
            Controls.Add(urlBox);
            var pasteBtn = new Button { Text = "Paste", Location = new Point(463, 12), Size = new Size(75, 22), FlatStyle = FlatStyle.System };
            pasteBtn.Click += (s, e) => { if (Clipboard.ContainsText()) urlBox.Text = Clipboard.GetText().Trim(); };
            Controls.Add(pasteBtn);

            AddLabel("Save to folder", 22, 78, muted);
            folderBox = new TextBox { Location = new Point(22, 100), Size = new Size(416, 26), Text = DefaultDir };
            Controls.Add(folderBox);
            folderBtn = new Button { Text = "Browse…", Location = new Point(448, 99), Size = new Size(90, 28), FlatStyle = FlatStyle.System };
            folderBtn.Click += (s, e) => BrowseFolder();
            Controls.Add(folderBtn);

            AddLabel("Quality", 22, 140, muted);
            qualityBox = new ComboBox { Location = new Point(22, 162), Size = new Size(160, 26), DropDownStyle = ComboBoxStyle.DropDownList };
            qualityBox.Items.AddRange(new object[] { "Best (VBR)", "320 kbps", "256 kbps", "192 kbps", "128 kbps" });
            qualityBox.SelectedIndex = 1;
            Controls.Add(qualityBox);

            playlistBox = new CheckBox { Text = "Download whole playlist", Location = new Point(210, 164), AutoSize = true };
            Controls.Add(playlistBox);

            convertBtn = new Button
            {
                Text = "Convert to MP3",
                Location = new Point(22, 206),
                Size = new Size(516, 44),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(220, 38, 38),
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 11F, FontStyle.Bold),
            };
            convertBtn.FlatAppearance.BorderSize = 0;
            convertBtn.Click += (s, e) => Start();
            Controls.Add(convertBtn);

            progress = new ProgressBar { Location = new Point(22, 262), Size = new Size(516, 14), Style = ProgressBarStyle.Continuous };
            Controls.Add(progress);

            status = new Label { Location = new Point(22, 282), AutoSize = true, ForeColor = muted, Text = "Ready." };
            Controls.Add(status);

            openBtn = new Button { Text = "Open folder", Location = new Point(448, 282), Size = new Size(90, 26), FlatStyle = FlatStyle.System };
            openBtn.Click += (s, e) => OpenFolder();
            Controls.Add(openBtn);

            logBox = new TextBox
            {
                Location = new Point(22, 316),
                Size = new Size(516, 108),
                Multiline = true,
                ReadOnly = true,
                ScrollBars = ScrollBars.Vertical,
                BackColor = Color.FromArgb(248, 248, 250),
                Font = new Font("Consolas", 8.5F),
            };
            Controls.Add(logBox);

            ResumeLayout(false);
            PerformLayout();
        }

        void AddLabel(string text, int x, int y, Color color)
        {
            Controls.Add(new Label { Text = text, AutoSize = true, Location = new Point(x, y), ForeColor = color });
        }

        void BrowseFolder()
        {
            using (var dlg = new FolderBrowserDialog { SelectedPath = folderBox.Text })
                if (dlg.ShowDialog(this) == DialogResult.OK)
                    folderBox.Text = dlg.SelectedPath;
        }

        void OpenFolder()
        {
            try
            {
                string dir = folderBox.Text;
                Directory.CreateDirectory(dir);
                Process.Start("explorer.exe", "\"" + dir + "\"");
            }
            catch { }
        }

        string Bitrate()
        {
            switch (qualityBox.SelectedIndex)
            {
                case 0: return "0";
                case 1: return "320";
                case 2: return "256";
                case 3: return "192";
                default: return "128";
            }
        }

        void Start()
        {
            if (running) return;
            string url = urlBox.Text.Trim();
            if (url.Length == 0)
            {
                MessageBox.Show(this, "Paste a video URL first.", "YouTube to MP3",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            var converter = new Converter();

            converter.Progress += pct => BeginInvoke((MethodInvoker)(() =>
            {
                progress.Value = (int)Math.Max(0, Math.Min(100, pct));
                status.Text = "Downloading… " + pct.ToString("0.0") + "%";
            }));
            converter.StageChanged += msg => BeginInvoke((MethodInvoker)(() => status.Text = msg));
            converter.Log += line => BeginInvoke((MethodInvoker)(() => AppendLog(line)));

            string dir = folderBox.Text.Trim();
            string bitrate = Bitrate();
            bool playlist = playlistBox.Checked;

            SetRunning(true);
            logBox.Clear();
            progress.Value = 0;
            status.Text = "Starting…";

            worker = new Thread(() =>
            {
                int code = -1;
                string err = null;
                string missing = null;
                try
                {
                    BeginInvoke((MethodInvoker)(() => status.Text = "Preparing tools (first run may take a moment)…"));
                    if (!converter.ToolsPresent(out missing))
                        throw new FileNotFoundException(missing + " could not be prepared.");
                    BeginInvoke((MethodInvoker)(() => status.Text = "Starting…"));
                    code = converter.Run(url, dir, bitrate, playlist);
                }
                catch (Exception ex) { err = ex.Message; }

                BeginInvoke((MethodInvoker)(() =>
                {
                    SetRunning(false);
                    if (err != null)
                    {
                        status.Text = "Error: " + err;
                    }
                    else if (code == 0)
                    {
                        progress.Value = 100;
                        status.Text = "Done. Saved to " + dir;
                        string f = converter.LastOutputFile;
                        if (f != null) AppendLog("Saved: " + f);
                    }
                    else
                    {
                        status.Text = "yt-dlp exited with code " + code + " (see log).";
                    }
                }));
            });
            worker.IsBackground = true;
            worker.Start();
        }

        void SetRunning(bool on)
        {
            running = on;
            convertBtn.Enabled = !on;
            convertBtn.Text = on ? "Converting…" : "Convert to MP3";
            urlBox.Enabled = folderBox.Enabled = folderBtn.Enabled = qualityBox.Enabled = playlistBox.Enabled = !on;
            UseWaitCursor = on;
        }

        void AppendLog(string line)
        {
            if (logBox.TextLength > 20000) logBox.Clear();
            logBox.AppendText(line + Environment.NewLine);
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if (running)
            {
                var r = MessageBox.Show(this, "A conversion is still running. Quit anyway?",
                    "YouTube to MP3", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
                if (r != DialogResult.Yes) { e.Cancel = true; return; }
            }
            base.OnFormClosing(e);
        }
    }
}
