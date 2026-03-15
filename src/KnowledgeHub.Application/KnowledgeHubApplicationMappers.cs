using KnowledgeHub.Documents;
using KnowledgeHub.Users;
using Riok.Mapperly.Abstractions;
using Volo.Abp.Mapperly;

namespace KnowledgeHub;

[Mapper]
public partial class DocumentToDocumentDtoMapper : MapperBase<Document, DocumentDto>
{
    public override partial DocumentDto Map(Document source);

    public override partial void Map(Document source, DocumentDto destination);
}

[Mapper]
public partial class CreateUpdateDocumentDtoToDocumentMapper : MapperBase<CreateUpdateDocumentDto, Document>
{
    public override partial Document Map(CreateUpdateDocumentDto source);

    public override partial void Map(CreateUpdateDocumentDto source, Document destination);
}

[Mapper]
public partial class UserToUserDtoMapper : MapperBase<AppUser, UserDto>
{
    public override partial UserDto Map(AppUser source);
    
    public override partial void Map(AppUser source, UserDto destination);
}

[Mapper]
public partial class UserToUserLookupDtoMapper : MapperBase<AppUser, UserLookupDto>
{
    public override partial UserLookupDto Map(AppUser source);

    public override partial void Map(AppUser source, UserLookupDto destination);
}
