namespace KnowledgeHub.Certificates;

/// <summary>已发放证书的状态。</summary>
public enum CertificateStatus : byte
{
    /// <summary>有效。</summary>
    Active = 0,

    /// <summary>已撤销（核验时提示无效）。</summary>
    Revoked = 1
}
