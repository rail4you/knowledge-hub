package com.knowledgehub.mobile.data.api

import android.annotation.SuppressLint
import com.knowledgehub.mobile.BuildConfig
import java.security.KeyStore
import java.security.MessageDigest
import java.security.cert.CertificateException
import java.security.cert.X509Certificate
import javax.net.ssl.TrustManager
import javax.net.ssl.TrustManagerFactory
import javax.net.ssl.X509TrustManager

/**
 * 统一的 TLS 证书策略。
 *
 * - iketong / release：只使用系统 CA，默认主机名校验，不信任任何自签证书。
 * - local 联调：系统 CA 之外额外信任【一枚固定指纹】的开发证书，主机名仅允许
 *   10.0.2.2 / localhost / 127.0.0.1。不再信任所有证书，也不接受任意主机。
 */
object SecurityConfig {

    private val devPin: String = BuildConfig.DEV_CERT_SHA256.trim().lowercase()
    private val allowedDevHosts: Set<String> = BuildConfig.ALLOWED_DEV_HOSTS
        .split(",")
        .map { it.trim().lowercase() }
        .filter { it.isNotBlank() }
        .toSet()

    val usesPinnedDevCert: Boolean
        get() = BuildConfig.USE_PINNED_DEV_CERT && devPin.isNotBlank()

    fun sha256(cert: X509Certificate): String =
        MessageDigest.getInstance("SHA-256")
            .digest(cert.encoded)
            .joinToString("") { "%02x".format(it) }

    fun isPinnedDevCert(cert: X509Certificate?): Boolean =
        cert != null && usesPinnedDevCert && sha256(cert) == devPin

    fun isAllowedDevHost(host: String?): Boolean =
        host != null && usesPinnedDevCert && host.lowercase() in allowedDevHosts

    private fun systemTrustManagers(): Array<X509TrustManager> {
        val factory = TrustManagerFactory.getInstance(TrustManagerFactory.getDefaultAlgorithm())
        factory.init(null as KeyStore?)
        return factory.trustManagers.filterIsInstance<X509TrustManager>().toTypedArray()
            .takeIf { it.isNotEmpty() }
            ?: throw IllegalStateException("系统未提供 X509TrustManager")
    }

    /**
     * 系统 CA 正常校验；local 版本仅在系统校验失败时，接受指纹匹配的开发证书。
     */
    @SuppressLint("CustomX509TrustManager")
    fun trustManagers(): Array<X509TrustManager> {
        val defaults = systemTrustManagers()
        if (!usesPinnedDevCert) return defaults

        return arrayOf(object : X509TrustManager {
            override fun checkClientTrusted(chain: Array<X509Certificate>, authType: String) {
                defaults.first().checkClientTrusted(chain, authType)
            }

            @SuppressLint("TrustAllX509TrustManager")
            override fun checkServerTrusted(chain: Array<X509Certificate>, authType: String) {
                try {
                    defaults.first().checkServerTrusted(chain, authType)
                } catch (error: CertificateException) {
                    // 只有指纹完全匹配本地开发证书才放行，其他证书一律拒绝
                    if (chain.none { isPinnedDevCert(it) }) throw error
                }
            }

            override fun getAcceptedIssuers(): Array<X509Certificate> =
                defaults.flatMap { it.acceptedIssuers.toList() }.toTypedArray()
        })
    }
}
