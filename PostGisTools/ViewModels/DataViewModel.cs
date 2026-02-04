using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Data;
using System.Globalization;
using System.Linq;
using System.Resources;
using System.Threading.Tasks;
using System.Windows.Input;
using Npgsql;
using PostGisTools.Core;
using PostGisTools.Services;

namespace PostGisTools.ViewModels
{
    public class DataViewModel : ViewModelBase
    {
        private readonly IDbConnectionService _dbService;
        private NpgsqlDataAdapter? _dataAdapter;
        private NpgsqlCommandBuilder? _commandBuilder;
        private DataTable? _dataTable;
        private readonly List<string> _primaryKeyColumns = new();

        private static readonly ResourceManager ResourceManager = new(
            "PostGisTools.Resources.Strings",
            typeof(DataViewModel).Assembly);

        private string? _selectedSchema;
        private string? _selectedTable;
        private DataView? _tableData;
        private string _statusMessage = GetString("StatusReady");
        private bool _isLoading;
        private bool _hasPrimaryKey;
        private DataRowView? _selectedRow;
        private bool _suppressAutoLoad;

        private const int DefaultRowLimit = 200;

        public string Title => "Data Explorer";

        public ObservableCollection<string> Schemas { get; } = new();
        public ObservableCollection<string> Tables { get; } = new();

        public string? SelectedSchema
        {
            get => _selectedSchema;
            set
            {
                if (SetProperty(ref _selectedSchema, value))
                {
                    Tables.Clear();
                    TableData = null;
                    SelectedTable = null;
                    if (!string.IsNullOrEmpty(value))
                    {
                        // Fire and forget, but handle carefully
                        _ = LoadTablesAsync();
                    }
                }
            }
        }

        public string? SelectedTable
        {
            get => _selectedTable;
            set
            {
                if (SetProperty(ref _selectedTable, value))
                {
                    TableData = null;
                    if (!_suppressAutoLoad && !string.IsNullOrEmpty(value))
                    {
                        _ = LoadDataAsync();
                    }
                }
            }
        }

        public DataView? TableData
        {
            get => _tableData;
            set => SetProperty(ref _tableData, value);
        }

        public DataRowView? SelectedRow
        {
            get => _selectedRow;
            set => SetProperty(ref _selectedRow, value);
        }

        public string StatusMessage
        {
            get => _statusMessage;
            set => SetProperty(ref _statusMessage, value);
        }

        public bool IsLoading
        {
            get => _isLoading;
            set => SetProperty(ref _isLoading, value);
        }

        public bool HasPrimaryKey
        {
            get => _hasPrimaryKey;
            private set
            {
                if (SetProperty(ref _hasPrimaryKey, value))
                {
                    OnPropertyChanged(nameof(CanEdit));
                    System.Windows.Input.CommandManager.InvalidateRequerySuggested();
                }
            }
        }

        public bool CanEdit => HasPrimaryKey && _dataTable != null;

        public ICommand LoadSchemasCommand { get; }
        public ICommand LoadDataCommand { get; }
        public ICommand SaveChangesCommand { get; }
        public ICommand AddRowCommand { get; }
        public ICommand DeleteRowCommand { get; }

        public DataViewModel(IDbConnectionService dbService)
        {
            _dbService = dbService ?? throw new ArgumentNullException(nameof(dbService));

            LoadSchemasCommand = new AsyncRelayCommand(LoadSchemasAsync);
            LoadDataCommand = new AsyncRelayCommand(LoadDataAsync, CanLoadData);
            SaveChangesCommand = new AsyncRelayCommand(SaveChangesAsync, () => CanEdit && _dataAdapter != null);
            AddRowCommand = new RelayCommand(_ => AddRow(), _ => CanEdit);
            DeleteRowCommand = new RelayCommand(_ => DeleteSelectedRow(), _ => CanEdit && SelectedRow != null);

            // Try to load schemas if we already have a connection string
            if (!string.IsNullOrEmpty(_dbService.CurrentConnectionString))
            {
                _ = LoadSchemasAsync();
            }
        }

        private static string GetString(string name)
            => ResourceManager.GetString(name, CultureInfo.CurrentUICulture) ?? name;

        private static string FormatString(string name, params object[] args)
            => string.Format(CultureInfo.CurrentUICulture, GetString(name), args);

