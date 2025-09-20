// UPDATED 2025-09-20 (nullable-friendly; robust file handling)
using System;
using System.IO;
using System.Web.Script.Serialization;

namespace AIPilot.POC
{
    public sealed class AppConfig
    {
        public SimConnectSection SimConnect { get; set; } = new SimConnectSection();
        public LoggingSection    Logging    { get; set; } = new LoggingSection();
        public Level1Section     Level1     { get; set; } = new Level1Section();

        public sealed class SimConnectSection { public int    UpdateHz { get; set; } = 5;     }
        public sealed class LoggingSection    { public string Level    { get; set; } = "INFO"; }
        public sealed class Level1Section
        {
            public int TargetAltitudeFeet { get; set; } = 4500;
            public int TaxiSpeedKtsMax    { get; set; } = 15;
        }

        public static AppConfig Load(string? path = null)
        {
            try
            {
                var baseDir = AppDomain.CurrentDomain.BaseDirectory;
                var usePath = path ?? Path.Combine(baseDir, "appsettings.json");

                if (!File.Exists(usePath))
                    return new AppConfig();

                var json = File.ReadAllText(usePath);
                var ser  = new JavaScriptSerializer();

                var cfg = ser.Deserialize<AppConfig>(json) ?? new AppConfig();
                Logger.Info(
                    $"Config loaded from {usePath}: " +
                    $"UpdateHz={cfg.SimConnect.UpdateHz}, " +
                    $"LogLevel={cfg.Logging.Level}, " +
                    $"TargetAlt={cfg.Level1.TargetAltitudeFeet}, " +
                    $"TaxiMax={cfg.Level1.TaxiSpeedKtsMax}"
                );
                return cfg;
            }
            catch (Exception ex)
            {
                Logger.Warn("Config load failed: " + ex.Message);
                return new AppConfig();
            }
        }
    }
}
