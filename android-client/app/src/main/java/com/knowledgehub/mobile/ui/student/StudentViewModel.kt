package com.knowledgehub.mobile.ui.student

import androidx.lifecycle.ViewModel
import androidx.lifecycle.viewModelScope
import com.knowledgehub.mobile.data.SessionManager
import com.knowledgehub.mobile.data.model.*
import com.knowledgehub.mobile.data.repository.AuthRepository
import com.knowledgehub.mobile.data.repository.CourseRepository
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.StateFlow
import kotlinx.coroutines.flow.asStateFlow
import kotlinx.coroutines.launch

/** 课程中心范围：与 Web /student/courses 的 published/shared/public 对应 */
enum class CourseCenterScope(val label: String) {
    SCHOOL("本校课程"),
    SHARED("共享课程"),
    PUBLIC("公共课程")
}

/** 学习统计：最近习题记录筛选 */
enum class RecordFilter(val label: String) {
    ALL("全部"),
    CORRECT("正确"),
    WRONG("错误")
}

/** 课程中心列表卡片视图模型 */
data class CourseCenterItem(
    val courseId: String = "",
    val title: String = "",
    val description: String? = null,
    val coverImageUrl: String? = null,
    val majorNames: List<String> = emptyList(),
    val teacherName: String? = null,
    val ownerTenantName: String? = null,
    val sourceTenantName: String? = null,
    val semester: String? = null,
    val credits: Int? = null,
    val semesterHours: Int? = null,
    val chapterCount: Int = 0,
    val studentCount: Int = 0,
    val scope: CourseCenterScope = CourseCenterScope.SCHOOL,
    val isEnrolled: Boolean = false,
    val progress: Double = 0.0
)

data class StudentUiState(
    val isLoading: Boolean = false,
    val myCourses: List<StudentCourseListItem> = emptyList(),
    val exploreCourses: List<CourseDto> = emptyList(),
    val dashboard: LearningDashboard? = null,
    val error: String? = null,

    // 课程中心
    val courseCenterItems: List<CourseCenterItem> = emptyList(),
    val courseScope: CourseCenterScope = CourseCenterScope.SCHOOL,
    val courseKeyword: String = "",
    val majorId: String? = null,
    val majors: List<MajorLookupDto> = emptyList(),
    val courseCenterLoading: Boolean = false,
    val courseCenterError: String? = null,

    // 学习统计
    val statsLoading: Boolean = false,
    val statsError: String? = null,
    val recentRecords: List<ExerciseRecordDto> = emptyList(),
    val recentRecordsLoading: Boolean = false,
    val recordFilter: RecordFilter = RecordFilter.ALL,

    // 设置
    val profile: MyProfileDto? = null,
    val profileLoading: Boolean = false,
    val profileSaving: Boolean = false,

    // 详情
    val detail: CourseDetailDto? = null,
    val detailLoading: Boolean = false,
    val progress: LearningProgress? = null,
    val exerciseProgress: CourseExerciseProgress? = null,
    val message: String? = null
)

