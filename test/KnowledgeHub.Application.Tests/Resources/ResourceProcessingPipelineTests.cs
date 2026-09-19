using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using KnowledgeHub.Resources.Conversion;
using KnowledgeHub.Resources.FileStorage;
using KnowledgeHub.Resources.Media;
using KnowledgeHub.Resources.Thumbnails;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Shouldly;
using Xunit;

namespace KnowledgeHub.Resources.Pipeline;

/// <summary>
/// 资源处理流水线综合测试（纯单元，无需 DB / ffmpeg / Gotenberg / 网络）：
/// 1) 流水线触发时机默认值（上传→草稿，发布→预览，校审→索引）；
/// 2) 遗留 ResourceMedia 节点兼容映射；
/// 3) 转换并发闸门语义；
/// 4) Gotenberg 失败负缓存（失败风暴退避）与成功缓存命中；
/// 5) 缩略图磁盘缓存命中/失效；
/// 6) 媒体处理结果契约。
/// </summary>
public class ResourceProcessingPipelineTests : IDisposable
{
    private readonly string _root;

    public ResourceProcessingPipelineTests()
    {
        _root = Path.Combine(Path.GetTempPath(), "kh-pipeline-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_root);
    }

    public void Dispose()
    {
        try { Directory.Delete(_root, true); } catch { /* ignore */ }
    }

    // ── 1. 触发时机默认值 ──────────────────────────────────────────────

    [Fact]
    public void FlowOptions_Defaults_Should_Match_Draft_Preview_Index_Lifecycle()
    {
        var flow = new ResourceProcessingFlowOptions();

        // 上传仅存草稿，不生成任何后台任务
        flow.ProcessOnUpload.ShouldBeFalse();
        // 发布/提交审核生成预览供审核人查看
        flow.GeneratePreviewOnPublish.ShouldBeTrue();
        // 院校审核通过后启动索引
        flow.GenerateIndexOnSchoolApproval.ShouldBeTrue();
        // 视频缩略图延后到院校审核（发布阶段跳过视频）
        flow.GenerateVideoThumbnailOnSchoolApproval.ShouldBeTrue();
        // 默认并行：媒体与索引互不阻塞
        flow.StartAfterIndexing.ShouldBeFalse();
    }

    // ── 2. 遗留配置映射 ────────────────────────────────────────────────

    [Fact]
    public void ApplyLegacy_Without_Legacy_Values_Should_Keep_Flow_Untouched()
    {
        var flow = new ResourceProcessingFlowOptions
        {
            ProcessOnUpload = true,
            GeneratePreviewOnPublish = false,
        };

        ResourceProcessingFlowMapper.ApplyLegacy(flow, legacyStartAfterIndexing: null, legacyGenerateOnApproval: null);

        flow.ProcessOnUpload.ShouldBeTrue();
        flow.GeneratePreviewOnPublish.ShouldBeFalse();
        flow.StartAfterIndexing.ShouldBeFalse();
    }

    [Fact]
    public void ApplyLegacy_StartAfterIndexing_Should_Pass_Through()
    {
        var flow = new ResourceProcessingFlowOptions();

        ResourceProcessingFlowMapper.ApplyLegacy(flow, legacyStartAfterIndexing: true, legacyGenerateOnApproval: null);

        flow.StartAfterIndexing.ShouldBeTrue();
        // 其他开关不受影响
        flow.ProcessOnUpload.ShouldBeFalse();
        flow.GeneratePreviewOnPublish.ShouldBeTrue();
    }

    [Fact]
    public void ApplyLegacy_GenerateOnApproval_Should_Mean_Approval_Only()
    {
        var flow = new ResourceProcessingFlowOptions { ProcessOnUpload = true };

        ResourceProcessingFlowMapper.ApplyLegacy(flow, legacyStartAfterIndexing: null, legacyGenerateOnApproval: true);

        // 上传/发布阶段不做功，索引与媒体都留到院校审核后
        flow.ProcessOnUpload.ShouldBeFalse();
        flow.GeneratePreviewOnPublish.ShouldBeFalse();
        flow.GenerateIndexOnSchoolApproval.ShouldBeTrue();
        flow.GenerateVideoThumbnailOnSchoolApproval.ShouldBeTrue();
    }

    [Fact]
    public void ApplyLegacy_False_Values_Should_Change_Nothing()
    {
        var flow = new ResourceProcessingFlowOptions();

        ResourceProcessingFlowMapper.ApplyLegacy(flow, legacyStartAfterIndexing: false, legacyGenerateOnApproval: false);

        flow.StartAfterIndexing.ShouldBeFalse();
        flow.ProcessOnUpload.ShouldBeFalse();
        flow.GeneratePreviewOnPublish.ShouldBeTrue();
    }

    [Fact]
    public void ApplyLegacy_Both_Flags_Should_Combine()
    {
        var flow = new ResourceProcessingFlowOptions();

        ResourceProcessingFlowMapper.ApplyLegacy(flow, legacyStartAfterIndexing: true, legacyGenerateOnApproval: true);

        flow.StartAfterIndexing.ShouldBeTrue();
        flow.ProcessOnUpload.ShouldBeFalse();
        flow.GeneratePreviewOnPublish.ShouldBeFalse();
    }

    // ── 3. 转换并发闸门 ────────────────────────────────────────────────

    [Fact]
    public void ConversionGate_Default_Should_Be_Serial()
    {
        var manager = new ConversionConcurrencyManager(NullLogger<ConversionConcurrencyManager>.Instance);

        manager.GetGate("preview").MaxConcurrent.ShouldBe(1);
    }

    [Fact]
    public async Task ConversionGate_Should_Serialize_Acquirers()
    {
        var manager = new ConversionConcurrencyManager(NullLogger<ConversionConcurrencyManager>.Instance);
        var gate = manager.GetGate("preview");

        using var first = await gate.AcquireAsync();
        var pending = gate.AcquireAsync();

        // 额度被占满时第二个请求必须排队（同步判定，无时间 flake）
        pending.IsCompleted.ShouldBeFalse();

        first.Dispose();
        using var second = await pending;
        second.ShouldNotBeNull();
    }

    [Fact]
    public async Task ConversionGate_SetMaxConcurrent_Should_Release_Waiters()
    {
        var manager = new ConversionConcurrencyManager(NullLogger<ConversionConcurrencyManager>.Instance);
        var gate = manager.GetGate("preview");

        using var first = await gate.AcquireAsync();
        var pending = gate.AcquireAsync();
        pending.IsCompleted.ShouldBeFalse();

        // 在线放开并发：排队者立即被放行，已持有者不受影响
        gate.SetMaxConcurrent(2);

        using var second = await pending;
        second.ShouldNotBeNull();
        gate.MaxConcurrent.ShouldBe(2);
    }

    [Fact]
    public void ConversionGate_SetMaxConcurrent_Below_One_Should_Clamp_To_One()
    {
        var manager = new ConversionConcurrencyManager(NullLogger<ConversionConcurrencyManager>.Instance);

        manager.SetMaxConcurrent("preview", 0);

        manager.GetGate("preview").MaxConcurrent.ShouldBe(1);
    }

    // ── 4. Gotenberg 失败负缓存 ─────────────────────────────────────────

    private sealed class FakeFileStorageService : IFileStorageService
    {
        public FakeFileStorageService(string root) => RootPath = root;
        public string RootPath { get; }
        public Task<string> SaveAsync(Stream stream, string fileName, string directory) => throw new NotImplementedException();
        public Task<string> SaveChunkAsync(Stream stream, string fileName, int chunkNumber, string uploadId) => throw new NotImplementedException();
        public Task<string> MergeChunksAsync(string uploadId, string fileName, string directory) => throw new NotImplementedException();
        public Task<Stream> GetAsync(string path) => throw new NotImplementedException();
        public Task DeleteAsync(string path) => throw new NotImplementedException();
        public string GetFileUrl(string path) => throw new NotImplementedException();
        public bool HasChunk(string uploadId, int chunkNumber) => throw new NotImplementedException();
        public string GetFilePath(string directory, string fileName) => throw new NotImplementedException();
    }

    private sealed class FakeFfmpegRunner : IFfmpegRunner
    {
        public int CallCount { get; private set; }
        public Task<ProcessRunResult> RunFfmpegAsync(IReadOnlyList<string> args, TimeSpan? timeout = null, CancellationToken ct = default)
        {
            CallCount++;
            return Task.FromResult(ProcessRunResult.Fail("fake-ffmpeg-failure"));
        }

        public Task<ProcessRunResult> RunAsync(string executable, IReadOnlyList<string> args, TimeSpan timeout, CancellationToken ct = default)
        {
            CallCount++;
            return Task.FromResult(ProcessRunResult.Fail("fake-runner-failure"));
        }
    }

    private sealed class CountingHttpHandler : HttpMessageHandler
    {
        public int CallCount { get; private set; }
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            CallCount++;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.InternalServerError)
            {
                Content = new StringContent("gotenberg-boom"),
            });
        }
    }

    private sealed class FakeHttpClientFactory : IHttpClientFactory
    {
        private readonly HttpMessageHandler _handler;
        public FakeHttpClientFactory(HttpMessageHandler handler) => _handler = handler;
        public HttpClient CreateClient(string name) =>
            new HttpClient(_handler, disposeHandler: false) { BaseAddress = new Uri("http://localhost:3000") };
    }

    private sealed class ConversionHarness : IDisposable
    {
        public string Root { get; }
        public string SourcePath { get; }
        public string ResourceId { get; } = Guid.NewGuid().ToString("N");
        public CountingHttpHandler HttpHandler { get; } = new();
        public GotenbergConversionService Service { get; }
        private readonly OfficeConversionOptions _options;

        public ConversionHarness(string root, int negativeCacheSeconds = 30)
        {
            Root = root;
            _options = new OfficeConversionOptions
            {
                BaseUrl = "http://localhost:3000",
                ConversionTimeoutSeconds = 30,
                ConversionNegativeCacheSeconds = negativeCacheSeconds,
                CacheDirectory = "converted",
                LinearizePdf = false, // 单测不依赖 qpdf 二进制
            };
            var storage = new FakeFileStorageService(root);
            var options = Options.Create(_options);
            var runner = new FakeFfmpegRunner();
            var preprocessor = new PptxImagePreprocessor(
                storage, runner, options, NullLogger<PptxImagePreprocessor>.Instance);
            Service = new GotenbergConversionService(
                new FakeHttpClientFactory(HttpHandler),
                storage,
                options,
                new ConversionConcurrencyManager(NullLogger<ConversionConcurrencyManager>.Instance),
                preprocessor,
                runner,
                NullLogger<GotenbergConversionService>.Instance);

            // .docx：跳过 PPTX 修复与媒体预压缩，直达 Gotenberg 调用，最适合测负缓存
            SourcePath = Path.Combine(root, "uploads", "case.docx");
            Directory.CreateDirectory(Path.GetDirectoryName(SourcePath)!);
            File.WriteAllBytes(SourcePath, new byte[] { 0x50, 0x4B, 0x03, 0x04, 0x01, 0x02 });
        }

        public void RewriteSource(byte[] content) => File.WriteAllBytes(SourcePath, content);

        public void PlantValidCache()
        {
            var dir = Path.Combine(Root, "converted");
            Directory.CreateDirectory(dir);
            var pdfPath = Path.Combine(dir, $"{ResourceId}.pdf");
            File.WriteAllBytes(pdfPath, new byte[] { 0x25, 0x50, 0x44, 0x46 });
            var info = new FileInfo(SourcePath);
            File.WriteAllText(
                Path.Combine(dir, $"{ResourceId}.meta"),
                JsonSerializer.Serialize(new { info.LastWriteTimeUtc, info.Length }));
        }

        public void Dispose()
        {
        }
    }

    [Fact]
    public async Task ConvertToPdf_Failure_Should_ShortCircuit_Repeat_Calls_Within_Ttl()
    {
        using var harness = new ConversionHarness(_root);

        var first = await Should.ThrowAsync<OfficeConversionException>(() =>
            harness.Service.ConvertToPdfAsync(harness.ResourceId, harness.SourcePath));
        first.Message.ShouldNotContain("刚失败"); // 首次是真实的下游失败

        var second = await Should.ThrowAsync<OfficeConversionException>(() =>
            harness.Service.ConvertToPdfAsync(harness.ResourceId, harness.SourcePath));
        second.Message.ShouldContain("刚失败"); // TTL 内直接短路，不再打 Gotenberg

        harness.HttpHandler.CallCount.ShouldBe(1);
    }

    [Fact]
    public async Task ConvertToPdf_InvalidateCache_Should_Clear_Negative_Cache()
    {
        using var harness = new ConversionHarness(_root);

        await Should.ThrowAsync<OfficeConversionException>(() =>
            harness.Service.ConvertToPdfAsync(harness.ResourceId, harness.SourcePath));
        harness.HttpHandler.CallCount.ShouldBe(1);

        harness.Service.InvalidateCache(harness.ResourceId);

        await Should.ThrowAsync<OfficeConversionException>(() =>
            harness.Service.ConvertToPdfAsync(harness.ResourceId, harness.SourcePath));
        harness.HttpHandler.CallCount.ShouldBe(2);
    }

    [Fact]
    public async Task ConvertToPdf_NegativeCacheDisabled_Should_Hit_Http_Every_Time()
    {
        using var harness = new ConversionHarness(_root, negativeCacheSeconds: 0);

        await Should.ThrowAsync<OfficeConversionException>(() =>
            harness.Service.ConvertToPdfAsync(harness.ResourceId, harness.SourcePath));
        await Should.ThrowAsync<OfficeConversionException>(() =>
            harness.Service.ConvertToPdfAsync(harness.ResourceId, harness.SourcePath));

        harness.HttpHandler.CallCount.ShouldBe(2);
    }

    [Fact]
    public async Task ConvertToPdf_SourceChanged_Should_Bypass_Negative_Cache()
    {
        using var harness = new ConversionHarness(_root);

        await Should.ThrowAsync<OfficeConversionException>(() =>
            harness.Service.ConvertToPdfAsync(harness.ResourceId, harness.SourcePath));
        harness.HttpHandler.CallCount.ShouldBe(1);

        // 源文件变化（长度不同）→ 负缓存失效，必须重新尝试转换
        harness.RewriteSource(new byte[] { 0x50, 0x4B, 0x03, 0x04, 0x01, 0x02, 0x03, 0x04, 0x05 });

        await Should.ThrowAsync<OfficeConversionException>(() =>
            harness.Service.ConvertToPdfAsync(harness.ResourceId, harness.SourcePath));
        harness.HttpHandler.CallCount.ShouldBe(2);
    }

    [Fact]
    public async Task ConvertToPdf_ValidCache_Should_Return_Without_Http_And_Clear_Failure()
    {
        using var harness = new ConversionHarness(_root);

        await Should.ThrowAsync<OfficeConversionException>(() =>
            harness.Service.ConvertToPdfAsync(harness.ResourceId, harness.SourcePath));
        harness.HttpHandler.CallCount.ShouldBe(1);

        // 同一源文件的有效缓存落盘后：命中缓存、不打 HTTP，并清除失败记录
        harness.PlantValidCache();
        var pdf = await harness.Service.ConvertToPdfAsync(harness.ResourceId, harness.SourcePath);

        pdf.ShouldEndWith($"{harness.ResourceId}.pdf");
        harness.HttpHandler.CallCount.ShouldBe(1);
    }

    // ── 5. 缩略图磁盘缓存 ───────────────────────────────────────────────

    private sealed class ExplodingFfmpegRunner : IFfmpegRunner
    {
        public bool Called { get; private set; }
        public Task<ProcessRunResult> RunFfmpegAsync(IReadOnlyList<string> args, TimeSpan? timeout = null, CancellationToken ct = default)
        {
            Called = true;
            return Task.FromResult(ProcessRunResult.Fail("should-not-run"));
        }

        public Task<ProcessRunResult> RunAsync(string executable, IReadOnlyList<string> args, TimeSpan timeout, CancellationToken ct = default)
        {
            Called = true;
            return Task.FromResult(ProcessRunResult.Fail("should-not-run"));
        }
    }

    private string WriteImageSource()
    {
        var path = Path.Combine(_root, "uploads", "pic.png");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        // 内容无所谓：缓存命中路径在调 ffmpeg 之前就返回，不解析图片
        File.WriteAllBytes(path, new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x01 });
        return path;
    }

    private static void WriteThumbMeta(string metaPath, FileInfo source, long? lengthOverride = null)
    {
        File.WriteAllText(metaPath, JsonSerializer.Serialize(new
        {
            Length = lengthOverride ?? source.Length,
            source.LastWriteTimeUtc,
        }));
    }

    [Fact]
    public async Task Thumbnail_CacheHit_Should_Return_Without_Running_Ffmpeg()
    {
        var source = WriteImageSource();
        var info = new FileInfo(source);
        const int width = 400;
        const string key = "res-thumb-hit";

        var dir = Path.Combine(_root, "thumbnails");
        Directory.CreateDirectory(dir);
        var thumbPath = Path.Combine(dir, $"{key}_{width}.jpg");
        File.WriteAllBytes(thumbPath, new byte[] { 0xFF, 0xD8, 0xFF });
        WriteThumbMeta(Path.Combine(dir, $"{key}_{width}.meta"), info);

        var runner = new ExplodingFfmpegRunner();
        var service = new FfmpegResourceThumbnailService(
            new FakeFileStorageService(_root), runner, NullLogger<FfmpegResourceThumbnailService>.Instance);

        var result = await service.GetOrCreateAsync(key, source, width);

        result.ShouldBe(thumbPath);
        runner.Called.ShouldBeFalse();
    }

    [Fact]
    public async Task Thumbnail_StaleMeta_Should_Fall_Through_And_Return_Null_On_Failure()
    {
        var source = WriteImageSource();
        var info = new FileInfo(source);
        const int width = 400;
        const string key = "res-thumb-stale";

        var dir = Path.Combine(_root, "thumbnails");
        Directory.CreateDirectory(dir);
        File.WriteAllBytes(Path.Combine(dir, $"{key}_{width}.jpg"), new byte[] { 0xFF, 0xD8, 0xFF });
        // meta 记录的长度与源文件不一致 → 缓存失效，必须走 ffmpeg
        WriteThumbMeta(Path.Combine(dir, $"{key}_{width}.meta"), info, lengthOverride: info.Length + 999);

        var runner = new ExplodingFfmpegRunner();
        var service = new FfmpegResourceThumbnailService(
            new FakeFileStorageService(_root), runner, NullLogger<FfmpegResourceThumbnailService>.Instance);

        var result = await service.GetOrCreateAsync(key, source, width);

        result.ShouldBeNull();
        runner.Called.ShouldBeTrue();
    }

    [Fact]
    public async Task Thumbnail_UnsupportedType_Should_Return_Null_Without_Running()
    {
        var txt = Path.Combine(_root, "uploads", "doc.txt");
        Directory.CreateDirectory(Path.GetDirectoryName(txt)!);
        File.WriteAllText(txt, "hello");

        var runner = new ExplodingFfmpegRunner();
        var service = new FfmpegResourceThumbnailService(
            new FakeFileStorageService(_root), runner, NullLogger<FfmpegResourceThumbnailService>.Instance);

        var result = await service.GetOrCreateAsync("res-txt", txt, 400);

        result.ShouldBeNull();
        runner.Called.ShouldBeFalse();
    }

    // ── 6. 媒体处理结果契约 ─────────────────────────────────────────────

    [Fact]
    public void MediaProcessOutcome_Should_Carry_Status_Error_And_Version()
    {
        var versionId = Guid.NewGuid();

        MediaProcessOutcome.Ok.Status.ShouldBe(MediaProcessStatus.Completed);
        MediaProcessOutcome.Ok.Error.ShouldBeNull();

        var partial = MediaProcessOutcome.Partial("缩略图失败");
        partial.Status.ShouldBe(MediaProcessStatus.PartialFailed);
        partial.Error.ShouldNotBeNull();
        partial.Error.ShouldContain("缩略图失败");

        var failed = MediaProcessOutcome.Fail("源文件不存在") with { ResourceVersionId = versionId };
        failed.Status.ShouldBe(MediaProcessStatus.Failed);
        failed.ResourceVersionId.ShouldBe(versionId);
    }
}
