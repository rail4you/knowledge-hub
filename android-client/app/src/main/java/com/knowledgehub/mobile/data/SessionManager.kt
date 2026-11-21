package com.knowledgehub.mobile.data

import android.content.Context
import androidx.datastore.core.DataStore
import androidx.datastore.preferences.core.Preferences
import androidx.datastore.preferences.core.edit
import androidx.datastore.preferences.core.stringPreferencesKey
import androidx.datastore.preferences.preferencesDataStore
import com.knowledgehub.mobile.data.model.CurrentUser
import kotlinx.coroutines.flow.Flow
import kotlinx.coroutines.flow.first
import kotlinx.coroutines.flow.map

private val Context.sessionDataStore: DataStore<Preferences> by preferencesDataStore(name = "kh_session")

/**
 * 会话管理：持久化 access_token / refresh_token / 租户 / 当前用户。
 * ABP OpenIddict 的 access_token 放在Bearer头；refresh_token 用于续期。
 */
class SessionManager(private val context: Context) {

    private object Keys {
        val ACCESS_TOKEN = stringPreferencesKey("access_token")
        val REFRESH_TOKEN = stringPreferencesKey("refresh_token")
        val TENANT = stringPreferencesKey("tenant")
        val USER_NAME = stringPreferencesKey("user_name")
        val USER_DISPLAY = stringPreferencesKey("user_display")
        val ROLES = stringPreferencesKey("roles")
    }

    // 内存镜像，供 OkHttp 拦截器同步读取
    @Volatile var accessToken: String? = null
        private set
    @Volatile var refreshToken: String? = null
        private set
    @Volatile var tenant: String? = null
        private set
    @Volatile var currentUser: CurrentUser? = null
        private set

    val isLoggedInFlow: Flow<Boolean> = context.sessionDataStore.data.map { prefs ->
        !prefs[Keys.ACCESS_TOKEN].isNullOrBlank()
    }

    /** 从磁盘恢复到内存，App 启动时调用 */
    suspend fun restore() {
        val prefs = context.sessionDataStore.data.first()
        accessToken = prefs[Keys.ACCESS_TOKEN]
        refreshToken = prefs[Keys.REFRESH_TOKEN]
        tenant = prefs[Keys.TENANT]
        val roles = prefs[Keys.ROLES]?.split(",")?.filter { it.isNotBlank() } ?: emptyList()
        currentUser = prefs[Keys.USER_NAME]?.let {
            CurrentUser(
                isAuthenticated = true,
                userName = it,
                name = prefs[Keys.USER_DISPLAY],
                roles = roles
            )
        }
    }

    suspend fun saveTokens(access: String, refresh: String?, tenantName: String?) {
        accessToken = access
        refreshToken = refresh
        tenantName?.let { tenant = it }
        context.sessionDataStore.edit { prefs ->
            prefs[Keys.ACCESS_TOKEN] = access
            refresh?.let { prefs[Keys.REFRESH_TOKEN] = it }
            tenantName?.let { prefs[Keys.TENANT] = it }
        }
    }

    suspend fun updateAccessToken(access: String, refresh: String? = null) {
        accessToken = access
        refresh?.let { refreshToken = it }
        context.sessionDataStore.edit { prefs ->
            prefs[Keys.ACCESS_TOKEN] = access
            refresh?.let { prefs[Keys.REFRESH_TOKEN] = it }
        }
    }

    suspend fun saveUser(user: CurrentUser) {
        currentUser = user
        context.sessionDataStore.edit { prefs ->
            prefs[Keys.USER_NAME] = user.userName ?: ""
            prefs[Keys.USER_DISPLAY] = listOfNotNull(user.surname, user.name).joinToString("")
            prefs[Keys.ROLES] = user.roles.joinToString(",")
        }
    }

    suspend fun clear() {
        accessToken = null
        refreshToken = null
        tenant = null
        currentUser = null
        context.sessionDataStore.edit { it.clear() }
    }

    /** 角色判定：与管理端 admin-roles.ts 口径一致 */
    fun isTeacher(): Boolean {
        val roles = currentUser?.roles ?: emptyList()
        return roles.any { it in setOf("Teacher", "SchoolAdmin", "LeagueAdmin", "EnterpriseUser", "admin") }
    }

    fun isStudent(): Boolean {
        val roles = currentUser?.roles ?: emptyList()
        return roles.contains("Student") || roles.isEmpty()
    }
}
