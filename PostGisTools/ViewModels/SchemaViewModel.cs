using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Resources;
using System.Threading.Tasks;
using System.Windows.Input;
using System.Windows;
using Npgsql;
using PostGisTools.Core;
using PostGisTools.Models;
using PostGisTools.Services;

namespace PostGisTools.ViewModels
{
    public class SchemaViewModel : ViewModelBase
    {
        private readonly IDbConnectionService _dbService;
        private readonly IAppConfigService _configService;

        private readonly Dictionary<string, FieldConfig> _fieldConfigMap = new();

        private static readonly ResourceManager ResourceManager = new(
            "PostGisTools.Resources.Strings",
            typeof(SchemaViewModel).Assembly);

        public string Title => GetString("SchemaTitle");

        public ObservableCollection<SchemaItem> Schemas { get; } = new ObservableCollection<SchemaItem>();

        public ObservableCollection<CoordinateSystemOption> CoordinateSystems { get; } = new();

        private CoordinateSystemOption? _selectedCoordinateSystem;
        public CoordinateSystemOption? SelectedCoordinateSystem
        {
            get => _selectedCoordinateSystem;
            set
            {
                if (SetProperty(ref _selectedCoordinateSystem, value))
                {
                    System.Windows.Input.CommandManager.InvalidateRequerySuggested();
                }
            }
        }

        private SchemaItem? _selectedSchema;
        public SchemaItem? SelectedSchema
        {
            get => _selectedSchema;
            set
            {
                if (SetProperty(ref _selectedSchema, value))
                {
                    OnPropertyChanged(nameof(IsSchemaSelected));
                    OnPropertyChanged(nameof(IsSchemaNotSelected));
                    OnPropertyChanged(nameof(IsSchemaOnlySelected));
                }
            }
        }

        private TableItem? _selectedTable;
        public TableItem? SelectedTable
        {
            get => _selectedTable;
            set
            {
                if (SetProperty(ref _selectedTable, value))
                {
                    OnPropertyChanged(nameof(IsTableSelected));
                    OnPropertyChanged(nameof(IsTableNotSelected));
                    OnPropertyChanged(nameof(IsSchemaOnlySelected));
                }
            }
        }

        public bool IsSchemaSelected => SelectedSchema != null;
        public bool IsSchemaNotSelected => SelectedSchema == null;
        public bool IsSchemaOnlySelected => SelectedSchema != null && SelectedTable == null;
        public bool IsTableSelected => SelectedTable != null;
        public bool IsTableNotSelected => SelectedTable == null;

        public ObservableCollection<string> FieldTypes { get; } = new()
        {
            "text",
            "varchar",
            "int",
            "bigint",
            "numeric",
            "boolean",
            "date",
            "timestamp"
        };

        private bool _isLoading;
        public bool IsLoading
        {
            get => _isLoading;
            set
            {
                if (SetProperty(ref _isLoading, value))
                {
                    System.Windows.Input.CommandManager.InvalidateRequerySuggested();
                }
            }
        }

        private ColumnItem? _selectedColumn;
        public ColumnItem? SelectedColumn
        {
            get => _selectedColumn;
            set
            {
                if (SetProperty(ref _selectedColumn, value))
                {
                    System.Windows.Input.CommandManager.InvalidateRequerySuggested();
                }
            }
        }

        private string _statusMessage = GetString("SchemaStatusReady");
        public string StatusMessage
        {
            get => _statusMessage;
            set => SetProperty(ref _statusMessage, value);
        }

        private string? _newSchemaName;
        public string? NewSchemaName
        {
            get => _newSchemaName;
            set
            {
                if (SetProperty(ref _newSchemaName, value))
                {
                    System.Windows.Input.CommandManager.InvalidateRequerySuggested();
                }
            }
        }

        private string? _newFieldName;
        public string? NewFieldName
        {
            get => _newFieldName;
            set
            {
                if (SetProperty(ref _newFieldName, value))
                {
                    System.Windows.Input.CommandManager.InvalidateRequerySuggested();
                }
            }
        }

