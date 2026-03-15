using System;
using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;

namespace KnowledgeHub.Users;

public class AppUser : FullAuditedAggregateRoot<Guid>
{
    public string Name { get; private set; }
    public DateTime BirthDate { get; set; }
    public string? ShortBio { get; set; }


    internal AppUser(
        Guid id,
        string name,
        DateTime birthDate,
        string? shortBio = null)
        : base(id)
    {
        SetName(name);
        Name = name;
        BirthDate = birthDate;
        ShortBio = shortBio;
    }

    internal AppUser ChangeName(string name)
    {
        SetName(name);
        return this;
    }

    private void SetName(string name)
    {
        Name = Check.NotNullOrWhiteSpace(
            name, 
            nameof(name), 
            maxLength: UserConsts.MaxNameLength
        );
    }
}
