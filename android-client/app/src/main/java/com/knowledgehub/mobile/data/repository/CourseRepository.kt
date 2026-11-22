package com.knowledgehub.mobile.data.repository

import com.knowledgehub.mobile.data.api.ApiClient
import com.knowledgehub.mobile.data.api.ApiException
import com.knowledgehub.mobile.data.model.*
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.withContext
import kotlinx.serialization.decodeFromString
import kotlinx.serialization.encodeToString
import java.net.URLEncoder

class CourseRepository {

    private fun queryString(vararg params: Pair<String, Any?>): String =
        params.mapNotNull { (key, value) ->
            val text = value?.toString()?.trim()?.takeIf { it.isNotBlank() && it != "null" }
                ?: return@mapNotNull null
            "$key=${URLEncoder.encode(text, "UTF-8")}"
        }.joinToString("&")

    /** 课程中心：本校已发布课程（GET /api/app/course/published） */
    suspend fun getPublishedCourses(
        filter: String = "",
        majorId: String? = null,
        skipCount: Int = 0,
        maxResultCount: Int = 100
    ): List<CourseDto> = withContext(Dispatchers.IO) {
        val query = queryString(
            "filter" to filter,
            "majorId" to majorId,
            "skipCount" to skipCount,
            "maxResultCount" to maxResultCount
        )
        val req = ApiClient.get("/api/app/course/published?$query")
        ApiClient.execute(req) { ApiClient.json.decodeFromString<PagedResult<CourseDto>>(it).items }
    }

    /** 课程中心：共享到本校的课程 */
    suspend fun getSharedCourses(
        filter: String = "",
        majorId: String? = null,
        skipCount: Int = 0,
        maxResultCount: Int = 100
    ): List<SharedCourseDto> = withContext(Dispatchers.IO) {
        val query = queryString(
            "filter" to filter,
            "majorId" to majorId,
            "skipCount" to skipCount,
            "maxResultCount" to maxResultCount
        )
        val req = ApiClient.get("/api/app/course-share/shared-to-me?$query")
        ApiClient.execute(req) { ApiClient.json.decodeFromString<PagedResult<SharedCourseDto>>(it).items }
    }

    /** 课程中心：公共资源库课程（跨校） */
    suspend fun getPublicLibraryCourses(
        filter: String = "",
        majorId: String? = null,
        skipCount: Int = 0,
        maxResultCount: Int = 100
    ): List<CourseDto> = withContext(Dispatchers.IO) {
        val query = queryString(
            "filter" to filter,
            "majorId" to majorId,
            "skipCount" to skipCount,
            "maxResultCount" to maxResultCount
        )
        val req = ApiClient.get("/api/app/course/public-library-courses?$query")
        ApiClient.execute(req) { ApiClient.json.decodeFromString<PagedResult<CourseDto>>(it).items }
    }

    /** 课程中心：专业筛选项 */
    suspend fun getMajorLookup(): List<MajorLookupDto> = withContext(Dispatchers.IO) {
        val req = ApiClient.get("/api/app/major/lookup-list")
        runCatching {
            ApiClient.execute(req) { ApiClient.json.decodeFromString<List<MajorLookupDto>>(it) }
        }.getOrDefault(emptyList())
    }