        private string? _newFieldType;
        public string? NewFieldType
        {
            get => _newFieldType;
            set
            {
                if (SetProperty(ref _newFieldType, value))
                {
                    System.Windows.Input.CommandManager.InvalidateRequerySuggested();
                }
            }
        }

        private string? _newFieldLength;
        public string? NewFieldLength
        {
            get => _newFieldLength;
            set
            {
                if (SetProperty(ref _newFieldLength, value))
                {
                    System.Windows.Input.CommandManager.InvalidateRequerySuggested();
                }
            }
        }

        public ICommand LoadSchemaCommand { get; }
        public ICommand RefreshCommand { get; }
        public ICommand SaveFieldConfigCommand { get; }
        public ICommand AddSchemaCommand { get; }
        public ICommand AddFieldCommand { get; }
        public ICommand BatchAddFieldCommand { get; }
        public ICommand ConvertCoordinateSystemCommand { get; }
        public ICommand DeleteFieldCommand { get; }

        public SchemaViewModel(IDbConnectionService dbService)
        {
            _dbService = dbService ?? throw new ArgumentNullException(nameof(dbService));
            _configService = new AppConfigService();
            CoordinateSystems.Add(new CoordinateSystemOption(GetString("SchemaCoordinateSystemWgs84"), 4326));
            CoordinateSystems.Add(new CoordinateSystemOption(GetString("SchemaCoordinateSystemWebMercator"), 3857));
            CoordinateSystems.Add(new CoordinateSystemOption(GetString("SchemaCoordinateSystemCgcs2000"), 4490));
            NewFieldType = FieldTypes.FirstOrDefault();
            LoadSchemaCommand = new AsyncRelayCommand(LoadSchemaAsync);
            RefreshCommand = new AsyncRelayCommand(LoadSchemaAsync);
            SaveFieldConfigCommand = new RelayCommand(_ => SaveFieldConfigs());
            AddSchemaCommand = new AsyncRelayCommand(AddSchemaAsync, CanAddSchema);
            AddFieldCommand = new AsyncRelayCommand(AddFieldAsync, CanAddField);
            BatchAddFieldCommand = new AsyncRelayCommand(BatchAddFieldAsync, CanBatchAddField);
            ConvertCoordinateSystemCommand = new AsyncRelayCommand(ConvertCoordinateSystemAsync, CanConvertCoordinateSystem);
            DeleteFieldCommand = new AsyncRelayCommand(DeleteFieldAsync, CanDeleteField);
        }

        private static string GetString(string name)
            => ResourceManager.GetString(name, CultureInfo.CurrentUICulture) ?? name;

        private static string FormatString(string name, params object[] args)
            => string.Format(CultureInfo.CurrentUICulture, GetString(name), args);