class StudentViewModel(
    val courseRepo: CourseRepository,
    val session: SessionManager,
    val authRepo: AuthRepository = AuthRepository()
) : ViewModel() {

    private val _uiState = MutableStateFlow(StudentUiState())
    val uiState: StateFlow<StudentUiState> = _uiState.asStateFlow()

    fun loadHome() {
        viewModelScope.launch {
            _uiState.value = _uiState.value.copy(isLoading = true, error = null)
            runCatching {
                val courses = courseRepo.getMyCourses()
                val dash = runCatching { courseRepo.getDashboard() }.getOrNull()
                _uiState.value = _uiState.value.copy(
                    isLoading = false,
                    myCourses = courses,
                    dashboard = dash
                )
            }.onFailure {
                _uiState.value = _uiState.value.copy(
                    isLoading = false,
                    error = it.message ?: "加载失败"
                )
            }
        }
    }

    /** 进入课程中心 Tab：先加载专业字典，再加载课程列表 */
    fun loadExplore() {
        viewModelScope.launch {
            _uiState.value = _uiState.value.copy(courseCenterError = null)
            runCatching { courseRepo.getMajorLookup() }
                .onSuccess { _uiState.value = _uiState.value.copy(majors = it) }
            loadCourseCenter()
        }
    }

    fun setCourseScope(scope: CourseCenterScope) {
        if (_uiState.value.courseScope == scope) return
        _uiState.value = _uiState.value.copy(courseScope = scope)
        loadCourseCenter()
    }

    fun setCourseKeyword(keyword: String) {
        _uiState.value = _uiState.value.copy(courseKeyword = keyword)
        loadCourseCenter()
    }

    fun setMajor(majorId: String?) {
        _uiState.value = _uiState.value.copy(majorId = majorId?.takeIf { it.isNotBlank() })
        loadCourseCenter()
    }

    private fun loadCourseCenter() {
        viewModelScope.launch {
            _uiState.value = _uiState.value.copy(courseCenterLoading = true, courseCenterError = null)
            val current = _uiState.value
            val filter = current.courseKeyword.trim()
            val majorId = current.majorId
            runCatching {
                val mine = courseRepo.getMyCourses().associateBy { it.courseId }
                val items = when (current.courseScope) {
                    CourseCenterScope.SCHOOL -> courseRepo
                        .getPublishedCourses(filter = filter, majorId = majorId)
                        .map { it.toCenterItem(CourseCenterScope.SCHOOL, mine) }
                    CourseCenterScope.SHARED -> courseRepo
                        .getSharedCourses(filter = filter, majorId = majorId)
                        .map { it.toCenterItem(mine) }
                    CourseCenterScope.PUBLIC -> courseRepo
                        .getPublicLibraryCourses(filter = filter, majorId = majorId)
                        .map { it.toCenterItem(CourseCenterScope.PUBLIC, mine) }
                }
                _uiState.value = _uiState.value.copy(
                    courseCenterLoading = false,
                    courseCenterItems = items
                )
            }.onFailure {
                _uiState.value = _uiState.value.copy(
                    courseCenterLoading = false,
                    courseCenterError = it.message ?: "课程列表加载失败"
                )
            }
        }
    }

    private fun CourseDto.toCenterItem(
        scope: CourseCenterScope,
        mine: Map<String, StudentCourseListItem>
    ): CourseCenterItem {
        val enrolled = isEnrolled || mine.containsKey(id)
        val names = majorNames.filter { it.isNotBlank() }.ifEmpty { listOfNotNull(majorName) }
        return CourseCenterItem(
            courseId = id,
            title = title.ifBlank { "未命名课程" },
            description = description,
            coverImageUrl = coverImageUrl,
            majorNames = names,
            teacherName = teacherName,
            ownerTenantName = ownerTenantName,
            sourceTenantName = ownerTenantName.takeIf { isOtherSchoolCourse },
            semester = semester,
            credits = credits,
            semesterHours = semesterHours,
            chapterCount = chapterCount,
            studentCount = studentCount,
            scope = scope,
            isEnrolled = enrolled,
            progress = mine[id]?.progress ?: progress
        )
    }

    private fun SharedCourseDto.toCenterItem(
        mine: Map<String, StudentCourseListItem>
    ): CourseCenterItem {
        val id = courseId.ifBlank { this.id }
        return CourseCenterItem(
            courseId = id,
            title = title.ifBlank { "未命名课程" },
            description = description,
            coverImageUrl = coverImageUrl,
            majorNames = listOfNotNull(majorName?.takeIf { it.isNotBlank() }),
            sourceTenantName = sourceTenantName,
            scope = CourseCenterScope.SHARED,
            isEnrolled = mine.containsKey(id),
            progress = mine[id]?.progress ?: 0.0
        )
    }

    /** 学习统计 Tab */
    fun loadStats() {
        viewModelScope.launch {
            _uiState.value = _uiState.value.copy(statsLoading = true, statsError = null)
            runCatching {
                val dash = courseRepo.getDashboard()
                val courses = runCatching { courseRepo.getMyCourses() }.getOrDefault(emptyList())
                val records = loadRecentRecordsInternal(_uiState.value.recordFilter)
                _uiState.value = _uiState.value.copy(
                    statsLoading = false,
                    dashboard = dash,
                    myCourses = courses,
                    recentRecords = records
                )
            }.onFailure {
                _uiState.value = _uiState.value.copy(
                    statsLoading = false,
                    statsError = it.message ?: "学习统计加载失败"
                )
            }
        }
    }

    fun setRecordFilter(filter: RecordFilter) {
        if (_uiState.value.recordFilter == filter) return
        _uiState.value = _uiState.value.copy(recordFilter = filter)
        viewModelScope.launch {
            _uiState.value = _uiState.value.copy(recentRecordsLoading = true)
            runCatching { loadRecentRecordsInternal(filter) }
                .onSuccess { _uiState.value = _uiState.value.copy(recentRecordsLoading = false, recentRecords = it) }
                .onFailure { _uiState.value = _uiState.value.copy(recentRecordsLoading = false) }
        }
    }

    private suspend fun loadRecentRecordsInternal(filter: RecordFilter): List<ExerciseRecordDto> =
        courseRepo.getRecentExerciseRecords(
            isCorrect = when (filter) {
                RecordFilter.ALL -> null
                RecordFilter.CORRECT -> true
                RecordFilter.WRONG -> false
            },
            maxResultCount = 20
        )

    /** 设置 Tab */
    fun loadProfile() {
        viewModelScope.launch {
            _uiState.value = _uiState.value.copy(profileLoading = true)
            runCatching { authRepo.getMyProfile() }
                .onSuccess { _uiState.value = _uiState.value.copy(profileLoading = false, profile = it) }
                .onFailure {
                    _uiState.value = _uiState.value.copy(
                        profileLoading = false,
                        message = it.message ?: "账户信息加载失败"
                    )
                }
        }
    }

    fun updateProfile(name: String, email: String?, phoneNumber: String?) {
        if (name.isBlank()) {
            _uiState.value = _uiState.value.copy(message = "姓名不能为空")
            return
        }
        viewModelScope.launch {
            _uiState.value = _uiState.value.copy(profileSaving = true)
            runCatching {
                authRepo.updateMyProfile(name.trim(), email?.trim(), phoneNumber?.trim())
                authRepo.getMyProfile()
            }.onSuccess {
                _uiState.value = _uiState.value.copy(
                    profileSaving = false,
                    profile = it,
                    message = "账户资料已保存"
                )
            }.onFailure {
                _uiState.value = _uiState.value.copy(
                    profileSaving = false,
                    message = it.message ?: "保存失败"
                )
            }
        }
    }

    fun changePassword(currentPassword: String, newPassword: String, confirmPassword: String) {
        when {
            currentPassword.isBlank() || newPassword.isBlank() ->
                _uiState.value = _uiState.value.copy(message = "请填写当前密码和新密码")
            newPassword != confirmPassword ->
                _uiState.value = _uiState.value.copy(message = "两次输入的新密码不一致")
            newPassword.length < 6 ->
                _uiState.value = _uiState.value.copy(message = "新密码至少 6 位")
            else -> viewModelScope.launch {
                _uiState.value = _uiState.value.copy(profileSaving = true)
                runCatching { authRepo.changePassword(currentPassword, newPassword) }
                    .onSuccess {
                        _uiState.value = _uiState.value.copy(
                            profileSaving = false,
                            message = "密码修改成功"
                        )
                    }
                    .onFailure {
                        _uiState.value = _uiState.value.copy(
                            profileSaving = false,
                            message = it.message ?: "密码修改失败"
                        )
                    }
            }
        }
    }

    fun loadDetail(courseId: String) {
        viewModelScope.launch {
            _uiState.value = _uiState.value.copy(detailLoading = true, detail = null)
            runCatching {
                val detail = courseRepo.getDetail(courseId)
                val exerciseProgress = runCatching { courseRepo.getExerciseProgress(courseId) }.getOrNull()
                _uiState.value = _uiState.value.copy(
                    detailLoading = false,
                    detail = detail,
                    exerciseProgress = exerciseProgress
                )
            }.onFailure {
                _uiState.value = _uiState.value.copy(
                    detailLoading = false,
                    error = it.message ?: "加载课程详情失败"
                )
            }
        }
    }

    fun enroll(courseId: String) {
        viewModelScope.launch {
            runCatching {
                courseRepo.enroll(courseId)
                _uiState.value = _uiState.value.copy(message = "报名成功")
                loadHome()
                loadCourseCenter()
            }.onFailure {
                _uiState.value = _uiState.value.copy(message = it.message ?: "报名失败")
            }
        }
    }

    /** 详情页报名：报名成功后刷新详情，按钮切换为“开始学习” */
    fun enrollForDetail(courseId: String) {
        viewModelScope.launch {
            runCatching {
                courseRepo.enroll(courseId)
                val detail = courseRepo.getDetail(courseId)
                val progress = runCatching { courseRepo.getExerciseProgress(courseId) }.getOrNull()
                _uiState.value = _uiState.value.copy(
                    detail = detail,
                    exerciseProgress = progress,
                    message = "报名成功，可以开始学习了"
                )
                loadHome()
            }.onFailure {
                _uiState.value = _uiState.value.copy(message = it.message ?: "报名失败")
            }
        }
    }

    fun clearMessage() {
        _uiState.value = _uiState.value.copy(message = null)
    }

    suspend fun logout() {
        session.clear()
    }
}
