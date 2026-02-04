using System.Collections.Generic;

namespace PostGisTools.Models
{
    public class AppConfig
    {
        public ConnectionSettings Connection { get; set; } = new();
        public List<FieldConfig> FieldConfigs { get; set; } = new();
    }

    public class ConnectionSettings
    {
        public string Host { get; set; } = "localhost";
        public int Port { get; set; } = 5432;
        public string Database { get; set; } = string.Empty;
        public string Username { get; set; } = string.Empty;
    }

    public class FieldConfig
    {
        public string Schema { get; set; } = string.Empty;
        public string Table { get; set; } = string.Empty;
        public string Column { get; set; } = string.Empty;

        public bool IsVisible { get; set; } = true;
        public string Alias { get; set; } = string.Empty;
        public string LocalType { get; set; } = string.Empty;
        public int? LocalLength { get; set; }
        public string LocalDefault { get; set; } = string.Empty;
    }
}
