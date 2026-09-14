using System;
using System.ComponentModel.DataAnnotations;
using KnowledgeHub.AI;

namespace KnowledgeHub.Application.AI.Dtos;

public class TeachingSceneDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Prompt { get; set; } = string.Empty;
    public TeachingSceneCategory Category { get; set; }
    public int SortOrder { get; set; }
    public bool IsSystem { get; set; }
}

public class CreateUpdateTeachingSceneDto
{
    [Required]
    [StringLength(100)]
    public string Name { get; set; } = string.Empty;

    [Required]
    [StringLength(2000)]
    public string Prompt { get; set; } = string.Empty;

    public TeachingSceneCategory Category { get; set; }

    public int SortOrder { get; set; }
}
