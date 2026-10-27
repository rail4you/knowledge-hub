using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using KnowledgeHub.Certificates.Dtos;
using Volo.Abp.Application.Dtos;

namespace KnowledgeHub.Certificates;

/// <summary>
/// 结业证书 AppService（PRD 第 9 节「结业证书规则」）。
///
/// <para>
/// 规则：
/// </para>
/// <list type="number">
/// <item><description>
/// <b>申请</b>：其他学员完成学习并通过考核后，在个人端主动申请结业证书；
/// </description></item>
/// <item><description>
/// <b>审核发放</b>：申请提交后由平台管理员统一审核，审核通过后平台发放电子证书；
/// </description></item>
/// <item><description>
/// <b>证书模板</b>：由平台侧统一定义（平台管理员在后台配置模板：校徽位、学员姓名、课程名称、
/// 结业时间、证书编号、防伪二维码等字段），院校 / 老师不可自定义；
/// </description></item>
/// <item><description>
/// <b>记录归档</b>：证书发放记录归入主建老师账号下「其他学员」目录中对应学员的档案。
/// </description></item>
/// </list>
/// </summary>
public interface ICertificateAppService
{
    // ── 平台管理员：模板管理 ────────────────────────────────
    Task<List<CertificateTemplateDto>> GetTemplatesAsync();

    Task<CertificateTemplateDto> CreateTemplateAsync(CreateUpdateCertificateTemplateDto input);

    Task<CertificateTemplateDto> UpdateTemplateAsync(Guid id, CreateUpdateCertificateTemplateDto input);

    Task DeleteTemplateAsync(Guid id);

    // ── 平台管理员：审核发放 ────────────────────────────────
    Task<PagedResultDto<CertificateApplicationDto>> GetApplicationsAsync(GetCertificateApplicationsInput input);

    Task<CertificateDto> ApproveAsync(ApproveCertificateInput input);

    /// <summary>发证前准备：生成证书编号与防伪码（供前端合成证书图片）。</summary>
    Task<CertificatePrepareDto> PrepareAsync(Guid applicationId);

    Task RejectAsync(RejectCertificateInput input);

    // ── 学员 ────────────────────────────────────────────────
    Task<CertificateApplyEligibilityDto> GetApplyEligibilityAsync(Guid courseId);

    Task<CertificateApplicationDto> ApplyAsync(ApplyCertificateInput input);

    Task<List<CertificateApplicationDto>> GetMyApplicationsAsync();

    Task<List<CertificateDto>> GetMyCertificatesAsync();

    // ── 公开防伪核验 ─────────────────────────────────────────
    Task<CertificateVerifyDto> VerifyAsync(string code);
}
