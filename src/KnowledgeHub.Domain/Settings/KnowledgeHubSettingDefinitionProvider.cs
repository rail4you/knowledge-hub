using Volo.Abp.Settings;

namespace KnowledgeHub.Settings;

public class KnowledgeHubSettingDefinitionProvider : SettingDefinitionProvider
{
    public override void Define(ISettingDefinitionContext context)
    {
        context.Add(
            new SettingDefinition(KnowledgeHubSettings.IsInstalled, "false", isVisibleToClients: false),
            new SettingDefinition(KnowledgeHubSettings.InstalledEdition, "Basic", isVisibleToClients: false),
            new SettingDefinition(KnowledgeHubSettings.LicenseKey, "", isVisibleToClients: false),

            // 站点品牌（全局唯一，host 超管在“品牌设置”页维护；公开可见，前端三端共用）
            new SettingDefinition(KnowledgeHubSettings.BrandingAppTitle, "易课通", isVisibleToClients: true),
            new SettingDefinition(KnowledgeHubSettings.BrandingAppSubtitle, "知识资源库", isVisibleToClients: true),
            new SettingDefinition(KnowledgeHubSettings.BrandingFooterText, "© 2026 易课通 · 知识资源库", isVisibleToClients: true),
            new SettingDefinition(KnowledgeHubSettings.BrandingLogoUrl, "", isVisibleToClients: true),

            // 邮件发送（SMTP）：租户级配置，密码加密存储，仅后端可读；
            // 未配置时回退到 appsettings 的 Smtp 节点 / 环境变量 Smtp__Password。
            new SettingDefinition(KnowledgeHubSettings.SmtpHost, "", isVisibleToClients: false),
            new SettingDefinition(KnowledgeHubSettings.SmtpPort, "465", isVisibleToClients: false),
            new SettingDefinition(KnowledgeHubSettings.SmtpUserName, "", isVisibleToClients: false),
            new SettingDefinition(KnowledgeHubSettings.SmtpPassword, "", isVisibleToClients: false, isEncrypted: true),
            new SettingDefinition(KnowledgeHubSettings.SmtpFromAddress, "", isVisibleToClients: false),
            new SettingDefinition(KnowledgeHubSettings.SmtpFromName, "", isVisibleToClients: false),

            // 存储配额监控（host 全局管理员）：默认配额 / 预警阈值 / 租户级配额，仅后端可读
            new SettingDefinition(KnowledgeHubSettings.DefaultStorageQuotaBytes, "53687091200", isVisibleToClients: false),
            new SettingDefinition(KnowledgeHubSettings.StorageQuotaBytes, "0", isVisibleToClients: false),
            new SettingDefinition(KnowledgeHubSettings.StorageWarnPercent, "80", isVisibleToClients: false),

            // 阿里云 OSS 对象存储：AK/SK 加密存储，仅后端可读；
            // 租户级配置，host 上下文写全局回退；未配置的字段回退 appsettings 的 Oss 节点 / 环境变量 Oss__*。
            new SettingDefinition(KnowledgeHubSettings.OssEndpoint, "", isVisibleToClients: false),
            new SettingDefinition(KnowledgeHubSettings.OssAccessKeyId, "", isVisibleToClients: false, isEncrypted: true),
            new SettingDefinition(KnowledgeHubSettings.OssAccessKeySecret, "", isVisibleToClients: false, isEncrypted: true),
            new SettingDefinition(KnowledgeHubSettings.OssBucketName, "", isVisibleToClients: false),
            new SettingDefinition(KnowledgeHubSettings.OssUploadPath, "", isVisibleToClients: false),
            new SettingDefinition(KnowledgeHubSettings.OssUrlPrefix, "", isVisibleToClients: false),

            // 智学分析：推荐三路权重（可由「推荐效果评估」一键应用，热生效；未设置时回退 appsettings）
            new SettingDefinition(KnowledgeHubSettings.RecWeightContent, "0.5", isVisibleToClients: false),
            new SettingDefinition(KnowledgeHubSettings.RecWeightCollab, "0.3", isVisibleToClients: false),
            new SettingDefinition(KnowledgeHubSettings.RecWeightFit, "0.2", isVisibleToClients: false),

            // AI 使用管理：Qwen Key 全局唯一、加密存储，仅后端可读；
            // 每日配额 JSON（角色 → 分组 → 次数），AI 使用管理页维护。
            new SettingDefinition(KnowledgeHubSettings.QwenApiKey, "", isVisibleToClients: false, isEncrypted: true),
            new SettingDefinition(KnowledgeHubSettings.AiQuotas, KnowledgeHubSettings.DefaultAiQuotas, isVisibleToClients: false)
        );
    }
}
