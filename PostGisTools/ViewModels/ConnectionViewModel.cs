using System;
using System.Globalization;
using System.Resources;
using System.Threading.Tasks;
using System.Windows.Input;
using PostGisTools.Core;
using PostGisTools.Models;
using PostGisTools.Services;

namespace PostGisTools.ViewModels
{
    public class ConnectionViewModel : ViewModelBase
    {
        private static readonly ResourceManager ResourceManager = new(
            "PostGisTools.Resources.Strings",
            typeof(ConnectionViewModel).Assembly);

        public string Title => GetString("ConnectionTitle");

        private readonly IDbConnectionService _db;
        private readonly IAppConfigService _config;

        private string _host = "localhost";
        public string Host
        {
            get => _host;
            set => SetProperty(ref _host, value);
        }

        private string _portText = "5432";
        public string PortText
        {
            get => _portText;
            set => SetProperty(ref _portText, value);
        }

        private string _database = string.Empty;
        public string Database
        {
            get => _database;
            set => SetProperty(ref _database, value);
        }

        private string _username = string.Empty;
        public string Username
        {
            get => _username;
            set => SetProperty(ref _username, value);
        }

        // Keep in-memory only; not stored on disk.
        private string _password = string.Empty;
        public string Password
        {
            get => _password;
            set => SetProperty(ref _password, value);
        }

        private string _statusMessage = "";
        public string StatusMessage
        {
            get => _statusMessage;
            private set => SetProperty(ref _statusMessage, value);
        }

        private bool _isConnected;
        public bool IsConnected
        {
            get => _isConnected;
            private set => SetProperty(ref _isConnected, value);
        }

        public ICommand TestConnectionCommand { get; }

        public ConnectionViewModel() : this(new DbConnectionService(), new AppConfigService())
        {
        }

        public ConnectionViewModel(IDbConnectionService db, IAppConfigService config)
        {
            _db = db ?? throw new ArgumentNullException(nameof(db));
            _config = config ?? throw new ArgumentNullException(nameof(config));
            TestConnectionCommand = new AsyncRelayCommand(TestConnectionAsync, CanTestConnection);

            // Load saved settings (no password persisted)
            var saved = _config.LoadConnection();
            Host = string.IsNullOrWhiteSpace(saved.Host) ? Host : saved.Host;
            PortText = saved.Port > 0 ? saved.Port.ToString() : PortText;
            Database = saved.Database ?? string.Empty;
            Username = saved.Username ?? string.Empty;
        }

        private bool CanTestConnection()
            => !string.IsNullOrWhiteSpace(Host)
               && int.TryParse(PortText, out var port) && port > 0 && port <= 65535
               && !string.IsNullOrWhiteSpace(Database)
               && !string.IsNullOrWhiteSpace(Username);

        private async Task TestConnectionAsync()
        {
            StatusMessage = GetString("ConnectionStatusTesting");
            IsConnected = false;

            if (!int.TryParse(PortText, out var port) || port <= 0 || port > 65535)
            {
                StatusMessage = GetString("ConnectionStatusInvalidPort");
                return;
            }

            var cs = _db.BuildConnectionString(Host, port, Database, Username, Password);
            var (ok, message) = await _db.TestConnectionAsync(cs);

            if (ok)
            {
                _db.CurrentConnectionString = cs;
                _config.SaveConnection(new ConnectionSettings
                {
                    Host = Host,
                    Port = port,
                    Database = Database,
                    Username = Username
                });
            }

            IsConnected = ok;
            StatusMessage = ok
                ? FormatString("ConnectionStatusSuccess", message)
                : FormatString("ConnectionStatusFailed", message);
        }

        private static string GetString(string name)
            => ResourceManager.GetString(name, CultureInfo.CurrentUICulture) ?? name;

        private static string FormatString(string name, params object[] args)
            => string.Format(CultureInfo.CurrentUICulture, GetString(name), args);

        // Ensure CanExecute updates when inputs change.
        protected override void OnPropertyChanged(string? propertyName = null)
        {
            base.OnPropertyChanged(propertyName);
            System.Windows.Input.CommandManager.InvalidateRequerySuggested();
        }
    }
}
