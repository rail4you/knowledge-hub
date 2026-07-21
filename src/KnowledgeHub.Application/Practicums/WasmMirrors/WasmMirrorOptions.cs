namespace KnowledgeHub.Practicums.WasmMirrors;

/// <summary>
/// 仿真实训 WASM 本地镜像配置。
/// 对应配置文件节点: <c>WasmMirror</c>。
/// </summary>
public class WasmMirrorOptions
{
    /// <summary>
    /// 是否启用 WASM 本地镜像静态托管。设为 false 时整个功能禁用，
    /// 前端管线下落到原 URL（走 /api/proxy/http/... 反向代理）。
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// 镜像根目录。绝对路径直接使用；相对路径相对 Kestrel ContentRootPath 解析。
    /// </summary>
    public string RootPath { get; set; } = "wasm-mirrors";

    /// <summary>
    /// 对外暴露的 URL 前缀，例如 <c>/wasm</c>。
    /// </summary>
    public string PublicBasePath { get; set; } = "/wasm";

    /// <summary>
    /// 是否必须存在 mirror.json 才认为镜像就绪。
    /// 设为 false 时只看 entry 文件是否存在（不推荐）。
    /// </summary>
    public bool RequireManifest { get; set; } = true;
}
