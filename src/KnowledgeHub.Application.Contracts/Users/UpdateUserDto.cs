using System;
using System.ComponentModel.DataAnnotations;

namespace KnowledgeHub.Users;

public class UpdateUserDto
{
    [Required]
    [StringLength(UserConsts.MaxNameLength)]
    public string Name { get; set; } = string.Empty;

    [Required]
    public DateTime BirthDate { get; set; }
    
    public string? ShortBio { get; set; }
}
