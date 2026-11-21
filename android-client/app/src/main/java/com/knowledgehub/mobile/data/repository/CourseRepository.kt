package com.knowledgehub.mobile.data.repository

import com.knowledgehub.mobile.data.api.ApiClient
import com.knowledgehub.mobile.data.api.ApiException
import com.knowledgehub.mobile.data.model.*
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.withContext
import kotlinx.serialization.decodeFromString
import kotlinx.serialization.encodeToString

class CourseRepository {

    /** 学生：已发布的课程（发现课程） */
    suspend fun getPublishedCourses(filter: String = ""): List<CourseDto> = withContext(Dispatchers.IO) {
        val body = buildMap {
            put("maxResultCount", "50")
            put("skipCount", "0")
            if (filter.isNotBlank()) put("filter", filter)
        }
        val req = ApiClient.postJson("/api/app/course/get-published", ApiClient.json.encodeToString(body))
        ApiClient.execute(req) { ApiClient.json.decodeFromString<PagedResult<CourseDto>>(it).items }
    }

    /** 学生：我选的课程 */
    suspend fun getMyCourses(): List<StudentCourseListItem> = withContext(Dispatchers.IO) {
        val req = ApiClient.get("/api/app/learning/my-courses")
        ApiClient.execute(req) { ApiClient.json.decodeFromString<List<StudentCourseListItem>>(it) }
    }

    /** 课程详情（含章节）。ABP 约定路由：/{id}/detail（id 在路径里，非 query） */
    suspend fun getDetail(courseId: String): CourseDetailDto = withContext(Dispatchers.IO) {
        val req = ApiClient.get("/api/app/course/$courseId/detail")
        ApiClient.execute(req) { ApiClient.json.decodeFromString<CourseDetailDto>(it) }
    }

    /** 章节树 */
    suspend fun getChapterTree(courseId: String): List<ChapterDto> = withContext(Dispatchers.IO) {
        val req = ApiClient.get("/api/app/chapter/chapter-tree/$courseId")
        ApiClient.execute(req) { ApiClient.json.decodeFromString<List<ChapterDto>>(it) }
    }

    /** 选课 */
    suspend fun enroll(courseId: String) = withContext(Dispatchers.IO) {
        val req = ApiClient.postJson(
            "/api/app/course/enroll",
            ApiClient.json.encodeToString(mapOf("courseId" to courseId))
        )
        ApiClient.execute(req) { it }
    }

    /** 学习仪表盘 */
    suspend fun getDashboard(): LearningDashboard = withContext(Dispatchers.IO) {
        val req = ApiClient.get("/api/app/learning/dashboard")
        ApiClient.execute(req) { ApiClient.json.decodeFromString<LearningDashboard>(it) }
    }

    /** 某课程学习进度 */
    suspend fun getProgress(courseId: String): LearningProgress? = withContext(Dispatchers.IO) {
        val req = ApiClient.get("/api/app/learning/progress/$courseId")
        runCatching {
            ApiClient.execute(req) { ApiClient.json.decodeFromString<LearningProgress>(it) }
        }.getOrNull()
    }

    /** 记录资源学习行为（仅传 additionalMinutes，不改进度；进度由后端按习题比例算） */
    suspend fun recordResourceStudy(
        courseId: String,
        chapterId: String? = null,
        resourceId: String? = null,
        additionalMinutes: Int = 1
    ) = withContext(Dispatchers.IO) {
        val body = kotlinx.serialization.json.buildJsonObject {
            put("courseId", kotlinx.serialization.json.JsonPrimitive(courseId))
            put("progress", kotlinx.serialization.json.JsonPrimitive(0))
            chapterId?.let { put("chapterId", kotlinx.serialization.json.JsonPrimitive(it)) }
            resourceId?.let { put("resourceId", kotlinx.serialization.json.JsonPrimitive(it)) }
            put("additionalMinutes", kotlinx.serialization.json.JsonPrimitive(additionalMinutes))
        }.toString()
        val req = ApiClient.postJson(
            "/api/app/learning/record-progress",
            body
        )
        runCatching { ApiClient.execute(req) { it } }.getOrDefault(Unit)
    }

    /** 习题进度：已提交数/总数（后端口径） */
    suspend fun getExerciseProgress(courseId: String): CourseExerciseProgress = withContext(Dispatchers.IO) {
        val req = ApiClient.get("/api/app/learning/my-exercise-progress/$courseId")
        ApiClient.execute(req) { ApiClient.json.decodeFromString<CourseExerciseProgress>(it) }
    }

    /** 章节习题 */
    suspend fun getExercises(chapterId: String): List<ExerciseDto> = withContext(Dispatchers.IO) {
        val req = ApiClient.get("/api/app/exercise/by-chapter/$chapterId")
        ApiClient.execute(req) { ApiClient.json.decodeFromString<List<ExerciseDto>>(it) }
    }

    /** 资源文件元数据：扩展名为空的旧资源用它回填 */
    suspend fun getResourceInfo(resourceId: String): ResourceFileInfoDto? = withContext(Dispatchers.IO) {
        val req = ApiClient.get("/api/app/resource/$resourceId")
        runCatching {
            ApiClient.execute(req) { ApiClient.json.decodeFromString<ResourceFileInfoDto>(it) }
        }.getOrNull()
    }

    /** 文本类资源：直接取预览内容（仅文本类型调用） */
    suspend fun getResourceText(resourceId: String): String = withContext(Dispatchers.IO) {
        val req = ApiClient.get("/api/resource-file/$resourceId/preview")
        ApiClient.execute(req) { body ->
            if (body.length > 2_000_000) {
                body.take(2_000_000) + "\n\n（内容过长，仅显示前 200 万字符）"
            } else body
        }
    }

