using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using PostGisTools.Models;

namespace PostGisTools.Services
{
    public interface IAppConfigService
    {
        AppConfig Load();
        void Save(AppConfig config);

        ConnectionSettings LoadConnection();
        void SaveConnection(ConnectionSettings settings);

        List<FieldConfig> LoadFieldConfigs();
        void SaveFieldConfigs(IEnumerable<FieldConfig> configs);
    }

    public sealed class AppConfigService : IAppConfigService
    {
        private readonly string _configPath;
        private readonly object _lock = new();

        public AppConfigService()
        {
            var folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "PostGisTools");
            Directory.CreateDirectory(folder);
            _configPath = Path.Combine(folder, "config.json");
        }

        public AppConfig Load()
        {
            lock (_lock)
            {
                if (!File.Exists(_configPath))
                    return new AppConfig();

                var json = File.ReadAllText(_configPath);
                if (string.IsNullOrWhiteSpace(json))
                    return new AppConfig();

                return JsonSerializer.Deserialize<AppConfig>(json) ?? new AppConfig();
            }
        }

        public void Save(AppConfig config)
        {
            lock (_lock)
            {
                var json = JsonSerializer.Serialize(config, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(_configPath, json);
            }
        }

        public ConnectionSettings LoadConnection()
            => Load().Connection;

        public void SaveConnection(ConnectionSettings settings)
        {
            var config = Load();
            config.Connection = settings;
            Save(config);
        }

        public List<FieldConfig> LoadFieldConfigs()
            => Load().FieldConfigs ?? new List<FieldConfig>();

        public void SaveFieldConfigs(IEnumerable<FieldConfig> configs)
        {
            var config = Load();
            config.FieldConfigs = configs.ToList();
            Save(config);
        }
    }
}
