using Volo.Abp;

namespace KnowledgeHub.Users;

public class UserAlreadyExistsException : BusinessException
{
    public UserAlreadyExistsException(string name)
        : base(KnowledgeHubDomainErrorCodes.UserAlreadyExists)
    {
        WithData("name", name);
    }
}
