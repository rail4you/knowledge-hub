using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Volo.Abp.Domain.Repositories;

namespace KnowledgeHub.Users;

public interface IUserRepository : IRepository<AppUser, Guid>
{
    Task<AppUser> FindByNameAsync(string name);

    Task<List<AppUser>> GetListAsync(
        int skipCount,
        int maxResultCount,
        string sorting,
        string? filter = null
    );
}
