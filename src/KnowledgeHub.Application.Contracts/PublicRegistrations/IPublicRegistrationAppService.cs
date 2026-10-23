using System.Threading.Tasks;
using KnowledgeHub.PublicRegistrations.Dtos;
using KnowledgeHub.Schools.Dtos;

namespace KnowledgeHub.PublicRegistrations;

/// <summary>
/// 公开注册 AppService。
///
/// <para>
/// 需求「租户有公开的『社会学校』，该租户的学生可以自由注册」的入口：
/// 所有公开注册用户统一进入平台唯一的社会学校租户，并自动获得该租户的 Student 角色。
/// </para>
/// </summary>
public interface IPublicRegistrationAppService
{
    /// <summary>公开注册（匿名）。</summary>
    Task<PublicRegistrationResultDto> RegisterAsync(PublicRegistrationDto input);

    /// <summary>公开：获取社会学校信息（注册页展示；未配置时 Configured=false）。</summary>
    Task<PublicSchoolDto> GetSocialSchoolAsync();
}
