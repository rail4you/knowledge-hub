using System.Collections.Generic;
using System.Threading.Tasks;
using Volo.Abp.Application.Services;

namespace KnowledgeHub.Practicums.WasmMirrors;

/// <summary>
/// 仿真实训 WASM 本地镜像服务。
///
/// 学生页面在加载时一次性获取 <see cref="GetMappingAsync"/>，
/// 把外部 sourceUrl 改写到本地 publicUrl（命中镜像时）或保持原样（未命中时）。
/// </summary>
public interface IWasmMirrorAppService : IApplicationService
{
    /// <summary>
    /// 获取 sourceUrl → publicUrl 的实时映射。
    /// 该方法有 60 秒内存缓存，根目录 mtime 变化时失效。
    /// </summary>
    Task<List<WasmMirrorMappingDto>> GetMappingAsync();

    /// <summary>
    /// 获取所有已知镜像的详情（仅在管理/诊断页面使用）。
    /// </summary>
    Task<List<WasmMirrorInfoDto>> GetAllAsync();
}
