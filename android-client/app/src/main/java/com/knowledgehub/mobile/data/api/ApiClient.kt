package com.knowledgehub.mobile.data.api

import com.knowledgehub.mobile.BuildConfig
import com.knowledgehub.mobile.data.SessionManager
import com.knowledgehub.mobile.data.model.TokenResponse
import kotlinx.coroutines.runBlocking
import kotlinx.serialization.json.Json
import kotlinx.serialization.decodeFromString
import okhttp3.FormBody
import okhttp3.MediaType.Companion.toMediaType
import okhttp3.OkHttpClient
import okhttp3.Request
import okhttp3.RequestBody.Companion.toRequestBody
import okhttp3.logging.HttpLoggingInterceptor
import java.net.URLEncoder
import java.security.SecureRandom
import javax.net.ssl.HostnameVerifier
import javax.net.ssl.HttpsURLConnection
import javax.net.ssl.SSLContext

/** 统一 API 异常 */
class ApiException(message: String, val code: Int = 0) : Exception(message)

object ApiClient {

    val json = Json {
        ignoreUnknownKeys = true
        isLenient = true
        encodeDefaults = true
        coerceInputValues = true
    }

    lateinit var session: SessionManager
        private set

    fun init(session: SessionManager) {
        this.session = session
    }

    // 不再记录 Body：避免 token、密码、个人资料进入 logcat
    private val logging = HttpLoggingInterceptor().apply {
        level = if (BuildConfig.DEBUG) HttpLoggingInterceptor.Level.BASIC
        else HttpLoggingInterceptor.Level.NONE
    }

    private val trustManagers by lazy { SecurityConfig.trustManagers() }

    private val sslSocketFactory by lazy {
        SSLContext.getInstance("TLS").apply {
            init(null, trustManagers, SecureRandom())
        }.socketFactory
    }

    private val hostnameVerifier = HostnameVerifier { hostname, sslSession ->
        if (SecurityConfig.isAllowedDevHost(hostname)) {
            // 仅 local 版本允许模拟器地址；证书仍由固定指纹的 TrustManager 校验
            true
        } else {
            HttpsURLConnection.getDefaultHostnameVerifier().verify(hostname, sslSession)
        }
    }

    private fun baseBuilder(): OkHttpClient.Builder = OkHttpClient.Builder()
        .sslSocketFactory(sslSocketFactory, trustManagers.first())
        .hostnameVerifier(hostnameVerifier)
        .addInterceptor(logging)

    /** 专用于 /connect/token：不带 authenticator，避免刷新失败时递归。 */
    private val tokenClient: OkHttpClient by lazy { baseBuilder().build() }

    private val refreshLock = Any()

    /**
     * 401 时用 refresh_token 续期并保存新令牌。
     * synchronized + 旧 access_token 比对，保证并发 401 只刷新一次（兼容 refresh token 轮换）。
     */
    private fun refreshAccessToken(staleAccessToken: String?): String? {
        val refreshToken = session.refreshToken?.takeIf { it.isNotBlank() } ?: return null
        val tenantName = session.tenant
        synchronized(refreshLock) {
            val current = session.accessToken
            if (!current.isNullOrBlank() && current != staleAccessToken) return current

            val form = FormBody.Builder()
                .add("grant_type", "refresh_token")
                .add("refresh_token", refreshToken)
                .add("client_id", BuildConfig.CLIENT_ID)
                .add("scope", BuildConfig.API_SCOPE)
                .build()
            val builder = Request.Builder()
                .url(baseUrl() + "/connect/token")
                .post(form)
            tenantName?.takeIf { it.isNotBlank() }?.let { builder.header("__tenant", it) }

            val response = tokenClient.newCall(builder.build()).execute()
            val body = response.body?.string().orEmpty()
            if (!response.isSuccessful) return null

            val token = runCatching {
                json.decodeFromString<TokenResponse>(body)
            }.getOrNull() ?: return null
            if (token.accessToken.isBlank()) return null

            runBlocking {
                session.updateAccessToken(
                    access = token.accessToken,
                    refresh = token.refreshToken?.takeIf { it.isNotBlank() } ?: refreshToken
                )
            }
            return token.accessToken
        }
    }

