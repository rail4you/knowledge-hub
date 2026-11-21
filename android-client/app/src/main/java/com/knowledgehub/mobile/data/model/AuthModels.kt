package com.knowledgehub.mobile.data.model

import kotlinx.serialization.SerialName
import kotlinx.serialization.Serializable

/** /connect/token 的响应 */
@Serializable
data class TokenResponse(
    @SerialName("access_token") val accessToken: String,
    @SerialName("refresh_token") val refreshToken: String? = null,
    @SerialName("expires_in") val expiresIn: Long = 0,
    @SerialName("token_type") val tokenType: String = "Bearer",
    @SerialName("error") val error: String? = null,
    @SerialName("error_description") val errorDescription: String? = null
)

/** /api/abp/application-configuration 中我们关心的部分 */
@Serializable
data class AppConfiguration(
    val currentUser: CurrentUser = CurrentUser(),
    val currentTenant: CurrentTenant = CurrentTenant()
)

@Serializable
data class CurrentTenant(
    val id: String? = null,
    val name: String? = null,
    val isAvailable: Boolean = false
)

@Serializable
data class CurrentUser(
    val isAuthenticated: Boolean = false,
    val id: String? = null,
    val userName: String? = null,
    val name: String? = null,
    val surname: String? = null,
    val email: String? = null,
    val roles: List<String> = emptyList()
)

/** ABP 标准分页响应 */
@Serializable
data class PagedResult<T>(
    val items: List<T> = emptyList(),
    val totalCount: Int = 0
)

/** 公开租户项：GET /api/public/tenants */
@Serializable
data class TenantItem(
    val id: String? = null,
    val name: String? = null,
    val schoolName: String? = null,
    val tenantName: String? = null,
    val isPublicSchool: Boolean = false
)

/** GET /api/app/user/my-profile */
@Serializable
data class MyProfileDto(
    val id: String = "",
    val userName: String = "",
    val name: String = "",
    val email: String? = null,
    val phoneNumber: String? = null,
    val emailConfirmed: Boolean = false,
    val phoneNumberConfirmed: Boolean = false,
    val isPhoneVerified: Boolean = false,
    val realName: String? = null,
    val idCardMasked: String? = null,
    val isRealNameVerified: Boolean = false,
    val realNameVerifiedAt: String? = null
)

/** PUT /api/app/user/my-profile */
@Serializable
data class UpdateMyProfileDto(
    val name: String,
    val email: String? = null,
    val phoneNumber: String? = null
)

/** POST /api/account/my-profile/change-password */
@Serializable
data class ChangePasswordInput(
    val currentPassword: String,
    val newPassword: String
)
