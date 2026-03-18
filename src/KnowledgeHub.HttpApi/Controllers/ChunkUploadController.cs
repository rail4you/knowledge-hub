using System;
using System.Threading.Tasks;
using KnowledgeHub.Resources;
using KnowledgeHub.Resources.FileStorage;
using KnowledgeHub.Permissions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Volo.Abp;
using Volo.Abp.AspNetCore.Mvc;

namespace KnowledgeHub.Controllers;

[Authorize(KnowledgeHubPermissions.Resources.Default)]
public class ChunkUploadController : AbpControllerBase
{
    private readonly IFileStorageService _fileStorageService;

    public ChunkUploadController(IFileStorageService fileStorageService)
    {
        _fileStorageService = fileStorageService;
    }

    [HttpPost]
    [Route("api/app/chunk-upload/initiate")]
    public async Task<InitiateUploadResultDto> InitiateUpload([FromBody] InitiateUploadDto input)
    {
        var uploadId = Guid.NewGuid().ToString();
        var totalChunks = (int)Math.Ceiling((double)input.TotalSize / input.ChunkSize);

        return new InitiateUploadResultDto
        {
            UploadId = uploadId,
            ChunkSize = input.ChunkSize,
            TotalChunks = totalChunks
        };
    }

    [HttpPost]
    [Route("api/app/chunk-upload/upload")]
    [DisableRequestSizeLimit]
    [RequestFormLimits(MultipartBodyLengthLimit = 524288000)]
    public async Task<bool> UploadChunk(IFormFile file, [FromForm] string uploadId, [FromForm] string fileName, [FromForm] int chunkNumber)
    {
        if (file == null || file.Length == 0)
        {
            return false;
        }

        try
        {
            using var stream = file.OpenReadStream();
            await _fileStorageService.SaveChunkAsync(stream, fileName, chunkNumber, uploadId);
            return true;
        }
        catch
        {
            return false;
        }
    }

    [HttpPost]
    [Route("api/app/chunk-upload/complete")]
    public async Task<CompleteUploadResultDto> CompleteUpload([FromBody] CompleteUploadDto input)
    {
        var fileName = input.FileName;
        var extension = System.IO.Path.GetExtension(fileName);
        var directory = DateTime.Now.ToString("yyyyMMdd");

        var filePath = await _fileStorageService.MergeChunksAsync(input.UploadId, fileName, directory);

        var fullFilePath = System.IO.Path.Combine(((LocalFileStorageService)_fileStorageService).RootPath, filePath);
        var fileInfo = new System.IO.FileInfo(fullFilePath);

        return new CompleteUploadResultDto
        {
            FilePath = filePath,
            FileSize = fileInfo.Length,
            FileExtension = extension,
            OriginalFileName = fileName
        };
    }
}
