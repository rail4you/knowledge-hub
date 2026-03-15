using System;
using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.Domain.Services;

namespace KnowledgeHub.Users;

public class UserManager : DomainService
{
    private readonly IUserRepository _userRepository;

    public UserManager(IUserRepository userRepository)
    {
        _userRepository = userRepository;
    }

    public async Task<AppUser> CreateAsync(
        string name,
        DateTime birthDate,
        string? shortBio = null)
    {
        Check.NotNullOrWhiteSpace(name, nameof(name));

        var existingUser = await _userRepository.FindByNameAsync(name);
        if (existingUser != null)
        {
            throw new UserAlreadyExistsException(name);
        }

        return new AppUser(
            GuidGenerator.Create(),
            name,
            birthDate,
            shortBio
        );
    }

    public async Task ChangeNameAsync(
        AppUser user,
        string newName)
    {
        Check.NotNull(user, nameof(user));
        Check.NotNullOrWhiteSpace(newName, nameof(newName));

        var existingUser = await _userRepository.FindByNameAsync(newName);
        if (existingUser != null && existingUser.Id != user.Id)
        {
            throw new UserAlreadyExistsException(newName);
        }

        user.ChangeName(newName);
    }
}
