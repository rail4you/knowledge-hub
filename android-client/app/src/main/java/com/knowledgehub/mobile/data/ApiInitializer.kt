package com.knowledgehub.mobile.data

import com.knowledgehub.mobile.data.api.ApiClient

/** 应用启动时把 SessionManager 注入网络层 */
object ApiInitializer {
    fun init(session: SessionManager) {
        ApiClient.init(session)
    }
}
