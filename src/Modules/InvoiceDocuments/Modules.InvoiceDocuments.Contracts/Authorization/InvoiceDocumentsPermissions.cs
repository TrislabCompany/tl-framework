using Trislab.Framework.Shared.Constants;

namespace Trislab.Modules.InvoiceDocuments.Contracts.Authorization;

public static class InvoiceDocumentsPermissions
{
    public static class Customers
    {
        public const string Resource = "InvoiceDocuments.Customers";
        public const string View   = $"Permissions.{Resource}.View";
        public const string Create = $"Permissions.{Resource}.Create";
        public const string Update = $"Permissions.{Resource}.Update";
        public const string Delete = $"Permissions.{Resource}.Delete";
    }

    public static class Items
    {
        public const string Resource = "InvoiceDocuments.Items";
        public const string View   = $"Permissions.{Resource}.View";
        public const string Create = $"Permissions.{Resource}.Create";
        public const string Update = $"Permissions.{Resource}.Update";
        public const string Delete = $"Permissions.{Resource}.Delete";
    }

    public static class Documents
    {
        public const string Resource = "InvoiceDocuments.Documents";
        public const string View     = $"Permissions.{Resource}.View";
        public const string Create   = $"Permissions.{Resource}.Create";
        public const string Update   = $"Permissions.{Resource}.Update";
        public const string Delete   = $"Permissions.{Resource}.Delete";
        public const string Issue    = $"Permissions.{Resource}.Issue";
        public const string MarkPaid = $"Permissions.{Resource}.MarkPaid";
        public const string Send     = $"Permissions.{Resource}.Send";

        /// <summary>The accountant's read-only access: the export of a period.</summary>
        public const string Export   = $"Permissions.{Resource}.Export";
    }

    public static class Settings
    {
        public const string Resource = "InvoiceDocuments.Settings";
        public const string View   = $"Permissions.{Resource}.View";
        public const string Update = $"Permissions.{Resource}.Update";
    }

    public static IReadOnlyList<TrislabPermission> All { get; } =
    [
        new("View Customers",   ActionConstants.View,   Customers.Resource),
        new("Create Customers", ActionConstants.Create, Customers.Resource),
        new("Update Customers", ActionConstants.Update, Customers.Resource),
        new("Delete Customers", ActionConstants.Delete, Customers.Resource),

        new("View Items",   ActionConstants.View,   Items.Resource),
        new("Create Items", ActionConstants.Create, Items.Resource),
        new("Update Items", ActionConstants.Update, Items.Resource),
        new("Delete Items", ActionConstants.Delete, Items.Resource),

        new("View Documents",      ActionConstants.View,   Documents.Resource),
        new("Create Documents",    ActionConstants.Create, Documents.Resource),
        new("Update Documents",    ActionConstants.Update, Documents.Resource),
        new("Delete Documents",    ActionConstants.Delete, Documents.Resource),
        new("Issue Documents",     "Issue",                Documents.Resource),
        new("Mark Documents Paid", "MarkPaid",             Documents.Resource),
        new("Send Documents",      "Send",                 Documents.Resource),
        new("Export Documents",    "Export",               Documents.Resource),

        new("View Invoice Settings",   ActionConstants.View,   Settings.Resource),
        new("Update Invoice Settings", ActionConstants.Update, Settings.Resource),
    ];
}
