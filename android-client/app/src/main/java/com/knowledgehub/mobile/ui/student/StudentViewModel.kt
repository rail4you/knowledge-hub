package com.knowledgehub.mobile.ui.student

import androidx.lifecycle.ViewModel
import androidx.lifecycle.viewModelScope
import com.knowledgehub.mobile.data.SessionManager
import com.knowledgehub.mobile.data.model.*
import com.knowledgehub.mobile.data.repository.CourseRepository
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.StateFlow
import kotlinx.coroutines.flow.asStateFlow
import kotlinx.coroutines.launch

data class StudentUiState(
    val isLoading: Boolean = false,
    val myCourses: List<StudentCourseListItem> = emptyList(),
    val exploreCourses: List<CourseDto> = emptyList(),
    val dashboard: LearningDashboard? = null,
    val error: String? = null,
    // 详情
    val detail: CourseDetailDto? = null,
    val detailLoading: Boolean = false,
    val progress: LearningProgress? = null,
    val exerciseProgress: CourseExerciseProgress? = null,
    val message: String? = null
)

class StudentViewModel(
    val courseRepo: CourseRepository,
    val session: SessionManager
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

    fun loadExplore() {
        viewModelScope.launch {
            _uiState.value = _uiState.value.copy(isLoading = true, error = null)
            runCatching { courseRepo.getPublishedCourses() }
                .onSuccess {
                    _uiState.value = _uiState.value.copy(isLoading = false, exploreCourses = it)
                }
                .onFailure {
                    _uiState.value = _uiState.value.copy(isLoading = false, error = it.message ?: "加载失败")
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
                _uiState.value = _uiState.value.copy(message = "选课成功")
                loadHome()
            }.onFailure {
                _uiState.value = _uiState.value.copy(message = it.message ?: "选课失败")
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