        public async Task LoadSchemaAsync()
        {
            if (string.IsNullOrWhiteSpace(_dbService.CurrentConnectionString))
            {
                StatusMessage = GetString("SchemaStatusNoConnection");
                return;
            }

            IsLoading = true;
            StatusMessage = GetString("SchemaStatusLoading");
            Schemas.Clear();
            SelectedSchema = null;
            SelectedTable = null;
            SelectedColumn = null;

            BuildFieldConfigMap();

            try
            {
                await using var conn = new NpgsqlConnection(_dbService.CurrentConnectionString);
                await conn.OpenAsync();

                var schemaSql = @"
                    SELECT schema_name
                    FROM information_schema.schemata
                    WHERE schema_name NOT IN ('information_schema', 'pg_catalog')
                    ORDER BY schema_name";

                await using (var schemaCmd = new NpgsqlCommand(schemaSql, conn))
                await using (var schemaReader = await schemaCmd.ExecuteReaderAsync())
                {
                    while (await schemaReader.ReadAsync())
                    {
                        var schemaName = schemaReader.GetString(0);
                        Schemas.Add(new SchemaItem { Name = schemaName });
                    }
                }

                var sql = @"
                    SELECT 
                        table_schema, 
                        table_name, 
                        column_name, 
                        data_type, 
                        column_default, 
                        character_maximum_length, 
                        is_nullable 
                    FROM information_schema.columns 
                    WHERE table_schema NOT IN ('information_schema', 'pg_catalog') 
                    ORDER BY table_schema, table_name, ordinal_position";

                using var cmd = new NpgsqlCommand(sql, conn);
                using var reader = await cmd.ExecuteReaderAsync();

                var schemaDict = Schemas.ToDictionary(item => item.Name ?? string.Empty, item => item);
                var tableDict = new Dictionary<string, TableItem>();

                while (await reader.ReadAsync())
                {
                    var schemaName = reader.GetString(0);
                    var tableName = reader.GetString(1);
                    var colName = reader.GetString(2);
                    var dataType = reader.GetString(3);
                    var colDefault = reader.IsDBNull(4) ? null : reader.GetString(4);
                    var charLen = reader.IsDBNull(5) ? (int?)null : reader.GetInt32(5);
                    var isNullable = reader.GetString(6) == "YES";

                    if (!schemaDict.TryGetValue(schemaName, out var schemaItem))
                    {
                        schemaItem = new SchemaItem { Name = schemaName };
                        schemaDict[schemaName] = schemaItem;
                        Schemas.Add(schemaItem);
                    }

                    var tableKey = $"{schemaName}.{tableName}";
                    if (!tableDict.TryGetValue(tableKey, out var tableItem))
                    {
                        tableItem = new TableItem { Schema = schemaName, Name = tableName };
                        tableDict[tableKey] = tableItem;
                        schemaItem.Tables.Add(tableItem);
                    }

                    var colItem = new ColumnItem
                    {
                        ColumnName = colName,
                        IsNullable = isNullable,
                        Alias = colName // Default alias to column name
                    };
                    colItem.InitFromDb(dataType, colDefault, charLen);

                    ApplyFieldConfig(schemaName, tableName, colItem);

                    tableItem.Columns.Add(colItem);
                }

                StatusMessage = GetString("SchemaStatusLoaded");
            }
            catch (NpgsqlException ex)
            {
                StatusMessage = FormatString("SchemaStatusLoadError", ex.Message);
            }
            catch (InvalidOperationException ex)
            {
                StatusMessage = FormatString("SchemaStatusLoadError", ex.Message);
            }
            finally
            {
                IsLoading = false;
            }
        }

        private void BuildFieldConfigMap()
        {
            _fieldConfigMap.Clear();
            var configs = _configService.LoadFieldConfigs();
            foreach (var cfg in configs)
            {
                var key = MakeFieldKey(cfg.Schema, cfg.Table, cfg.Column);
                _fieldConfigMap[key] = cfg;
            }
        }

        private void ApplyFieldConfig(string schema, string table, ColumnItem col)
        {
            var key = MakeFieldKey(schema, table, col.ColumnName);
            if (_fieldConfigMap.TryGetValue(key, out var cfg))
            {
                col.IsVisible = cfg.IsVisible;
                col.Alias = string.IsNullOrWhiteSpace(cfg.Alias) ? col.ColumnName : cfg.Alias;
                col.LocalType = string.IsNullOrWhiteSpace(cfg.LocalType) ? col.LocalType : cfg.LocalType;
                col.LocalLength = cfg.LocalLength ?? col.LocalLength;
                col.LocalDefault = cfg.LocalDefault ?? col.LocalDefault;
            }
        }

        public void SaveFieldConfigs()
        {
            var configs = new List<FieldConfig>();
            foreach (var schema in Schemas)
            {
                if (string.IsNullOrWhiteSpace(schema.Name))
                    continue;
                foreach (var table in schema.Tables)
                {
                    if (string.IsNullOrWhiteSpace(table.Name))
                        continue;
                    foreach (var col in table.Columns)
                    {
                        if (string.IsNullOrWhiteSpace(col.ColumnName))
                            continue;
                        configs.Add(new FieldConfig
                        {
                            Schema = schema.Name,
                            Table = table.Name,
                            Column = col.ColumnName,
                            IsVisible = col.IsVisible,
                            Alias = col.Alias ?? string.Empty,
                            LocalType = col.LocalType ?? string.Empty,
                            LocalLength = col.LocalLength,
                            LocalDefault = col.LocalDefault ?? string.Empty
                        });
                    }
                }
            }

            _configService.SaveFieldConfigs(configs);
            StatusMessage = FormatString("SchemaStatusSavedConfigs", configs.Count);
        }

