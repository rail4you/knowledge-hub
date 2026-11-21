package com.knowledgehub.mobile.data.model

import kotlinx.serialization.Serializable

/**
 * 课程 DTO：对应后端 CourseDto（KnowledgeHub.Courses.Dtos）。
 * 只声明客户端用到的字段；kotlinx.serialization 默认忽略未知字段。
 */
@Serializable
data class CourseDto(
    val id: String,
    val title: String = "",
    val description: String? = null,
    val coverImageUrl: String? = null,
    val majorId: String? = null,
    val majorName: String? = null,
    val majorIds: List<String> = emptyList(),
    val majorNames: List<String> = emptyList(),
    val libraryId: String? = null,
    val tenantId: String? = null,
    val semester: String? = null,
    val credits: Int? = null,
    val semesterHours: Int? = null,
    val status: Int = 0,
    val difficulty: Int = 0,
    val isRecommended: Boolean = false,
    val isShared: Boolean = false,
    val sharingScope: Int? = null,
    val teacherId: String? = null,
    val categoryId: String? = null,
    val teacherName: String? = null,
    val chapterCount: Int = 0,
    val studentCount: Int = 0,
    val isEnrolled: Boolean = false,
    val progress: Double = 0.0,
    val canEdit: Boolean = false,
    val ownerTenantName: String? = null,
    val isOtherSchoolCourse: Boolean = false
)

/** 共享课程：GET /api/app/course-share/shared-to-me */
@Serializable
data class SharedCourseDto(
    val id: String = "",
    val shareId: String? = null,
    val courseId: String = "",
    val title: String = "",
    val description: String? = null,
    val coverImageUrl: String? = null,
    val majorId: String? = null,
    val majorName: String? = null,
    val libraryId: String? = null,
    val sourceTenantId: String? = null,
    val sourceTenantName: String? = null,
    val sharedAt: String? = null,
    val sharedByUserId: String? = null,
    val sharedByUserName: String? = null
)

/** 专业字典：GET /api/app/major/lookup-list */
@Serializable
data class MajorLookupDto(
    val id: String = "",
    val name: String = "",
    val code: String? = null
)

/** 课程详情 = CourseDto + Chapters */
@Serializable
data class CourseDetailDto(
    val id: String,
    val title: String = "",
    val description: String? = null,
    val coverImageUrl: String? = null,
    val majorName: String? = null,
    val teacherName: String? = null,
    val semester: String? = null,
    val credits: Int? = null,
    val semesterHours: Int? = null,
    val difficulty: Int = 0,
    val status: Int = 0,
    val chapterCount: Int = 0,
    val studentCount: Int = 0,
    val isEnrolled: Boolean = false,
    val progress: Double = 0.0,
    val canEdit: Boolean = false,
    val chapters: List<ChapterDto> = emptyList()
)

/** 章节 DTO（后端 ChapterDto 的核心字段） */
@Serializable
data class ChapterDto(
    val id: String,
    val title: String = "",
    val description: String? = null,
    val parentId: String? = null,
    val sortOrder: Int = 0,
    val children: List<ChapterDto> = emptyList(),
    val knowledgeResources: List<KnowledgeResourceDto> = emptyList()
)

/** 学生课程列表项：GET /api/app/learning/my-courses */
@Serializable
data class StudentCourseListItem(
    val id: String = "",
    val courseId: String = "",
    val courseTitle: String = "",
    val courseCoverImageUrl: String? = null,
    val majorName: String? = null,
    val semester: String? = null,
    val progress: Double = 0.0,
    val status: Int = 0,
    val chapterCount: Int = 0,
    val completedExerciseCount: Int = 0,
    val totalExerciseCount: Int = 0,
    val studyMinutes: Int? = null,
    val lastLearnedAt: String? = null
)

/** 学习仪表盘：GET /api/app/learning/dashboard */
@Serializable
data class LearningDashboard(
    val totalCourses: Int = 0,
    val completedCourses: Int = 0,
    val inProgressCourses: Int = 0,
    val notStartedCourses: Int = 0,
    val totalLearningTime: Double = 0.0,
    val totalExerciseRecords: Int = 0,
    val totalResourceActivities: Int = 0,
    val averageProgress: Double = 0.0,
    val dailyTimeLabels: List<String> = emptyList(),
    val dailyTimeValues: List<Double> = emptyList(),
    val knowledgeDimensions: List<KnowledgeDimensionDto> = emptyList(),
    val masteryValues: List<Double> = emptyList(),
    val recentLearning: List<RecentLearningItem> = emptyList()
)

@Serializable
data class KnowledgeDimensionDto(
    val name: String = "",
    val maxValue: Int = 100
)

@Serializable
data class RecentLearningItem(
    val courseId: String = "",
    val courseName: String = "",
    val progress: Double = 0.0,
    val lastAccessAt: String? = null
)

/** 学习进度：GET /api/app/learning/progress/{courseId} */
@Serializable
data class LearningProgress(
    val id: String = "",
    val courseId: String = "",
    val chapterId: String? = null,
    val resourceId: String? = null,
    val progress: Double = 0.0,
    val lastPosition: String? = null,
    val lastAccessAt: String? = null
)

/** 教师：课程学习概览 GET /api/app/student-exercise-record/course-learning-overview */
@Serializable
data class CourseLearningOverview(
    val courseId: String = "",
    val courseName: String = "",
    val totalStudents: Int = 0,
    val activeStudents: Int = 0,
    val totalExercises: Int = 0,
    val averageCompletionRate: Double = 0.0,
    val averageCorrectRate: Double = 0.0,
    val totalLearningMinutes: Double = 0.0,
    val chapterProgress: List<ChapterProgress> = emptyList()
)

@Serializable
data class ChapterProgress(
    val chapterId: String = "",
    val chapterName: String = "",
    val totalExercises: Int = 0,
    val completedCount: Int = 0,
    val completionRate: Double = 0.0,
    val correctRate: Double = 0.0,
    val participantCount: Int = 0
)

/** 教师：学生统计 GET /api/app/student-exercise-record/learning-statistics */
@Serializable
data class StudentLearningStatistics(
    val studentId: String = "",
    val studentName: String = "",
    val completedCount: Int = 0,
    val totalCount: Int = 0,
    val completionRate: Double = 0.0,
    val correctRate: Double = 0.0,
    val lastActiveTime: String? = null
)