        public async Task LoadSchemasAsync()
        {
            if (string.IsNullOrEmpty(_dbService.CurrentConnectionString))
            {
                StatusMessage = GetString("StatusNoConnectionString");
                return;
            }

            IsLoading = true;
            StatusMessage = GetString("StatusLoadingSchemas");
            Schemas.Clear();

            try
            {
                await using var conn = new NpgsqlConnection(_dbService.CurrentConnectionString);
                await conn.OpenAsync();

                using var cmd = new NpgsqlCommand(@"SELECT schema_name 
                    FROM information_schema.schemata 
                    WHERE schema_name NOT IN ('information_schema','pg_catalog') 
                    ORDER BY schema_name", conn);
                await using var reader = await cmd.ExecuteReaderAsync();
                
                while (await reader.ReadAsync())
                {
                    Schemas.Add(reader.GetString(0));
                }

                StatusMessage = FormatString("StatusLoadedSchemas", Schemas.Count);
                
                // Select 'public' by default if it exists
                if (Schemas.Contains("public"))
                {
                    SelectedSchema = "public";
                }
                else if (Schemas.Count > 0)
                {
                    SelectedSchema = Schemas[0];
                }
            }
            catch (Exception ex)
            {
                StatusMessage = FormatString("StatusErrorLoadingSchemas", ex.Message);
            }
            finally
            {
                IsLoading = false;
            }
        }

        private async Task LoadTablesAsync()
        {
            if (string.IsNullOrEmpty(SelectedSchema) || string.IsNullOrEmpty(_dbService.CurrentConnectionString))
                return;

            IsLoading = true;
            StatusMessage = FormatString("StatusLoadingTables", SelectedSchema);
            Tables.Clear();

            try
            {
                await using var conn = new NpgsqlConnection(_dbService.CurrentConnectionString);
                await conn.OpenAsync();

                // Query tables
                string sql = "SELECT table_name FROM information_schema.tables WHERE table_schema = @schema AND table_type = 'BASE TABLE' ORDER BY table_name";
                using var cmd = new NpgsqlCommand(sql, conn);
                cmd.Parameters.AddWithValue("schema", SelectedSchema);

                await using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    Tables.Add(reader.GetString(0));
                }

                StatusMessage = FormatString("StatusLoadedTables", SelectedSchema, Tables.Count);

                if (Tables.Count > 0)
                {
                    _suppressAutoLoad = true;
                    SelectedTable = Tables[0];
                    _suppressAutoLoad = false;
                    await LoadDataAsync();
                }
            }
            catch (Exception ex)
            {
                StatusMessage = FormatString("StatusErrorLoadingTables", ex.Message);
            }
            finally
            {
                _suppressAutoLoad = false;
                IsLoading = false;
            }
        }

        private bool CanLoadData()
            => !string.IsNullOrEmpty(SelectedSchema) && !string.IsNullOrEmpty(SelectedTable) && !IsLoading;

        public async Task LoadDataAsync()
        {
            if (string.IsNullOrEmpty(SelectedSchema) || string.IsNullOrEmpty(SelectedTable))
            {
                StatusMessage = GetString("StatusSelectSchemaAndTable");
                return;
            }

            IsLoading = true;
            StatusMessage = FormatString("StatusLoadingData", SelectedSchema, SelectedTable);
            
            try
            {
                var connStr = _dbService.CurrentConnectionString;
                var schemaName = SelectedSchema;
                var tableName = SelectedTable;

                _primaryKeyColumns.Clear();
                HasPrimaryKey = false;

                // Fetch columns (exclude geometry/geography) and primary keys
                var columns = await GetSelectableColumnsAsync(connStr, schemaName, tableName);
                var pkCols = await GetPrimaryKeyColumnsAsync(connStr, schemaName, tableName);
                _primaryKeyColumns.AddRange(pkCols);
                HasPrimaryKey = _primaryKeyColumns.Count > 0;

                if (columns.Count == 0)
                {
                    StatusMessage = GetString("StatusNoSelectableColumns");
                    TableData = null;
                    return;
                }

                var columnList = string.Join(", ", columns.Select(c => $"\"{c}\""));

                await Task.Run(() =>
                {
                    string fullTableName = $"\"{schemaName}\".\"{tableName}\"";
                    var sql = $"SELECT {columnList} FROM {fullTableName} LIMIT {DefaultRowLimit}";

                    _dataAdapter = new NpgsqlDataAdapter(sql, connStr);
                    _commandBuilder = new NpgsqlCommandBuilder(_dataAdapter);

                    _dataTable = new DataTable();
                    _dataAdapter.Fill(_dataTable);
                });

                if (_dataTable != null)
                {
                    TableData = _dataTable.DefaultView;
                    StatusMessage = HasPrimaryKey
                        ? FormatString("StatusLoadedRowsWithLimit", _dataTable.Rows.Count, DefaultRowLimit)
                        : GetString("StatusLoadedNoPrimaryKey");
                }
            }
            catch (Exception ex)
            {
                StatusMessage = FormatString("StatusErrorLoadingData", ex.Message);
                TableData = null;
            }
            finally
            {
                IsLoading = false;
            }
        }

