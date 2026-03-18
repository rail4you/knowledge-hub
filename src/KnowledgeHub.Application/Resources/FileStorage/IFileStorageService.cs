using System.IO;
using System.Threading.Tasks;

namespace KnowledgeHub.Resources.FileStorage;

public interface IFileStorageService
{
    Task<string> SaveAsync(Stream stream, string fileName, string directory);
    Task<string> SaveChunkAsync(Stream stream, string fileName, int chunkNumber, string uploadId);
    Task<string> MergeChunksAsync(string uploadId, string fileName, string directory);
    Task<Stream> GetAsync(string path);
    Task DeleteAsync(string path);
    string GetFileUrl(string path);
    bool HasChunk(string uploadId, int chunkNumber);
    string GetFilePath(string directory, string fileName);
}
