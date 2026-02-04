using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using PostGisTools.Models;
using PostGisTools.ViewModels;

namespace PostGisTools.Views
{
    public partial class SchemaView : UserControl
    {
        public SchemaView()
        {
            InitializeComponent();
        }

        private void TreeView_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            if (DataContext is SchemaViewModel vm)
            {
                if (e.NewValue is TableItem table)
                {
                    vm.SelectedTable = table;
                    vm.SelectedSchema = vm.Schemas.FirstOrDefault(schema =>
                        string.Equals(schema.Name, table.Schema, StringComparison.OrdinalIgnoreCase));
                }
                else if (e.NewValue is SchemaItem schema)
                {
                    vm.SelectedSchema = schema;
                    vm.SelectedTable = null;
                }
                else
                {
                    vm.SelectedSchema = null;
                    vm.SelectedTable = null;
                }
            }
        }
    }
}