    val client: OkHttpClient = baseBuilder()
        .addInterceptor { chain ->
            val original = chain.request()
            val builder = original.newBuilder()
            // Bearer token
            session.accessToken?.takeIf { it.isNotBlank() }?.let {
                builder.header("Authorization", "Bearer $it")
            }
            // ABP 多租户头
            session.tenant?.takeIf { it.isNotBlank() }?.let {
                builder.header("__tenant", it)
            }
            chain.proceed(builder.build())
        }
        .authenticator { _, response ->
            if (response.code != 401) return@authenticator null
            // 刷新接口本身失败：直接退出，避免死循环
            if (response.request.url.encodedPath.endsWith("/connect/token")) {
                return@authenticator null
            }
            val stale = response.request.header("Authorization")
                ?.removePrefix("Bearer ")?.trim()
            val newToken = refreshAccessToken(stale)
            if (newToken.isNullOrBlank()) {
                com.knowledgehub.mobile.data.AuthEvents.notifyUnauthorized()
                null
            } else {
                response.request.newBuilder()
                    .header("Authorization", "Bearer $newToken")
                    .build()
            }
        }
        .build()

    fun baseUrl(): String = BuildConfig.API_BASE_URL.trimEnd('/')

    /**
     * 给 WebView / 媒体控件等无法设置 Header 的场景追加 access_token。
     * 后端 GrantAllPoliciesMiddleware 支持 query 形式令牌（Web 端同口径）。
     */
    fun withAccessToken(url: String): String {
        val token = session.accessToken
        if (token.isNullOrBlank()) return url
        val separator = if (url.contains("?")) "&" else "?"
        return "$url${separator}access_token=${URLEncoder.encode(token, "UTF-8")}"
    }

    /** 资源文件预览/下载 URL。endpoint 如 preview、preview-pdf、download、thumbnail?w=960。 */
    fun resourceFileUrl(resourceId: String, endpoint: String = "preview"): String =
        withAccessToken("${baseUrl()}/api/resource-file/$resourceId/$endpoint")

    /**
     * 从错误响应里提取友好提示，避免把整段 JSON 直接抛给用户。
     * 支持 OAuth({error,error_description}) 与 ABP({error:{message,details}}) 两种结构。
     */
    fun friendlyError(body: String): String {
        if (body.isBlank()) return "请求失败"
        return runCatching {
            val root = json.decodeFromString<kotlinx.serialization.json.JsonObject>(body)
            fun text(v: kotlinx.serialization.json.JsonElement?) =
                v?.takeIf { it is kotlinx.serialization.json.JsonPrimitive }
                    ?.toString()?.trim('"')?.takeIf { it.isNotBlank() }

            // OAuth: {"error":"invalid_grant","error_description":"..."}
            val oauthDesc = text(root["error_description"])
            if (oauthDesc != null) return oauthDesc

            // ABP: {"error":{"message":"...","details":"..."}} 或顶层 {"message":"..."}
            val errObj = root["error"] as? kotlinx.serialization.json.JsonObject
            val message = text(errObj?.get("message")) ?: text(root["message"])
            val details = text(errObj?.get("details")) ?: text(root["details"])
            when {
                !details.isNullOrBlank() -> {
                    // details 形如 "验证过程中检测到以下错误。\n - The value 'x' is not valid."
                    val lines = details.replace("\\n", "\n").lineSequence()
                        .map { it.trim().removePrefix("-").trim() }
                        .filter { it.isNotBlank() }
                        .toList()
                    val firstDetail = lines.drop(1).firstOrNull() ?: lines.firstOrNull()
                    if (firstDetail != null && !message.isNullOrBlank()) "$message：$firstDetail"
                    else firstDetail ?: message!!
                }
                !message.isNullOrBlank() -> message
                else -> null
            }
        }.getOrNull() ?: "请求失败（${body.take(80)}）"
    }

    /** 执行请求并返回解析后的对象；失败抛 ApiException */
    fun <T> execute(request: Request, parser: (String) -> T): T {
        val response = client.newCall(request).execute()
        val body = response.body?.string().orEmpty()
        if (!response.isSuccessful) {
            // 401 的续期与跳登录统一由 authenticator 处理，此处不重复发事件
            throw ApiException(friendlyError(body), response.code)
        }
        return parser(body)
    }

    fun get(path: String): Request =
        Request.Builder().url(baseUrl() + path).get().build()

    fun postForm(path: String, form: Map<String, String>): Request {
        val body = FormBody.Builder().apply {
            form.forEach { (k, v) -> add(k, v) }
        }.build()
        return Request.Builder().url(baseUrl() + path).post(body).build()
    }

    fun postJson(path: String, jsonBody: String): Request {
        val body = jsonBody.toRequestBody("application/json".toMediaType())
        return Request.Builder().url(baseUrl() + path).post(body).build()
    }

    fun putJson(path: String, jsonBody: String): Request {
        val body = jsonBody.toRequestBody("application/json".toMediaType())
        return Request.Builder().url(baseUrl() + path).put(body).build()
    }

    fun delete(path: String): Request =
        Request.Builder().url(baseUrl() + path).delete().build()
}
