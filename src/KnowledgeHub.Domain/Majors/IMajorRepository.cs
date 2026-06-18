using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Volo.Abp.Domain.Repositories;

namespace KnowledgeHub.Majors;

public interface IMajorRepository : IRepository<Major, Guid>
{
    Task<Major?> FindByNameAsync(string name, CancellationToken cancellationToken = default);

    Task<Major?> FindByCodeAsync(string code, CancellationToken cancellationToken = default);

    Task<List<Major>> GetLookupListAsync(CancellationToken cancellationToken = default);
}
