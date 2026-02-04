using System.Globalization;
using System.Resources;

namespace PostGisTools.Resources
{
    public static class Strings
    {
        private static readonly ResourceManager ResourceManager = new(
            "PostGisTools.Resources.Strings",
            typeof(Strings).Assembly);

        private static string GetString(string name)
            => ResourceManager.GetString(name, CultureInfo.CurrentUICulture) ?? name;

        public static string ConnectionIntroText => GetString("ConnectionIntroText");
        public static string ConnectionLabelHost => GetString("ConnectionLabelHost");
        public static string ConnectionLabelPort => GetString("ConnectionLabelPort");
        public static string ConnectionLabelDatabase => GetString("ConnectionLabelDatabase");
        public static string ConnectionLabelUsername => GetString("ConnectionLabelUsername");
        public static string ConnectionLabelPassword => GetString("ConnectionLabelPassword");
        public static string ConnectionButtonTest => GetString("ConnectionButtonTest");
        public static string ConnectionPasswordNote => GetString("ConnectionPasswordNote");

        public static string NavigationConnection => GetString("NavigationConnection");
        public static string NavigationSchema => GetString("NavigationSchema");
        public static string NavigationData => GetString("NavigationData");

        public static string DataViewLabelSchema => GetString("DataViewLabelSchema");
        public static string DataViewLabelTable => GetString("DataViewLabelTable");
        public static string DataViewButtonLoad => GetString("DataViewButtonLoad");
        public static string DataViewButtonAdd => GetString("DataViewButtonAdd");
        public static string DataViewButtonDelete => GetString("DataViewButtonDelete");
        public static string DataViewButtonSave => GetString("DataViewButtonSave");
        public static string DataViewRefreshSchemas => GetString("DataViewRefreshSchemas");
        public static string DataViewProcessing => GetString("DataViewProcessing");

        public static string SchemaViewHeader => GetString("SchemaViewHeader");
        public static string SchemaViewCoordinateTargetLabel => GetString("SchemaViewCoordinateTargetLabel");
        public static string SchemaViewCoordinateConvertButton => GetString("SchemaViewCoordinateConvertButton");
        public static string SchemaViewBatchAddFieldLabel => GetString("SchemaViewBatchAddFieldLabel");
        public static string SchemaViewBatchAddFieldButton => GetString("SchemaViewBatchAddFieldButton");
        public static string SchemaViewSaveConfig => GetString("SchemaViewSaveConfig");
        public static string SchemaViewRefresh => GetString("SchemaViewRefresh");
        public static string SchemaViewSelectedSchemaFormat => GetString("SchemaViewSelectedSchemaFormat");
        public static string SchemaViewHeaderVisible => GetString("SchemaViewHeaderVisible");
        public static string SchemaViewHeaderColumn => GetString("SchemaViewHeaderColumn");
        public static string SchemaViewHeaderAlias => GetString("SchemaViewHeaderAlias");
        public static string SchemaViewHeaderType => GetString("SchemaViewHeaderType");
        public static string SchemaViewHeaderLength => GetString("SchemaViewHeaderLength");
        public static string SchemaViewHeaderDefault => GetString("SchemaViewHeaderDefault");
        public static string SchemaViewHeaderDbType => GetString("SchemaViewHeaderDbType");
        public static string SchemaViewSelectTableHint => GetString("SchemaViewSelectTableHint");
        public static string SchemaViewLoadingTitle => GetString("SchemaViewLoadingTitle");
        public static string SchemaViewLoadingMessage => GetString("SchemaViewLoadingMessage");
        public static string SchemaViewAddFieldLabel => GetString("SchemaViewAddFieldLabel");
        public static string SchemaViewAddFieldNamePlaceholder => GetString("SchemaViewAddFieldNamePlaceholder");
        public static string SchemaViewAddFieldTypePlaceholder => GetString("SchemaViewAddFieldTypePlaceholder");
        public static string SchemaViewAddFieldAddButton => GetString("SchemaViewAddFieldAddButton");
        public static string SchemaViewAddFieldTypeLabel => GetString("SchemaViewAddFieldTypeLabel");
        public static string SchemaViewDeleteFieldButton => GetString("SchemaViewDeleteFieldButton");
        public static string SchemaViewDeleteFieldConfirmTitle => GetString("SchemaViewDeleteFieldConfirmTitle");
        public static string SchemaViewDeleteFieldConfirmMessage => GetString("SchemaViewDeleteFieldConfirmMessage");
        public static string SchemaViewAddFieldLengthLabel => GetString("SchemaViewAddFieldLengthLabel");
        public static string SchemaViewAddFieldLengthPlaceholder => GetString("SchemaViewAddFieldLengthPlaceholder");
    }
}
