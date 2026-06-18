using System;
using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace KnowledgeHub.Majors;

public class Major : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
    public const int MaxNameLength = 128;
    public const int MaxCodeLength = 64;
    public const int MaxDescriptionLength = 2000;
    public const int MaxTrainingObjectivesLength = 2000;

    public Guid? TenantId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Code { get; set; }
    public string? Description { get; set; }
    public string? TrainingObjectives { get; set; }

    public Major()
    {
    }

    public Major(Guid id, string name, string? code = null, string? description = null, string? trainingObjectives = null)
        : base(id)
    {
        SetName(name);
        SetCode(code);
        SetDescription(description);
        SetTrainingObjectives(trainingObjectives);
    }

    public void SetName(string name)
    {
        Name = Check.NotNullOrWhiteSpace(
            name,
            nameof(name),
            maxLength: MaxNameLength);
    }

    public void SetCode(string? code)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            Code = null;
            return;
        }

        Code = Check.Length(
            code.Trim(),
            nameof(code),
            maxLength: MaxCodeLength);
    }

    public void SetDescription(string? description)
    {
        if (string.IsNullOrWhiteSpace(description))
        {
            Description = null;
            return;
        }

        Description = Check.Length(
            description.Trim(),
            nameof(description),
            maxLength: MaxDescriptionLength);
    }

    public void SetTrainingObjectives(string? trainingObjectives)
    {
        if (string.IsNullOrWhiteSpace(trainingObjectives))
        {
            TrainingObjectives = null;
            return;
        }

        TrainingObjectives = Check.Length(
            trainingObjectives.Trim(),
            nameof(trainingObjectives),
            maxLength: MaxTrainingObjectivesLength);
    }
}
