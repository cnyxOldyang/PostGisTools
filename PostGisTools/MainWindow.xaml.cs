using System.Windows;
using PostGisTools.ViewModels;

namespace PostGisTools
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            DataContext = new MainViewModel();
        }
    }
}
