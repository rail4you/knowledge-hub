package com.knowledgehub.mobile.data.repository

import com.knowledgehub.mobile.data.api.ApiClient
import com.knowledgehub.mobile.data.api.ApiException
import com.knowledgehub.mobile.data.model.ExerciseDto
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.withContext
import kotlinx.serialization.json.JsonObject
import kotlinx.serialization.json.JsonPrimitive

/** 教师端 AI 能力：同步生成习题 + SSE 文本生成（备课 / 助手）。 */
class AiRepository {

    /** 同步生成并保存习题：POST /api/app/exercise/generate-by-aI */
    suspend fun generateExercises(
        courseId: String,
        type: Int,
        count: Int,
        difficulty: Int,
        topicHint: String?,
        customPrompt: String?
    ): List<ExerciseDto> = withContext(Dispatchers.IO) {
        val body = kotlinx.serialization.json.buildJsonObject {
            put("courseId", kotlinx.serialization.json.JsonPrimitive(courseId))
            put("type", kotlinx.serialization.json.JsonPrimitive(type))
            put("count", kotlinx.serialization.json.JsonPrimitive(count))
            put("difficulty", kotlinx.serialization.json.JsonPrimitive(difficulty))
            topicHint?.takeIf { it.isNotBlank() }?.let {
                put("topicHint", kotlinx.serialization.json.JsonPrimitive(it))
            }
            customPrompt?.takeIf { it.isNotBlank() }?.let {
                put("customPrompt", kotlinx.serialization.json.JsonPrimitive(it))
            }
        }.toString()
        val req = ApiClient.postJson("/api/app/exercise/generate-by-aI", body)
        ApiClient.execute(req) { ApiClient.json.decodeFromString<List<ExerciseDto>>(it) }
    }

    /** AI 备课：POST /api/learning/ai/generate-lesson-plan（SSE） */
    suspend fun generateLessonPlan(
        topic: String,
        subject: String?,
        grade: String?,
        duration: Int,
        customPrompt: String?
    ): String = withContext(Dispatchers.IO) {
        val body = kotlinx.serialization.json.buildJsonObject {
            put("topic", kotlinx.serialization.json.JsonPrimitive(topic))
            subject?.takeIf { it.isNotBlank() }?.let {
                put("subject", kotlinx.serialization.json.JsonPrimitive(it))
            }
            grade?.takeIf { it.isNotBlank() }?.let {
                put("grade", kotlinx.serialization.json.JsonPrimitive(it))
            }
            put("duration", kotlinx.serialization.json.JsonPrimitive(duration))
            customPrompt?.takeIf { it.isNotBlank() }?.let {
                put("customPrompt", kotlinx.serialization.json.JsonPrimitive(it))
            }
        }.toString()
        postSseText("/api/learning/ai/generate-lesson-plan", body)
    }

    /** AI 教学助手：POST /api/learning/ai/chat（SSE） */
    suspend fun chat(message: String): String = withContext(Dispatchers.IO) {
        val body = kotlinx.serialization.json.buildJsonObject {
            put("message", kotlinx.serialization.json.JsonPrimitive(message))
        }.toString()
        postSseText("/api/learning/ai/chat", body)
    }

    /** 读取 SSE 响应中的 content 片段并拼接为完整文本。 */
    private fun postSseText(path: String, jsonBody: String): String {
        val request = ApiClient.postJson(path, jsonBody)
        val response = ApiClient.client.newCall(request).execute()
        val responseBody = response.body
        if (!response.isSuccessful) {
            val text = responseBody?.string().orEmpty()
            response.close()
            if (response.code == 401) {
                com.knowledgehub.mobile.data.AuthEvents.notifyUnauthorized()
                throw ApiException("登录已过期，请重新登录", 401)
            }
            throw ApiException(ApiClient.friendlyError(text), response.code)
        }

        val result = StringBuilder()
        responseBody?.charStream()?.buffered()?.useLines { lines ->
            lines.forEach { line ->
                if (!line.startsWith("data:")) return@forEach
                val data = line.substringAfter("data:").trim()
                if (data.isBlank() || data == "[DONE]") return@forEach
                runCatching {
                    val obj = ApiClient.json.decodeFromString<JsonObject>(data)
                    val content = (obj["content"] as? JsonPrimitive)?.content.orEmpty()
                    val isError = (obj["isError"] as? JsonPrimitive)?.content?.toBooleanStrictOrNull() == true
                    // AI 后端可能以 SSE 事件返回错误（如 API Key 失效）。
                    // 直接展示在结果区，避免瞬时 Snackbar 一闪而过看不到原因。
                    if (isError && content.isNotBlank()) result.append("AI 返回错误：$content")
                    else result.append(content)
                }.onFailure { error ->
                    if (error is ApiException) throw error
                }
            }
        }
        if (result.isBlank()) throw ApiException("AI 未返回内容，请稍后重试", 500)
        return result.toString()
    }
}
