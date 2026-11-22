package com.knowledgehub.mobile.data.model

import kotlinx.serialization.Serializable

/** 知识点/学习资源：章节直挂的 knowledgeResources（KnowledgeResourceDto） */
@Serializable
data class KnowledgeResourceDto(
    val id: String,
    val courseId: String? = null,
    val chapterId: String? = null,
    val name: String = "",
    val description: String? = null,
    val content: String? = null,
    val importanceLevel: String? = null,   // 后端返回字符串（如 "normal"）
    val difficulty: Int = 0,
    val sortOrder: Int = 0,
    val tags: String? = null,
    val parentId: String? = null,
    val resourceId: String? = null,        // 有值才能预览文件
    val originalFileName: String? = null,
    val fileExtension: String? = null,
    val fileSize: Long? = null,
    val children: List<KnowledgeResourceDto> = emptyList()
)

/** 习题（ExerciseDto）。type: 0=单选 1=多选 2=判断(正确/错误) 3=填空 4=简答（真实后端枚举） */
@Serializable
data class ExerciseDto(
    val id: String,
    val courseId: String? = null,
    val chapterId: String? = null,
    val title: String = "",
    val questionContent: String = "",
    val type: Int = 0,
    val options: String? = null,           // JSON 字符串数组，如 ["A. xx","B. xx"]
    val answer: String? = null,
    val questionAnalysis: String? = null,
    val difficulty: Int = 0,
    val score: Double = 0.0,
    val knowledgeResourceId: String? = null,
    val isAiGenerated: Boolean = false
)

/** 习题提交记录（StudentExerciseRecordDto） */
@Serializable
data class ExerciseRecordDto(
    val id: String = "",
    val courseId: String? = null,
    val courseName: String? = null,
    val chapterId: String? = null,
    val chapterName: String? = null,
    val exerciseId: String? = null,
    val exerciseTitle: String? = null,
    val exerciseType: Int = 0,
    val studentAnswer: String? = null,
    val isCorrect: Boolean? = null,
    val hasViewedAnswer: Boolean = false,
    val viewedAt: String? = null,
    val selfAssessment: Int = 0,
    val timeSpent: String? = null,
    val completedAt: String? = null
)

/** 习题进度（CourseExerciseProgressDto）：完成数/总数 */
@Serializable
data class CourseExerciseProgress(
    val courseId: String = "",
    val completedCount: Int = 0,
    val totalCount: Int = 0
)

/** 资源文件元数据：用于旧资源扩展名为空时回填（GET /api/app/resource/{id}） */
@Serializable
data class ResourceFileInfoDto(
    val id: String = "",
    val name: String? = null,
    val originalFileName: String? = null,
    val fileExtension: String? = null,
    val fileSize: Long? = null,
    val resourceId: String? = null
)
