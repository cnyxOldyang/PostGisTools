using System.Threading.Tasks;
using System.Windows.Input;
using PostGisTools.Core;
using PostGisTools.Services;

namespace PostGisTools.ViewModels
{
    public class MainViewModel : ViewModelBase
    {
        private ViewModelBase _currentView;

        public ConnectionViewModel ConnectionVM { get; }
        public SchemaViewModel SchemaVM { get; }
        public DataViewModel DataVM { get; }

        public ViewModelBase CurrentView
        {
            get => _currentView;
            set => SetProperty(ref _currentView, value);
        }

        public ICommand NavigateToConnectionCommand { get; }
        public ICommand NavigateToSchemaCommand { get; }
        public ICommand NavigateToDataCommand { get; }

        public MainViewModel()
        {
            IDbConnectionService dbService = new DbConnectionService();

            var configService = new AppConfigService();
            ConnectionVM = new ConnectionViewModel(dbService, configService);
            SchemaVM = new SchemaViewModel(dbService);
            DataVM = new DataViewModel(dbService);

            // Default view
            _currentView = ConnectionVM;

            NavigateToConnectionCommand = new RelayCommand(_ => CurrentView = ConnectionVM);
            NavigateToSchemaCommand = new AsyncRelayCommand(NavigateToSchemaAsync);
            NavigateToDataCommand = new AsyncRelayCommand(NavigateToDataAsync);
        }

        private async Task NavigateToSchemaAsync()
        {
            CurrentView = SchemaVM;
            await SchemaVM.LoadSchemaAsync();
        }

        private async Task NavigateToDataAsync()
        {
            CurrentView = DataVM;
            await DataVM.LoadSchemasAsync();
        }
    }
}
