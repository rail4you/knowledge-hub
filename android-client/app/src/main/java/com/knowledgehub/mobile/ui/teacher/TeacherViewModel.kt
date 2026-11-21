package com.knowledgehub.mobile.ui.teacher

import androidx.lifecycle.ViewModel
import androidx.lifecycle.viewModelScope
import com.knowledgehub.mobile.data.SessionManager
import com.knowledgehub.mobile.data.model.*
import com.knowledgehub.mobile.data.repository.CourseRepository
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.StateFlow
import kotlinx.coroutines.flow.asStateFlow
import kotlinx.coroutines.launch

data class TeacherUiState(
    val isLoading: Boolean = false,
    val courses: List<CourseDto> = emptyList(),
    val overview: CourseLearningOverview? = null,
    val students: List<StudentLearningStatistics> = emptyList(),
    val error: String? = null,
    val selectedCourseId: String? = null,
    val selectedCourseTitle: String = ""
)

class TeacherViewModel(
    private val courseRepo: CourseRepository,
    val session: SessionManager
) : ViewModel() {

    private val _uiState = MutableStateFlow(TeacherUiState())
    val uiState: StateFlow<TeacherUiState> = _uiState.asStateFlow()

    fun loadHome() {
        viewModelScope.launch {
            _uiState.value = _uiState.value.copy(isLoading = true, error = null)
            runCatching { courseRepo.getMyTeachingCourses() }
                .onSuccess { _uiState.value = _uiState.value.copy(isLoading = false, courses = it) }
                .onFailure {
                    _uiState.value = _uiState.value.copy(isLoading = false, error = it.message ?: "加载失败")
                }
        }
    }

    fun selectCourse(courseId: String, title: String) {
        _uiState.value = _uiState.value.copy(
            selectedCourseId = courseId,
            selectedCourseTitle = title,
            overview = null,
            students = emptyList()
        )
        viewModelScope.launch {
            runCatching {
                val overview = courseRepo.getCourseOverview(courseId)
                val students = courseRepo.getLearningStatistics(courseId)
                _uiState.value = _uiState.value.copy(overview = overview, students = students)
            }.onFailure {
                _uiState.value = _uiState.value.copy(error = it.message ?: "加载学情失败")
            }
        }
    }

    fun clearSelection() {
        _uiState.value = _uiState.value.copy(selectedCourseId = null, overview = null, students = emptyList())
    }

    suspend fun logout() { session.clear() }
}
