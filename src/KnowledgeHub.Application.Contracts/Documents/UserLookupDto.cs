using System;
using Volo.Abp.Application.Dtos;

namespace KnowledgeHub.Documents;

public class UserLookupDto : EntityDto<Guid>
{
    public string Name { get; set; }
}
