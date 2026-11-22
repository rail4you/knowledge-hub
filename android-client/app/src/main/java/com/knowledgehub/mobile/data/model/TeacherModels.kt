package com.knowledgehub.mobile.data.model

import kotlinx.serialization.Serializable

/** GET /api/app/workbench/stats 的精简视图，只保留教师工作台需要的字段。 */
@Serializable
data class WorkbenchStatsDto(
    val tenantName: String? = null,
    val courses: WorkbenchCourseStatsDto = WorkbenchCourseStatsDto(),
    val users: WorkbenchUserStatsDto = WorkbenchUserStatsDto(),
    val ai: WorkbenchAiStatsDto = WorkbenchAiStatsDto(),
    val resources: WorkbenchResourceStatsDto = WorkbenchResourceStatsDto()
)

@Serializable
data class WorkbenchCourseStatsDto(
    val total: Long = 0,
    val published: Long = 0,
    val pendingReview: Long = 0,
    val draft: Long = 0,
    val chapterCount: Long = 0,
    val exerciseCount: Long = 0,
    val majorCount: Long = 0,
    val enrollmentCount: Long = 0
)

@Serializable
data class WorkbenchUserStatsDto(
    val studentCount: Long = 0,
    val teacherCount: Long = 0
)

@Serializable
data class WorkbenchAiStatsDto(
    val totalCalls: Long = 0,
    val successCalls: Long = 0,
    val runningCalls: Long = 0,
    val taskCount: Long = 0
)

@Serializable
data class WorkbenchResourceStatsDto(
    val total: Long = 0,
    val approved: Long = 0
)
