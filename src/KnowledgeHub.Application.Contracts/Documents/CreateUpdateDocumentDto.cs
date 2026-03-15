using System;
using System.ComponentModel.DataAnnotations;

namespace KnowledgeHub.Documents;

public class CreateUpdateDocumentDto
{
    [Required]
    [StringLength(128)]
    public string Name { get; set; } = string.Empty;

    [Required]
    public DocumentType Type { get; set; } = DocumentType.Undefined;

    [Required]
    [DataType(DataType.Date)]
    public DateTime PublishDate { get; set; } = DateTime.Now;

    [Required]
    public float Price { get; set; }
    
    public Guid UserId { get; set; }
}
