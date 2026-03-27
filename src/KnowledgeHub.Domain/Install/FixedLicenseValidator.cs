using Volo.Abp.DependencyInjection;

namespace KnowledgeHub.Install;

public interface ILicenseValidator
{
    bool Validate(string licenseKey);
}

public class FixedLicenseValidator : ILicenseValidator, ITransientDependency
{
    private const string ValidLicensePrefix = "KH-STANDARD-";
    
    public bool Validate(string licenseKey)
    {
        if (string.IsNullOrWhiteSpace(licenseKey))
        {
            return false;
        }
        
        return licenseKey.StartsWith(ValidLicensePrefix) && licenseKey.Length > ValidLicensePrefix.Length;
    }
}