    /** Office 转换状态：ready=true 才能加载 /preview-pdf */
    suspend fun getPreviewPdfReady(resourceId: String): Boolean? = withContext(Dispatchers.IO) {
        val req = ApiClient.get("/api/resource-file/$resourceId/preview-pdf-info")
        runCatching {
            ApiClient.execute(req) { body ->
                val obj = ApiClient.json.decodeFromString<kotlinx.serialization.json.JsonObject>(body)
                obj["ready"]?.toString()?.toBooleanStrictOrNull() == true
            }
        }.getOrNull()
    }

    /** 下载 PDF/Office 转换后的 PDF 字节，供客户端原生逐页渲染 */
    suspend fun getPreviewBytes(resourceId: String, endpoint: String): ByteArray = withContext(Dispatchers.IO) {
        val maxSize = 25 * 1024 * 1024
        val req = ApiClient.get("/api/resource-file/$resourceId/$endpoint")
        val response = ApiClient.client.newCall(req).execute()
        val body = response.body
        if (!response.isSuccessful) {
            val text = body?.string().orEmpty()
            if (response.code == 401) {
                com.knowledgehub.mobile.data.AuthEvents.notifyUnauthorized()
                throw ApiException("登录已过期，请重新登录", 401)
            }
            throw ApiException(ApiClient.friendlyError(text), response.code)
        }
        val length = body?.contentLength() ?: -1
        if (length > maxSize) {
            response.close()
            throw ApiException("文件较大，暂不支持在线预览，请下载后查看", 413)
        }
        val bytes = body?.bytes() ?: ByteArray(0)
        if (bytes.size > maxSize) throw ApiException("文件较大，暂不支持在线预览，请下载后查看", 413)
        bytes
    }

    /** 章节提交记录 */
    suspend fun getExerciseRecords(courseId: String, chapterId: String): List<ExerciseRecordDto> =
        withContext(Dispatchers.IO) {
            val req = ApiClient.get(
                "/api/app/student-exercise-record/records-by-chapter" +
                    "?courseId=$courseId&chapterId=$chapterId&skipCount=0&maxResultCount=100"
            )
            runCatching {
                ApiClient.execute(req) {
                    ApiClient.json.decodeFromString<PagedResult<ExerciseRecordDto>>(it).items
                }
            }.getOrDefault(emptyList())
        }

    /** 课程全部提交记录 */
    suspend fun getCourseRecords(courseId: String): List<ExerciseRecordDto> = withContext(Dispatchers.IO) {
        val req = ApiClient.get(
            "/api/app/student-exercise-record/records-by-course" +
                "?courseId=$courseId&skipCount=0&maxResultCount=100"
        )
        runCatching {
            ApiClient.execute(req) {
                ApiClient.json.decodeFromString<PagedResult<ExerciseRecordDto>>(it).items
            }
        }.getOrDefault(emptyList())
    }

    /** 提交习题答案 */
    suspend fun submitAnswer(
        courseId: String,
        chapterId: String,
        exerciseId: String,
        studentAnswer: String,
        timeSpentMs: Long = 0
    ) = withContext(Dispatchers.IO) {
        val body = kotlinx.serialization.json.buildJsonObject {
            put("courseId", kotlinx.serialization.json.JsonPrimitive(courseId))
            put("chapterId", kotlinx.serialization.json.JsonPrimitive(chapterId))
            put("exerciseId", kotlinx.serialization.json.JsonPrimitive(exerciseId))
            put("studentAnswer", kotlinx.serialization.json.JsonPrimitive(studentAnswer))
            put("timeSpentTicks", kotlinx.serialization.json.JsonPrimitive(timeSpentMs * 10000))
        }.toString()
        val req = ApiClient.postJson("/api/app/student-exercise-record/save-or-update-record", body)
        ApiClient.execute(req) { it }
    }

    /** 教师：我教的课程 */
    suspend fun getMyTeachingCourses(): List<CourseDto> = withContext(Dispatchers.IO) {
        val body = mapOf(
            "maxResultCount" to "50",
            "skipCount" to "0",
            "onlyMine" to "true"
        )
        val req = ApiClient.postJson("/api/app/course/get-by-filter", ApiClient.json.encodeToString(body))
        ApiClient.execute(req) { ApiClient.json.decodeFromString<PagedResult<CourseDto>>(it).items }
    }

    /** 教师：课程学习概览 */
    suspend fun getCourseOverview(courseId: String): CourseLearningOverview? = withContext(Dispatchers.IO) {
        val req = ApiClient.postJson(
            "/api/app/student-exercise-record/course-learning-overview",
            ApiClient.json.encodeToString(mapOf("courseId" to courseId))
        )
        runCatching {
            ApiClient.execute(req) { ApiClient.json.decodeFromString<CourseLearningOverview>(it) }
        }.getOrNull()
    }

    /** 教师：学生列表统计 */
    suspend fun getLearningStatistics(courseId: String): List<StudentLearningStatistics> = withContext(Dispatchers.IO) {
        val req = ApiClient.postJson(
            "/api/app/student-exercise-record/learning-statistics",
            ApiClient.json.encodeToString(
                mapOf("courseId" to courseId, "maxResultCount" to "100", "skipCount" to "0")
            )
        )
        runCatching {
            ApiClient.execute(req) { ApiClient.json.decodeFromString<PagedResult<StudentLearningStatistics>>(it).items }
        }.getOrDefault(emptyList())
    }
}
