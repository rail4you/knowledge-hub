using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using KnowledgeHub.ResourceLibraries.Dtos;
using Volo.Abp.Application.Dtos;

namespace KnowledgeHub.ResourceLibraries;

/// <summary>
/// 「其他学员」AppService —— PRD 5.3「其他学员文件夹机制」。
///
/// <para>
/// 规则：
/// </para>
/// <list type="bullet">
/// <item><description>「其他学员」是系统级默认目录，不需要老师手动创建；</description></item>
/// <item><description>
/// 当有第一个其他学员开始学习该资源库时，系统在主建老师账号下自动创建该学员档案；
/// </description></item>
/// <item><description>该目录内学员不归属任何班级，与老师自建班级天然隔离；支持按学员维度查看、导出。</description></item>
/// </list>
/// </summary>
public interface IResourceLibraryLearnerAppService
{
    /// <summary>
    /// 登记 / 更新自己的「其他学员」档案（手机号验证 + 实名认证）。
    /// 登记成功后即可学习该库中对外开放自学的课程。
    /// </summary>
    Task<ResourceLibraryLearnerDto> RegisterAsync(RegisterOtherLearnerInput input);

    /// <summary>取我在某个资源库的「其他学员」档案。</summary>
    Task<ResourceLibraryLearnerDto?> GetMyProfileAsync(Guid libraryId);

    /// <summary>教师端：查看名下资源库的「其他学员」目录。</summary>
    Task<PagedResultDto<ResourceLibraryLearnerDto>> GetLearnersAsync(GetResourceLibraryLearnersInput input);

    /// <summary>教师端：导出「其他学员」数据（Excel）。</summary>
    Task<byte[]> ExportLearnersAsync(GetResourceLibraryLearnersInput input);
}