        public async Task SaveChangesAsync()
        {
            if (_dataAdapter == null || _dataTable == null)
            {
                StatusMessage = GetString("StatusNoDataToSave");
                return;
            }

            if (!HasPrimaryKey)
            {
                StatusMessage = GetString("StatusCannotSaveNoPrimaryKey");
                return;
            }

            IsLoading = true;
            StatusMessage = GetString("StatusSavingChanges");

            try
            {
                await Task.Run(() =>
                {
                    // Update() is synchronous
                    _dataAdapter.Update(_dataTable);
                });
                
                StatusMessage = GetString("StatusChangesSaved");
                
                // Optional: Reload to refresh IDs/defaults?
                // await LoadDataAsync(); 
            }
            catch (Exception ex)
            {
                StatusMessage = FormatString("StatusErrorSavingChanges", ex.Message);
            }
            finally
            {
                IsLoading = false;
            }
        }

        private void AddRow()
        {
            if (_dataTable == null)
            {
                StatusMessage = GetString("StatusLoadDataFirst");
                return;
            }

            if (!HasPrimaryKey)
            {
                StatusMessage = GetString("StatusCannotAddRowNoPrimaryKey");
                return;
            }

            _dataTable.Rows.Add(_dataTable.NewRow());
            StatusMessage = GetString("StatusNewRowAdded");
        }

        private void DeleteSelectedRow()
        {
            if (SelectedRow == null)
            {
                StatusMessage = GetString("StatusNoRowSelected");
                return;
            }

            if (!HasPrimaryKey)
            {
                StatusMessage = GetString("StatusCannotDeleteNoPrimaryKey");
                return;
            }

            SelectedRow.Row.Delete();
            StatusMessage = GetString("StatusRowMarkedForDeletion");
        }

        private async Task<List<string>> GetSelectableColumnsAsync(string connStr, string schema, string table)
        {
            var columns = new List<string>();
            await using var conn = new NpgsqlConnection(connStr);
            await conn.OpenAsync();

            const string sql = @"
                SELECT column_name, data_type, udt_name
                FROM information_schema.columns
                WHERE table_schema = @schema AND table_name = @table
                ORDER BY ordinal_position";

            await using var cmd = new NpgsqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("schema", schema);
            cmd.Parameters.AddWithValue("table", table);

            await using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                var columnName = reader.GetString(0);
                var dataType = reader.GetString(1);
                var udtName = reader.GetString(2);

                var isGeometry = string.Equals(udtName, "geometry", StringComparison.OrdinalIgnoreCase)
                                 || string.Equals(udtName, "geography", StringComparison.OrdinalIgnoreCase)
                                 || (string.Equals(dataType, "USER-DEFINED", StringComparison.OrdinalIgnoreCase)
                                     && (string.Equals(udtName, "geometry", StringComparison.OrdinalIgnoreCase)
                                         || string.Equals(udtName, "geography", StringComparison.OrdinalIgnoreCase)));

                if (!isGeometry)
                {
                    columns.Add(columnName);
                }
            }

            return columns;
        }

        private async Task<List<string>> GetPrimaryKeyColumnsAsync(string connStr, string schema, string table)
        {
            var pkCols = new List<string>();
            await using var conn = new NpgsqlConnection(connStr);
            await conn.OpenAsync();

            const string sql = @"
                SELECT kcu.column_name
                FROM information_schema.table_constraints tc
                JOIN information_schema.key_column_usage kcu
                  ON tc.constraint_name = kcu.constraint_name
                 AND tc.table_schema = kcu.table_schema
                WHERE tc.table_schema = @schema
                  AND tc.table_name = @table
                  AND tc.constraint_type = 'PRIMARY KEY'
                ORDER BY kcu.ordinal_position";

            await using var cmd = new NpgsqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("schema", schema);
            cmd.Parameters.AddWithValue("table", table);

            await using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                pkCols.Add(reader.GetString(0));
            }

            return pkCols;
        }
    }
}
