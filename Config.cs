using System;
using System.IO;
using System.Text.Json;

namespace LeagueSpellTracker
{
    public class Config
    {
        public double Scale { get; set; } = 1.0;
        public double WindowLeft { get; set; } = 100;
        public double WindowTop { get; set; } = 100;
        public bool UseMinuteFormat { get; set; } = false;

        private static string ConfigPath => Path.Combine(
            AppDomain.CurrentDomain.BaseDirectory,
            "config.json"
        );

        public static Config Load()
        {
            try
            {
                if (File.Exists(ConfigPath))
                {
                    string json = File.ReadAllText(ConfigPath);
                    return JsonSerializer.Deserialize<Config>(json) ?? new Config();
                }
            }
            catch { }
            return new Config();
        }

        public void Save()
        {
            try
            {
                string json = JsonSerializer.Serialize(this);
                File.WriteAllText(ConfigPath, json);
            }
            catch { }
        }
    }
} 