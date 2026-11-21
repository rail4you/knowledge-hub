package com.knowledgehub.mobile.data.api

import com.knowledgehub.mobile.BuildConfig
import com.knowledgehub.mobile.data.SessionManager
import okhttp3.FormBody
import okhttp3.MediaType.Companion.toMediaType
import okhttp3.OkHttpClient
import okhttp3.Request
import okhttp3.RequestBody.Companion.toRequestBody
import okhttp3.logging.HttpLoggingInterceptor
import kotlinx.serialization.json.Json
import kotlinx.serialization.decodeFromString
import java.net.URLEncoder
import java.security.SecureRandom
import java.security.cert.X509Certificate
import javax.net.ssl.SSLContext
import javax.net.ssl.TrustManager
import javax.net.ssl.X509TrustManager

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

    private val logging = HttpLoggingInterceptor().apply {
        level = HttpLoggingInterceptor.Level.BODY
    }

    /**
     * 联调用：信任后端开发服务器的自签证书。
     * TODO(迁移到生产前必须移除)：改用正式 CA 证书或 certificate pinning。
     */
    private val trustAllManager = object : X509TrustManager {
        override fun checkClientTrusted(chain: Array<X509Certificate>, authType: String) {}
        override fun checkServerTrusted(chain: Array<X509Certificate>, authType: String) {}
        override fun getAcceptedIssuers(): Array<X509Certificate> = arrayOf()
    }

    private val sslContext: SSLContext = SSLContext.getInstance("TLS").apply {
        init(null, arrayOf<TrustManager>(trustAllManager), SecureRandom())
    }

    val client: OkHttpClient = OkHttpClient.Builder()
        .sslSocketFactory(sslContext.socketFactory, trustAllManager)
        .hostnameVerifier { _, _ -> true }
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
        .addInterceptor(logging)
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
            // token 失效（后端重启/过期）：通知 UI 跳回登录页
            if (response.code == 401) {
                com.knowledgehub.mobile.data.AuthEvents.notifyUnauthorized()
                throw ApiException("登录已过期，请重新登录", 401)
            }
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
}
