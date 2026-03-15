using System;
using System.Threading.Tasks;
using KnowledgeHub.Documents;
using KnowledgeHub.Users;
using Volo.Abp.Data;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;

namespace KnowledgeHub;

public class KnowledgeHubDataSeederContributor
    : IDataSeedContributor, ITransientDependency
{
    private readonly IRepository<Document, Guid> _documentRepository;
    private readonly IUserRepository _userRepository;
    private readonly UserManager _userManager;

    public KnowledgeHubDataSeederContributor(
        IRepository<Document, Guid> documentRepository,
        IUserRepository userRepository,
        UserManager userManager)
    {
        _documentRepository = documentRepository;
        _userRepository = userRepository;
        _userManager = userManager;
    }

    public async Task SeedAsync(DataSeedContext context)
    {
        if (await _documentRepository.GetCountAsync() > 0)
        {
            return;
        }

        var orwell = await _userRepository.InsertAsync(
            await _userManager.CreateAsync(
                "George Orwell",
                new DateTime(1903, 06, 25),
                "Orwell produced literary criticism and poetry, fiction and polemical journalism; and is best known for the allegorical novella Animal Farm (1945) and the dystopian novel Nineteen Eighty-Four (1949)."
            )
        );

        var douglas = await _userRepository.InsertAsync(
            await _userManager.CreateAsync(
                "Douglas Adams",
                new DateTime(1952, 03, 11),
                "Douglas Adams was an English author, screenwriter, essayist, humorist, satirist and dramatist. Adams was an advocate for environmentalism and conservation, a lover of fast cars, technological innovation and the Apple Macintosh, and a self-proclaimed 'radical atheist'."
            )
        );

        await _documentRepository.InsertAsync(
            new Document
            {
                UserId = orwell.Id,
                Name = "1984",
                Type = DocumentType.Dystopia,
                PublishDate = new DateTime(1949, 6, 8),
                Price = 19.84f
            },
            autoSave: true
        );

        await _documentRepository.InsertAsync(
            new Document
            {
                UserId = douglas.Id,
                Name = "The Hitchhiker's Guide to the Galaxy",
                Type = DocumentType.ScienceFiction,
                PublishDate = new DateTime(1995, 9, 27),
                Price = 42.0f
            },
            autoSave: true
        );
    }
}
