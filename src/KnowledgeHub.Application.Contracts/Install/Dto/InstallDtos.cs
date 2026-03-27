namespace KnowledgeHub.Install.Dto;

public class InstallStatusDto
{
    public bool IsInstalled { get; set; }
    public string CurrentEdition { get; set; } = "Basic";
}

public class InstallInputDto
{
    public string LicenseKey { get; set; } = string.Empty;
    
    public string Edition { get; set; } = "Basic";
    
    public string AdminUsername { get; set; } = "admin";
    
    public string AdminPassword { get; set; } = string.Empty;
    
    public string AdminEmail { get; set; } = "admin@knowledgehub.com";
}

public class EditionUpgradeInputDto
{
    public string LicenseKey { get; set; } = string.Empty;
}

public class EditionDto
{
    public string Edition { get; set; } = "Basic";
    
    public int MaxTenantCount { get; set; }
    
    public bool IsAllianceEnabled { get; set; }
    
    public bool IsTwoLevelApprovalEnabled { get; set; }
}