        private bool CanAddField()
            => !IsLoading
               && SelectedTable != null
               && !string.IsNullOrWhiteSpace(NewFieldName)
               && !string.IsNullOrWhiteSpace(NewFieldType);

        private bool CanBatchAddField()
            => !IsLoading
               && SelectedSchema != null
               && SelectedTable == null
               && !string.IsNullOrWhiteSpace(NewFieldName)
               && !string.IsNullOrWhiteSpace(NewFieldType);

        private bool CanConvertCoordinateSystem()
            => !IsLoading
               && SelectedTable != null
               && SelectedCoordinateSystem != null;

        private bool TryGetFieldSpecification(out string fieldName, out string fieldType, out int? length)
        {
            fieldName = NewFieldName?.Trim() ?? string.Empty;
            fieldType = string.IsNullOrWhiteSpace(NewFieldType)
                ? "text"
                : NewFieldType.Trim();
            length = null;

            if (string.IsNullOrWhiteSpace(fieldName))
            {
                StatusMessage = GetString("SchemaStatusFieldNameRequired");
                return false;
            }

            if (!string.IsNullOrWhiteSpace(NewFieldLength))
            {
                if (!int.TryParse(NewFieldLength, out var parsedLength) || parsedLength <= 0)
                {
                    StatusMessage = GetString("SchemaStatusFieldInvalidLength");
                    return false;
                }
                length = parsedLength;
            }

            if (!FieldTypes.Contains(fieldType, StringComparer.OrdinalIgnoreCase))
            {
                StatusMessage = GetString("SchemaStatusFieldInvalidType");
                return false;
            }

            return true;
        }

        private async Task AddFieldAsync()
        {
            if (SelectedTable == null)
            {
                return;
            }

            if (!TryGetFieldSpecification(out var fieldName, out var fieldType, out var length))
            {
                return;
            }

            if (SelectedTable.Columns.Any(column => string.Equals(column.ColumnName, fieldName, StringComparison.OrdinalIgnoreCase)))
            {
                StatusMessage = FormatString("SchemaStatusFieldExists", fieldName);
                return;
            }

            if (string.IsNullOrWhiteSpace(_dbService.CurrentConnectionString))
            {
                StatusMessage = GetString("SchemaStatusNoConnection");
                return;
            }

            IsLoading = true;
            StatusMessage = FormatString("SchemaStatusFieldCreating", fieldName);

            try
            {
                await using var conn = new NpgsqlConnection(_dbService.CurrentConnectionString);
                await conn.OpenAsync();

                await using (var existsCommand = new NpgsqlCommand(
                    "SELECT 1 FROM information_schema.columns WHERE table_schema = @schema AND table_name = @table AND column_name = @column",
                    conn))
                {
                    existsCommand.Parameters.AddWithValue("schema", SelectedTable.Schema);
                    existsCommand.Parameters.AddWithValue("table", SelectedTable.Name);
                    existsCommand.Parameters.AddWithValue("column", fieldName);
                    var exists = await existsCommand.ExecuteScalarAsync();
                    if (exists != null)
                    {
                        StatusMessage = FormatString("SchemaStatusFieldExists", fieldName);
                        return;
                    }
                }

                var fullTableName = $"{QuoteIdentifier(SelectedTable.Schema)}.{QuoteIdentifier(SelectedTable.Name)}";
                var columnName = QuoteIdentifier(fieldName);
                var typeDefinition = length.HasValue ? $"{fieldType}({length.Value})" : fieldType;
                var sql = $"ALTER TABLE {fullTableName} ADD COLUMN {columnName} {typeDefinition}";
                await using (var createCommand = new NpgsqlCommand(sql, conn))
                {
                    await createCommand.ExecuteNonQueryAsync();
                }

                await ReloadSelectedTableColumnsAsync(conn, SelectedTable.Schema, SelectedTable.Name, fieldName);

                NewFieldName = string.Empty;
                NewFieldType = FieldTypes.FirstOrDefault() ?? string.Empty;
                NewFieldLength = string.Empty;
                StatusMessage = FormatString("SchemaStatusFieldAdded", fieldName);
            }
            catch (PostgresException ex)
            {
                StatusMessage = FormatString("SchemaStatusFieldCreateError", ex.Message);
            }
            catch (NpgsqlException ex)
            {
                StatusMessage = FormatString("SchemaStatusFieldCreateError", ex.Message);
            }
            catch (InvalidOperationException ex)
            {
                StatusMessage = FormatString("SchemaStatusFieldCreateError", ex.Message);
            }
            finally
            {
                IsLoading = false;
            }
        }