    /** 学习统计：最近习题提交记录 */
    suspend fun getRecentExerciseRecords(
        isCorrect: Boolean? = null,
        maxResultCount: Int = 20
    ): List<ExerciseRecordDto> = withContext(Dispatchers.IO) {
        val query = queryString(
            "isCorrect" to isCorrect?.let { if (it) 1 else 0 },
            "skipCount" to 0,
            "maxResultCount" to maxResultCount
        )
        val req = ApiClient.get("/api/app/student-exercise-record/my-recent-records?$query")
        ApiClient.execute(req) {
            ApiClient.json.decodeFromString<PagedResult<ExerciseRecordDto>>(it).items
        }
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

    /** 选课：与 Web 端一致，courseId 在路径里 */
    suspend fun enroll(courseId: String) = withContext(Dispatchers.IO) {
        val req = ApiClient.postJson("/api/app/course/enroll/${URLEncoder.encode(courseId, "UTF-8")}", "{}")
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

    /** 教师：我教的课程（与 Web managed-course.service 一致：GET /api/app/course?onlyMine=true） */
    suspend fun getMyTeachingCourses(filter: String = ""): List<CourseDto> = withContext(Dispatchers.IO) {
        val query = queryString(
            "onlyMine" to true,
            "filter" to filter,
            "skipCount" to 0,
            "maxResultCount" to 100
        )
        val req = ApiClient.get("/api/app/course?$query")
        ApiClient.execute(req) { ApiClient.json.decodeFromString<PagedResult<CourseDto>>(it).items }
    }

    /** 教师工作台统计 */
    suspend fun getWorkbenchStats(): WorkbenchStatsDto = withContext(Dispatchers.IO) {
        val req = ApiClient.get("/api/app/workbench/stats")
        ApiClient.execute(req) { ApiClient.json.decodeFromString<WorkbenchStatsDto>(it) }
    }

    /** 创建课程；新建课程固定为草稿，发布需显式调用 publish */
    suspend fun createCourse(
        title: String,
        description: String?,
        semester: String?,
        credits: Int?,
        semesterHours: Int?,
        difficulty: Int,
        majorIds: List<String>
    ): CourseDto? = withContext(Dispatchers.IO) {
        val body = kotlinx.serialization.json.buildJsonObject {
            put("title", kotlinx.serialization.json.JsonPrimitive(title))
            description?.takeIf { it.isNotBlank() }?.let {
                put("description", kotlinx.serialization.json.JsonPrimitive(it))
            }
            semester?.takeIf { it.isNotBlank() }?.let {
                put("semester", kotlinx.serialization.json.JsonPrimitive(it))
            }
            credits?.let { put("credits", kotlinx.serialization.json.JsonPrimitive(it)) }
            semesterHours?.let { put("semesterHours", kotlinx.serialization.json.JsonPrimitive(it)) }
            put("difficulty", kotlinx.serialization.json.JsonPrimitive(difficulty))
            put(
                "majorIds",
                kotlinx.serialization.json.JsonArray(majorIds.map { kotlinx.serialization.json.JsonPrimitive(it) })
            )
            majorIds.firstOrNull()?.let {
                put("majorId", kotlinx.serialization.json.JsonPrimitive(it))
            }
        }.toString()
        val req = ApiClient.postJson("/api/app/course", body)
        runCatching {
            ApiClient.execute(req) { ApiClient.json.decodeFromString<CourseDto>(it) }
        }.getOrNull()
    }

    suspend fun publishCourse(courseId: String) = withContext(Dispatchers.IO) {
        val req = ApiClient.postJson("/api/app/course/$courseId/publish", "{}")
        ApiClient.execute(req) { it }
    }

    suspend fun unpublishCourse(courseId: String) = withContext(Dispatchers.IO) {
        val req = ApiClient.postJson("/api/app/course/$courseId/unpublish", "{}")
        ApiClient.execute(req) { it }
    }

    suspend fun deleteCourse(courseId: String) = withContext(Dispatchers.IO) {
        val req = ApiClient.delete("/api/app/course/$courseId")
        ApiClient.execute(req) { it }
    }

    /** 教师内容管理：课程下全部习题 */
    suspend fun getExercisesByCourse(courseId: String): List<ExerciseDto> = withContext(Dispatchers.IO) {
        val req = ApiClient.get("/api/app/exercise/by-course/$courseId")
        runCatching {
            ApiClient.execute(req) { ApiClient.json.decodeFromString<List<ExerciseDto>>(it) }
        }.getOrDefault(emptyList())
    }

    /** 教师内容管理：课程知识资源 */
    suspend fun getKnowledgeResources(courseId: String): List<KnowledgeResourceDto> = withContext(Dispatchers.IO) {
        val req = ApiClient.get("/api/app/knowledge-resource/by-course/$courseId")
        runCatching {
            ApiClient.execute(req) { ApiClient.json.decodeFromString<List<KnowledgeResourceDto>>(it) }
        }.getOrDefault(emptyList())
    }

    suspend fun createChapter(
        courseId: String,
        title: String,
        description: String?,
        parentId: String?,
        sortOrder: Int
    ) = withContext(Dispatchers.IO) {
        val body = kotlinx.serialization.json.buildJsonObject {
            put("courseId", kotlinx.serialization.json.JsonPrimitive(courseId))
            put("title", kotlinx.serialization.json.JsonPrimitive(title))
            description?.takeIf { it.isNotBlank() }?.let {
                put("description", kotlinx.serialization.json.JsonPrimitive(it))
            }
            parentId?.takeIf { it.isNotBlank() }?.let {
                put("parentId", kotlinx.serialization.json.JsonPrimitive(it))
            }
            put("sortOrder", kotlinx.serialization.json.JsonPrimitive(sortOrder))
        }.toString()
        val req = ApiClient.postJson("/api/app/chapter", body)
        ApiClient.execute(req) { it }
    }

    suspend fun createExercise(
        courseId: String,
        chapterId: String?,
        title: String,
        questionContent: String,
        type: Int,
        options: String?,
        answer: String,
        questionAnalysis: String?,
        difficulty: Int,
        score: Int
    ) = withContext(Dispatchers.IO) {
        val body = kotlinx.serialization.json.buildJsonObject {
            put("courseId", kotlinx.serialization.json.JsonPrimitive(courseId))
            chapterId?.takeIf { it.isNotBlank() }?.let {
                put("chapterId", kotlinx.serialization.json.JsonPrimitive(it))
                put(
                    "chapterIds",
                    kotlinx.serialization.json.JsonArray(listOf(kotlinx.serialization.json.JsonPrimitive(it)))
                )
            }
            put("title", kotlinx.serialization.json.JsonPrimitive(title))
            put("questionContent", kotlinx.serialization.json.JsonPrimitive(questionContent))
            put("type", kotlinx.serialization.json.JsonPrimitive(type))
            options?.takeIf { it.isNotBlank() }?.let {
                put("options", kotlinx.serialization.json.JsonPrimitive(it))
            }
            put("answer", kotlinx.serialization.json.JsonPrimitive(answer))
            questionAnalysis?.takeIf { it.isNotBlank() }?.let {
                put("questionAnalysis", kotlinx.serialization.json.JsonPrimitive(it))
            }
            put("difficulty", kotlinx.serialization.json.JsonPrimitive(difficulty))
            put("score", kotlinx.serialization.json.JsonPrimitive(score))
        }.toString()
        val req = ApiClient.postJson("/api/app/exercise", body)
        ApiClient.execute(req) { it }
    }

    suspend fun createKnowledgeResource(
        courseId: String,
        chapterId: String?,
        name: String,
        description: String?,
        content: String?
    ) = withContext(Dispatchers.IO) {
        val body = kotlinx.serialization.json.buildJsonObject {
            put("courseId", kotlinx.serialization.json.JsonPrimitive(courseId))
            chapterId?.takeIf { it.isNotBlank() }?.let {
                put("chapterId", kotlinx.serialization.json.JsonPrimitive(it))
            }
            put("name", kotlinx.serialization.json.JsonPrimitive(name))
            description?.takeIf { it.isNotBlank() }?.let {
                put("description", kotlinx.serialization.json.JsonPrimitive(it))
            }
            content?.takeIf { it.isNotBlank() }?.let {
                put("content", kotlinx.serialization.json.JsonPrimitive(it))
            }
            put("importanceLevel", kotlinx.serialization.json.JsonPrimitive("normal"))
            put("difficulty", kotlinx.serialization.json.JsonPrimitive(1))
            put("sortOrder", kotlinx.serialization.json.JsonPrimitive(0))
        }.toString()
        val req = ApiClient.postJson("/api/app/knowledge-resource", body)
        ApiClient.execute(req) { it }
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
