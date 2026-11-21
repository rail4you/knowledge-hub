package com.knowledgehub.mobile.data.repository

import com.knowledgehub.mobile.BuildConfig
import com.knowledgehub.mobile.data.api.ApiClient
import com.knowledgehub.mobile.data.api.ApiException
import com.knowledgehub.mobile.data.model.AppConfiguration
import com.knowledgehub.mobile.data.model.ChangePasswordInput
import com.knowledgehub.mobile.data.model.MyProfileDto
import com.knowledgehub.mobile.data.model.TenantItem
import com.knowledgehub.mobile.data.model.TokenResponse
import com.knowledgehub.mobile.data.model.UpdateMyProfileDto
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.withContext
import kotlinx.serialization.decodeFromString
import kotlinx.serialization.encodeToString

class AuthRepository {

    /** 拉取公开租户列表。宿主登录时租户可留空。 */
    suspend fun getTenants(): List<TenantItem> = withContext(Dispatchers.IO) {
        val request = ApiClient.get("/api/public/tenants")
        ApiClient.execute(request) { ApiClient.json.decodeFromString<List<TenantItem>>(it) }
    }

    /**
     * 密码模式登录。tenantName 非空时通过 __tenant 头传递。
     * 注意：/connect/token 必须带租户头，因此这里单独构造带头的请求。
     */
    suspend fun login(userName: String, password: String, tenantName: String?): TokenResponse =
        withContext(Dispatchers.IO) {
            val form = mapOf(
                "grant_type" to "password",
                "username" to userName,
                "password" to password,
                "scope" to BuildConfig.API_SCOPE,
                "client_id" to BuildConfig.CLIENT_ID
            )
            val body = okhttp3.FormBody.Builder().apply {
                form.forEach { (k, v) -> add(k, v) }
            }.build()
            val builder = okhttp3.Request.Builder()
                .url(ApiClient.baseUrl() + "/connect/token")
                .header("Content-Type", "application/x-www-form-urlencoded")
                .post(body)
            if (!tenantName.isNullOrBlank()) builder.header("__tenant", tenantName)

            val response = ApiClient.client.newCall(builder.build()).execute()
            val bodyStr = response.body?.string().orEmpty()
            if (!response.isSuccessful) {
                throw ApiException(ApiClient.friendlyError(bodyStr), response.code)
            }
            ApiClient.json.decodeFromString<TokenResponse>(bodyStr)
        }

    /** 用 token 读取当前用户与角色 */
    suspend fun getCurrentUser(): AppConfiguration = withContext(Dispatchers.IO) {
        val request = ApiClient.get("/api/abp/application-configuration")
        ApiClient.execute(request) { ApiClient.json.decodeFromString<AppConfiguration>(it) }
    }

    /** 设置页：读取个人资料 */
    suspend fun getMyProfile(): MyProfileDto = withContext(Dispatchers.IO) {
        val request = ApiClient.get("/api/app/user/my-profile")
        ApiClient.execute(request) { ApiClient.json.decodeFromString<MyProfileDto>(it) }
    }

    /** 设置页：修改姓名、邮箱、手机号（手机号为空表示解绑） */
    suspend fun updateMyProfile(name: String, email: String?, phoneNumber: String?) =
        withContext(Dispatchers.IO) {
            val body = ApiClient.json.encodeToString(
                UpdateMyProfileDto(
                    name = name,
                    email = email?.takeIf { it.isNotBlank() },
                    // Web 端口径：手机号传空字符串表示解绑
                    phoneNumber = phoneNumber.orEmpty()
                )
            )
            val request = ApiClient.putJson("/api/app/user/my-profile", body)
            ApiClient.execute(request) { it }
        }

    /** 设置页：修改密码（统一账户服务入口） */
    suspend fun changePassword(currentPassword: String, newPassword: String) =
        withContext(Dispatchers.IO) {
            val body = ApiClient.json.encodeToString(
                ChangePasswordInput(currentPassword, newPassword)
            )
            val request = ApiClient.postJson("/api/account/my-profile/change-password", body)
            ApiClient.execute(request) { it }
        }
}
