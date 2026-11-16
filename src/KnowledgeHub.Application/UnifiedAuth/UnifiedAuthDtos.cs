using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace KnowledgeHub.UnifiedAuth;

public class UnifiedTenantDto
{
    [JsonPropertyName("id")] public Guid Id { get; set; }
    [JsonPropertyName("code")] public string? Code { get; set; }
    [JsonPropertyName("name")] public string? Name { get; set; }
}

public class UnifiedPersonDto
{
    [JsonPropertyName("id")] public Guid Id { get; set; }
    [JsonPropertyName("name")] public string? Name { get; set; }
    [JsonPropertyName("phone_e164")] public string? PhoneE164 { get; set; }
    [JsonPropertyName("email")] public string? Email { get; set; }
}

public class UnifiedMembershipDto
{
    [JsonPropertyName("id")] public Guid Id { get; set; }
    [JsonPropertyName("member_no")] public string? MemberNo { get; set; }
    [JsonPropertyName("role")] public string? Role { get; set; }
    [JsonPropertyName("status")] public string? Status { get; set; }
    [JsonPropertyName("valid_from")] public DateTimeOffset? ValidFrom { get; set; }
    [JsonPropertyName("valid_until")] public DateTimeOffset? ValidUntil { get; set; }
}

public class UnifiedVerifyResponse
{
    [JsonPropertyName("allowed")] public bool Allowed { get; set; }
    [JsonPropertyName("reason")] public string? Reason { get; set; }
    [JsonPropertyName("must_change_password")] public bool MustChangePassword { get; set; }
    [JsonPropertyName("person")] public UnifiedPersonDto? Person { get; set; }
    [JsonPropertyName("tenant")] public UnifiedTenantDto? Tenant { get; set; }
    [JsonPropertyName("membership")] public UnifiedMembershipDto? Membership { get; set; }
    [JsonPropertyName("external_id")] public string? ExternalId { get; set; }
    [JsonPropertyName("external_login")] public string? ExternalLogin { get; set; }
    [JsonPropertyName("valid_until")] public DateTimeOffset? ValidUntil { get; set; }
}

public class UnifiedCreateMemberResponse
{
    [JsonPropertyName("person")] public UnifiedPersonDto? Person { get; set; }
    [JsonPropertyName("membership")] public UnifiedMembershipDto? Membership { get; set; }
    [JsonPropertyName("initial_password")] public string? InitialPassword { get; set; }
    [JsonPropertyName("created")] public bool Created { get; set; }
}

public class CreateUnifiedMemberRequest
{
    [JsonPropertyName("app_tenant_id")] public string? AppTenantId { get; set; }
    [JsonPropertyName("tenant")] public string? Tenant { get; set; }
    [JsonPropertyName("member_no")] public string MemberNo { get; set; } = string.Empty;
    [JsonPropertyName("name")] public string? Name { get; set; }
    [JsonPropertyName("phone")] public string? Phone { get; set; }
    [JsonPropertyName("email")] public string? Email { get; set; }
    [JsonPropertyName("role")] public string? Role { get; set; }
    [JsonPropertyName("password")] public string? Password { get; set; }
    [JsonPropertyName("external_login")] public string? ExternalLogin { get; set; }
    [JsonPropertyName("external_id")] public string? ExternalId { get; set; }
}

public class UnifiedErrorResponse
{
    [JsonPropertyName("error")] public string? Error { get; set; }
    [JsonPropertyName("message")] public string? Message { get; set; }
}

public sealed class UnifiedAuthException : Exception
{
    public int StatusCode { get; }

    public UnifiedAuthException(int statusCode, string message) : base(message)
    {
        StatusCode = statusCode;
    }
}

public class UnifiedTenantConflictDto
{
    [JsonPropertyName("code")] public string? Code { get; set; }
    [JsonPropertyName("message")] public string? Message { get; set; }
    [JsonPropertyName("candidates")] public List<UnifiedTenantDto>? Candidates { get; set; }
}

public class UnifiedLookupTenantResponse
{
    [JsonPropertyName("matched")] public UnifiedTenantDto? Matched { get; set; }
    [JsonPropertyName("enabled")] public bool Enabled { get; set; }
    [JsonPropertyName("available")] public bool Available { get; set; }
    [JsonPropertyName("conflict")] public UnifiedTenantConflictDto? Conflict { get; set; }
    [JsonPropertyName("candidates")] public List<UnifiedTenantDto>? Candidates { get; set; }
}
