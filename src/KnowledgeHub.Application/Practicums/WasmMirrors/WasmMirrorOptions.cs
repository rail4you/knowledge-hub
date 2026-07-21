namespace KnowledgeHub.Practicums.WasmMirrors;

/// <summary>
/// 仿真实训 WASM 本地镜像配置。
/// 对应配置文件节点: <c>WasmMirror</c>。
/// </summary>
public class WasmMirrorOptions
{
    public bool Enabled { get; set; } = true;
    public string RootPath { get; set; } = "wasm-mirrors";
    public string PublicBasePath { get; set; } = "/wasm";
}
