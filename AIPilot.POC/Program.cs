// [UPDATED: Nullable enabled] make Config nullable; callers already use ? safely
using System;
using System.Windows.Forms;

namespace AIPilot.POC
{
    internal static class Program
    {
        public static AppConfig? Config { get; private set; }

        [STAThread]
        static void Main()
        {
            Logger.Init();
            Config = AppConfig.Load();

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new MainForm());
        }
    }
}
