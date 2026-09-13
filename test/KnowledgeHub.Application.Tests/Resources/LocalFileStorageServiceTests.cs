using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using KnowledgeHub.Common;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Shouldly;
using Volo.Abp;
using Xunit;

namespace KnowledgeHub.Resources.FileStorage;

public class LocalFileStorageServiceTests : IDisposable
{
    private readonly string _root;
    private readonly LocalFileStorageService _service;

    public LocalFileStorageServiceTests()
    {
        _root = Path.Combine(Path.GetTempPath(), "kh-fs-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_root);

        _service = new LocalFileStorageService(
            new FakeHostEnvironment { ContentRootPath = _root },
            Options.Create(new FileStorageOptions { RootPath = "uploads" }));
    }

    public void Dispose()
    {
        try { Directory.Delete(_root, true); } catch { /* ignore */ }
    }

    private sealed class FakeHostEnvironment : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = "Development";
        public string ApplicationName { get; set; } = "Test";
        public string ContentRootPath { get; set; } = string.Empty;
        public IFileProvider ContentRootFileProvider { get; set; } = null!;
    }

    [Fact]
    public void GetFilePath_Should_Strip_Directory_Components()
    {
        _service.GetFilePath("20260101", "../../evil.txt")
            .ShouldBe(Path.Combine("20260101", "evil.txt"));
        _service.GetFilePath(string.Empty, "../../evil.txt").ShouldBe("evil.txt");
    }

    [Fact]
    public async Task SaveAsync_With_Traversal_File_Name_Should_Stay_Under_Root()
    {
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes("x"));
        var relative = await _service.SaveAsync(stream, "../../escape.txt", "20260101");

        relative.ShouldNotContain("..");
        File.Exists(Path.Combine(_service.RootPath, relative)).ShouldBeTrue();
        File.Exists(Path.Combine(_root, "escape.txt")).ShouldBeFalse();
        File.Exists(Path.Combine(Path.GetDirectoryName(_root)!, "escape.txt")).ShouldBeFalse();
    }

    [Fact]
    public async Task SaveChunkAsync_With_Invalid_UploadId_Should_Throw()
    {
        using var stream = new MemoryStream(new byte[] { 1, 2, 3 });
        await Should.ThrowAsync<BusinessException>(() =>
            _service.SaveChunkAsync(stream, "a.txt", 1, "../../etc"));
    }

    [Fact]
    public async Task MergeChunksAsync_Should_Sanitize_And_Merge_In_Order()
    {
        var uploadId = Guid.NewGuid().ToString();
        using (var c1 = new MemoryStream(Encoding.UTF8.GetBytes("hello")))
            await _service.SaveChunkAsync(c1, "../part.txt", 1, uploadId);
        using (var c2 = new MemoryStream(Encoding.UTF8.GetBytes("world")))
            await _service.SaveChunkAsync(c2, "../part.txt", 2, uploadId);

        var relative = await _service.MergeChunksAsync(uploadId, "../../merged.txt", "20260102");

        relative.ShouldNotContain("..");
        var full = Path.Combine(_service.RootPath, relative);
        File.Exists(full).ShouldBeTrue();
        File.ReadAllText(full).ShouldBe("helloworld");
    }

    [Fact]
    public async Task GetAsync_With_Traversal_Should_Throw()
    {
        await Should.ThrowAsync<BusinessException>(() => _service.GetAsync("../../etc/passwd"));
    }
}
