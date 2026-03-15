using System;
using Volo.Abp.Application.Dtos;

namespace KnowledgeHub.Users;

public class UserDto : EntityDto<Guid>
{
    public string Name { get; set; }

    public DateTime BirthDate { get; set; }

    public string ShortBio { get; set; }
}