        private async Task ConvertCoordinateSystemAsync()
        {
            if (SelectedTable == null)
            {
                StatusMessage = GetString("SchemaStatusNoTableSelected");
                return;
            }

            if (SelectedCoordinateSystem == null)
            {
                StatusMessage = GetString("SchemaStatusNoTargetCoordinateSystem");
                return;
            }

            if (string.IsNullOrWhiteSpace(_dbService.CurrentConnectionString))
            {
                StatusMessage = GetString("SchemaStatusNoConnection");
                return;
            }

            var schemaName = SelectedTable.Schema;
            var tableName = SelectedTable.Name;
            var targetSrid = SelectedCoordinateSystem.Srid;

            IsLoading = true;
            StatusMessage = FormatString("SchemaStatusCoordinateConverting", schemaName, tableName, SelectedCoordinateSystem.Name);

            try
            {
                await using var conn = new NpgsqlConnection(_dbService.CurrentConnectionString);
                await conn.OpenAsync();

                var columns = new List<CoordinateColumn>();

                const string geometrySql = @"
                    SELECT f_geometry_column, type, srid
                    FROM public.geometry_columns
                    WHERE f_table_schema = @schema
                      AND f_table_name = @table";

                await using (var geometryCommand = new NpgsqlCommand(geometrySql, conn))
                {
                    geometryCommand.Parameters.AddWithValue("schema", schemaName);
                    geometryCommand.Parameters.AddWithValue("table", tableName);
                    await using var reader = await geometryCommand.ExecuteReaderAsync();
                    while (await reader.ReadAsync())
                    {
                        columns.Add(new CoordinateColumn(
                            reader.GetString(0),
                            reader.GetString(1),
                            reader.GetInt32(2),
                            false));
                    }
                }

                const string geographySql = @"
                    SELECT f_geography_column, type, srid
                    FROM public.geography_columns
                    WHERE f_table_schema = @schema
                      AND f_table_name = @table";

                await using (var geographyCommand = new NpgsqlCommand(geographySql, conn))
                {
                    geographyCommand.Parameters.AddWithValue("schema", schemaName);
                    geographyCommand.Parameters.AddWithValue("table", tableName);
                    await using var reader = await geographyCommand.ExecuteReaderAsync();
                    while (await reader.ReadAsync())
                    {
                        columns.Add(new CoordinateColumn(
                            reader.GetString(0),
                            reader.GetString(1),
                            reader.GetInt32(2),
                            true));
                    }
                }

                if (columns.Count == 0)
                {
                    StatusMessage = GetString("SchemaStatusNoSpatialColumns");
                    return;
                }

                var converted = 0;
                var skipped = 0;
                var fullTableName = $"{QuoteIdentifier(schemaName)}.{QuoteIdentifier(tableName)}";

                foreach (var column in columns)
                {
                    if (column.Srid == targetSrid)
                    {
                        skipped++;
                        continue;
                    }

                    var columnName = QuoteIdentifier(column.Name);
                    var typeDefinition = column.IsGeography
                        ? $"geography({column.Type}, {targetSrid})"
                        : $"geometry({column.Type}, {targetSrid})";
                    var usingExpression = column.IsGeography
                        ? $"ST_Transform({columnName}::geometry, {targetSrid})::geography"
                        : $"ST_Transform({columnName}, {targetSrid})";
                    var sql = $"ALTER TABLE {fullTableName} ALTER COLUMN {columnName} TYPE {typeDefinition} USING {usingExpression}";

                    await using (var convertCommand = new NpgsqlCommand(sql, conn))
                    {
                        await convertCommand.ExecuteNonQueryAsync();
                    }

                    converted++;
                }

                await ReloadSelectedTableColumnsAsync(conn, schemaName, tableName, null);
                StatusMessage = FormatString("SchemaStatusCoordinateConverted", converted, skipped);
            }
            catch (PostgresException ex)
            {
                StatusMessage = FormatString("SchemaStatusCoordinateConvertError", ex.Message);
            }
            catch (NpgsqlException ex)
            {
                StatusMessage = FormatString("SchemaStatusCoordinateConvertError", ex.Message);
            }
            catch (InvalidOperationException ex)
            {
                StatusMessage = FormatString("SchemaStatusCoordinateConvertError", ex.Message);
            }
            finally
            {
                IsLoading = false;
            }
        }

