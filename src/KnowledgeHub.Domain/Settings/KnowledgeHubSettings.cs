namespace KnowledgeHub.Settings;

public static class KnowledgeHubSettings
{
    private const string Prefix = "KnowledgeHub";
    private const string InstallPrefix = Prefix + ".Install";

    public const string IsInstalled = InstallPrefix + ".IsInstalled";
    public const string InstalledEdition = InstallPrefix + ".InstalledEdition";
    public const string LicenseKey = InstallPrefix + ".LicenseKey";
}