using System;
using System.Windows.Forms;

namespace YouTubeToMp3
{
    static class Program
    {
        [STAThread]
        static void Main()
        {
            Native.SetProcessDPIAware();
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new MainForm());
        }
    }
}
