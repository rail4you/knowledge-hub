namespace KnowledgeHub.Features;

public static class KnowledgeHubFeatures
{
    public const string GroupName = "KnowledgeHub";
    private const string DefaultPrefix = GroupName + ".";

    public const string Edition = DefaultPrefix + "Edition";

    public const string MaxTenantCount = DefaultPrefix + "MaxTenantCount";

    public const string Alliance = DefaultPrefix + "Alliance";

    public const string TwoLevelApproval = DefaultPrefix + "TwoLevelApproval";
}

public static class KnowledgeHubEditions
{
    public const string Basic = "Basic";
    public const string Standard = "Standard";
}