        private async Task BatchAddFieldAsync()
        {
            if (SelectedSchema == null)
            {
                StatusMessage = GetString("SchemaStatusNoSchemaSelected");
                return;
            }

            if (!TryGetFieldSpecification(out var fieldName, out var fieldType, out var length))
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(_dbService.CurrentConnectionString))
            {
                StatusMessage = GetString("SchemaStatusNoConnection");
                return;
            }

            var schemaName = SelectedSchema.Name?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(schemaName))
            {
                StatusMessage = GetString("SchemaStatusNoSchemaSelected");
                return;
            }
            IsLoading = true;
            StatusMessage = FormatString("SchemaStatusBatchFieldCreating", schemaName, fieldName);

            try
            {
                await using var conn = new NpgsqlConnection(_dbService.CurrentConnectionString);
                await conn.OpenAsync();

                var tables = new List<string>();
                const string tableSql = @"
                    SELECT table_name
                    FROM information_schema.tables
                    WHERE table_schema = @schema
                      AND table_type = 'BASE TABLE'
                    ORDER BY table_name";

                await using (var tableCommand = new NpgsqlCommand(tableSql, conn))
                {
                    tableCommand.Parameters.AddWithValue("schema", schemaName);
                    await using var tableReader = await tableCommand.ExecuteReaderAsync();
                    while (await tableReader.ReadAsync())
                    {
                        tables.Add(tableReader.GetString(0));
                    }
                }

                if (tables.Count == 0)
                {
                    StatusMessage = FormatString("SchemaStatusSchemaNoTables", schemaName);
                    return;
                }

                var existingTables = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                const string existingSql = @"
                    SELECT table_name
                    FROM information_schema.columns
                    WHERE table_schema = @schema
                      AND column_name = @column";

                await using (var existingCommand = new NpgsqlCommand(existingSql, conn))
                {
                    existingCommand.Parameters.AddWithValue("schema", schemaName);
                    existingCommand.Parameters.AddWithValue("column", fieldName);
                    await using var existingReader = await existingCommand.ExecuteReaderAsync();
                    while (await existingReader.ReadAsync())
                    {
                        existingTables.Add(existingReader.GetString(0));
                    }
                }

                var added = 0;
                var skipped = 0;
                var columnName = QuoteIdentifier(fieldName);
                var typeDefinition = length.HasValue ? $"{fieldType}({length.Value})" : fieldType;

                foreach (var tableName in tables)
                {
                    if (existingTables.Contains(tableName))
                    {
                        skipped++;
                        continue;
                    }

                    var fullTableName = $"{QuoteIdentifier(schemaName)}.{QuoteIdentifier(tableName)}";
                    var sql = $"ALTER TABLE {fullTableName} ADD COLUMN {columnName} {typeDefinition}";
                    await using (var createCommand = new NpgsqlCommand(sql, conn))
                    {
                        await createCommand.ExecuteNonQueryAsync();
                    }

                    added++;
                }

                if (SelectedTable != null)
                {
                    await ReloadSelectedTableColumnsAsync(conn, SelectedTable.Schema, SelectedTable.Name, fieldName);
                }

                NewFieldName = string.Empty;
                NewFieldType = FieldTypes.FirstOrDefault() ?? string.Empty;
                NewFieldLength = string.Empty;
                StatusMessage = FormatString("SchemaStatusBatchFieldCompleted", added, skipped);
                await LoadSchemaAsync();
            }
            catch (PostgresException ex)
            {
                StatusMessage = FormatString("SchemaStatusFieldCreateError", ex.Message);
            }
            catch (NpgsqlException ex)
            {
                StatusMessage = FormatString("SchemaStatusFieldCreateError", ex.Message);
            }
            catch (InvalidOperationException ex)
            {
                StatusMessage = FormatString("SchemaStatusFieldCreateError", ex.Message);
            }
            finally
            {
                IsLoading = false;
            }
        }

        private async Task ReloadSelectedTableColumnsAsync(NpgsqlConnection conn, string schemaName, string tableName, string? selectColumnName)
        {
            BuildFieldConfigMap();
            var columns = new List<ColumnItem>();

            const string sql = @"
                SELECT 
                    column_name, 
                    data_type, 
                    column_default, 
                    character_maximum_length, 
                    is_nullable
                FROM information_schema.columns 
                WHERE table_schema = @schema
                  AND table_name = @table
                ORDER BY ordinal_position";

            await using (var cmd = new NpgsqlCommand(sql, conn))
            {
                cmd.Parameters.AddWithValue("schema", schemaName);
                cmd.Parameters.AddWithValue("table", tableName);

                await using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    var colName = reader.GetString(0);
                    var dataType = reader.GetString(1);
                    var colDefault = reader.IsDBNull(2) ? null : reader.GetString(2);
                    var charLen = reader.IsDBNull(3) ? (int?)null : reader.GetInt32(3);
                    var isNullable = reader.GetString(4) == "YES";

                    var colItem = new ColumnItem
                    {
                        ColumnName = colName,
                        IsNullable = isNullable,
                        Alias = colName
                    };
                    colItem.InitFromDb(dataType, colDefault, charLen);
                    ApplyFieldConfig(schemaName, tableName, colItem);
                    columns.Add(colItem);
                }
            }

            if (SelectedTable == null)
            {
                return;
            }

            SelectedTable.Columns.Clear();
            foreach (var column in columns)
            {
                SelectedTable.Columns.Add(column);
            }

            if (!string.IsNullOrWhiteSpace(selectColumnName))
            {
                SelectedColumn = SelectedTable.Columns
                    .FirstOrDefault(column => string.Equals(column.ColumnName, selectColumnName, StringComparison.OrdinalIgnoreCase));
            }
        }

        private bool CanDeleteField()
            => !IsLoading && SelectedTable != null && SelectedColumn != null;

        private async Task DeleteFieldAsync()
        {
            if (SelectedTable == null)
            {
                return;
            }

            if (SelectedColumn == null)
            {
                StatusMessage = GetString("SchemaStatusNoFieldSelected");
                return;
            }

            var fieldName = SelectedColumn.ColumnName;
            var confirmMessage = FormatString("SchemaViewDeleteFieldConfirmMessage", fieldName);
            var title = GetString("SchemaViewDeleteFieldConfirmTitle");
            var result = MessageBox.Show(confirmMessage, title, MessageBoxButton.YesNo, MessageBoxImage.Warning);
            if (result != MessageBoxResult.Yes)
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(_dbService.CurrentConnectionString))
            {
                StatusMessage = GetString("SchemaStatusNoConnection");
                return;
            }

            IsLoading = true;
            StatusMessage = FormatString("SchemaStatusFieldDeleting", fieldName);

            try
            {
                await using var conn = new NpgsqlConnection(_dbService.CurrentConnectionString);
                await conn.OpenAsync();

                var fullTableName = $"{QuoteIdentifier(SelectedTable.Schema)}.{QuoteIdentifier(SelectedTable.Name)}";
                var columnName = QuoteIdentifier(fieldName);
                var sql = $"ALTER TABLE {fullTableName} DROP COLUMN {columnName}";
                await using (var deleteCommand = new NpgsqlCommand(sql, conn))
                {
                    await deleteCommand.ExecuteNonQueryAsync();
                }

                await ReloadSelectedTableColumnsAsync(conn, SelectedTable.Schema, SelectedTable.Name, null);

                SelectedColumn = null;
                StatusMessage = FormatString("SchemaStatusFieldDeleted", fieldName);
            }
            catch (PostgresException ex)
            {
                StatusMessage = FormatString("SchemaStatusFieldDeleteError", ex.Message);
            }
            catch (NpgsqlException ex)
            {
                StatusMessage = FormatString("SchemaStatusFieldDeleteError", ex.Message);
            }
            catch (InvalidOperationException ex)
            {
                StatusMessage = FormatString("SchemaStatusFieldDeleteError", ex.Message);
            }
            finally
            {
                IsLoading = false;
            }
        }

        private bool CanAddSchema()
            => !IsLoading && !string.IsNullOrWhiteSpace(NewSchemaName);

        private async Task AddSchemaAsync()
        {
            if (string.IsNullOrWhiteSpace(_dbService.CurrentConnectionString))
            {
                StatusMessage = GetString("SchemaStatusNoConnection");
                return;
            }

            var schemaName = NewSchemaName?.Trim();
            if (string.IsNullOrWhiteSpace(schemaName))
            {
                StatusMessage = GetString("SchemaStatusSchemaNameRequired");
                return;
            }

            IsLoading = true;
            StatusMessage = FormatString("SchemaStatusCreating", schemaName);

            try
            {
                await using var conn = new NpgsqlConnection(_dbService.CurrentConnectionString);
                await conn.OpenAsync();

                await using (var existsCommand = new NpgsqlCommand(
                    "SELECT 1 FROM information_schema.schemata WHERE schema_name = @schema",
                    conn))
                {
                    existsCommand.Parameters.AddWithValue("schema", schemaName);
                    var exists = await existsCommand.ExecuteScalarAsync();
                    if (exists != null)
                    {
                        StatusMessage = FormatString("SchemaStatusSchemaExists", schemaName);
                        return;
                    }
                }

                var quotedSchemaName = QuoteIdentifier(schemaName);
                await using (var createCommand = new NpgsqlCommand($"CREATE SCHEMA {quotedSchemaName}", conn))
                {
                    await createCommand.ExecuteNonQueryAsync();
                }

                if (!Schemas.Any(schema => string.Equals(schema.Name, schemaName, StringComparison.OrdinalIgnoreCase)))
                {
                    Schemas.Add(new SchemaItem { Name = schemaName });
                }

                NewSchemaName = string.Empty;
                StatusMessage = FormatString("SchemaStatusSchemaCreated", schemaName);
            }
            catch (PostgresException ex)
            {
                StatusMessage = FormatString("SchemaStatusCreateError", ex.Message);
            }
            catch (NpgsqlException ex)
            {
                StatusMessage = FormatString("SchemaStatusCreateError", ex.Message);
            }
            catch (InvalidOperationException ex)
            {
                StatusMessage = FormatString("SchemaStatusCreateError", ex.Message);
            }
            finally
            {
                IsLoading = false;
            }
        }

        private static string QuoteIdentifier(string identifier)
            => $"\"{identifier.Replace("\"", "\"\"")}\"";

        private static string MakeFieldKey(string schema, string table, string column)
            => $"{schema}.{table}.{column}";

        public sealed record CoordinateSystemOption(string Name, int Srid);

        private sealed record CoordinateColumn(string Name, string Type, int Srid, bool IsGeography);
    }
}
