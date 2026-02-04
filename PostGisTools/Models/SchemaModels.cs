using System.Collections.ObjectModel;
using PostGisTools.Core;

namespace PostGisTools.Models
{
    public class SchemaItem : ViewModelBase
    {
        public string Name { get; set; } = string.Empty;
        public ObservableCollection<TableItem> Tables { get; set; } = new ObservableCollection<TableItem>();
    }

    public class TableItem : ViewModelBase
    {
        public string Schema { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public ObservableCollection<ColumnItem> Columns { get; set; } = new ObservableCollection<ColumnItem>();
    }

    public class ColumnItem : ViewModelBase
    {
        public string ColumnName { get; set; } = string.Empty;
        
        // Read-only from DB (source of truth)
        public string DbType { get; set; } = string.Empty;
        public bool IsNullable { get; set; }
        public int? CharacterMaximumLength { get; set; }

        // Local Metadata (Editable)
        private bool _isVisible = true;
        public bool IsVisible
        {
            get => _isVisible;
            set => SetProperty(ref _isVisible, value);
        }

        private string _alias = string.Empty;
        public string Alias
        {
            get => _alias;
            set => SetProperty(ref _alias, value);
        }

        // Allow overriding type/length/default locally if needed, or just display DB values
        // Prompt says "configurable... type/length/default as local metadata only"
        // So I will create backing fields for local overrides, initialized with DB values.

        private string _localType = string.Empty;
        public string LocalType
        {
            get => _localType;
            set => SetProperty(ref _localType, value);
        }

        private string _localDefault = string.Empty;
        public string LocalDefault
        {
            get => _localDefault;
            set => SetProperty(ref _localDefault, value);
        }

        private int? _localLength;
        public int? LocalLength
        {
            get => _localLength;
            set => SetProperty(ref _localLength, value);
        }

        public ColumnItem() { }

        public void InitFromDb(string type, string? def, int? length)
        {
            DbType = type;
            LocalType = type;
            LocalDefault = def ?? string.Empty;
            CharacterMaximumLength = length;
            LocalLength = length;
        }
    }
}
