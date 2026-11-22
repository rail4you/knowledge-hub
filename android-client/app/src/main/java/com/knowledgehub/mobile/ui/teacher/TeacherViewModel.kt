package com.knowledgehub.mobile.ui.teacher

import androidx.lifecycle.ViewModel
import androidx.lifecycle.viewModelScope
import com.knowledgehub.mobile.data.SessionManager
import com.knowledgehub.mobile.data.model.*
import com.knowledgehub.mobile.data.repository.AiRepository
import com.knowledgehub.mobile.data.repository.CourseRepository
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.StateFlow
import kotlinx.coroutines.flow.asStateFlow
import kotlinx.coroutines.launch

data class TeacherUiState(
    val isLoading: Boolean = false,
    val courses: List<CourseDto> = emptyList(),
    val dashboard: WorkbenchStatsDto? = null,
    val error: String? = null,
    val message: String? = null,
    val courseKeyword: String = "",
    val majors: List<MajorLookupDto> = emptyList(),

    // 学情二级页面
    val selectedCourseId: String? = null,
    val selectedCourseTitle: String = "",
    val overview: CourseLearningOverview? = null,
    val students: List<StudentLearningStatistics> = emptyList(),

    // 课程内容
    val contentCourseId: String? = null,
    val chapters: List<ChapterDto> = emptyList(),
    val exercises: List<ExerciseDto> = emptyList(),
    val resources: List<KnowledgeResourceDto> = emptyList(),

    // AI 工具
    val aiCourseId: String? = null,
    val aiGeneratedExercises: List<ExerciseDto> = emptyList(),
    val aiText: String = "",
    val aiBusy: Boolean = false
)

class TeacherViewModel(
    private val courseRepo: CourseRepository,
    val session: SessionManager,
    private val aiRepo: AiRepository = AiRepository()
) : ViewModel() {

    private val _uiState = MutableStateFlow(TeacherUiState())
    val uiState: StateFlow<TeacherUiState> = _uiState.asStateFlow()

    fun loadHome() {
        viewModelScope.launch {
            _uiState.value = _uiState.value.copy(isLoading = true, error = null)
            runCatching {
                val courses = courseRepo.getMyTeachingCourses(_uiState.value.courseKeyword)
                val stats = runCatching { courseRepo.getWorkbenchStats() }.getOrNull()
                val majors = runCatching { courseRepo.getMajorLookup() }.getOrDefault(emptyList())
                _uiState.value = _uiState.value.copy(
                    isLoading = false,
                    courses = courses,
                    dashboard = stats,
                    majors = majors
                )
            }.onFailure {
                _uiState.value = _uiState.value.copy(isLoading = false, error = it.message ?: "加载失败")
            }
        }
    }

    fun loadCourses() {
        viewModelScope.launch {
            _uiState.value = _uiState.value.copy(isLoading = true, error = null)
            runCatching { courseRepo.getMyTeachingCourses(_uiState.value.courseKeyword) }
                .onSuccess { _uiState.value = _uiState.value.copy(isLoading = false, courses = it) }
                .onFailure {
                    _uiState.value = _uiState.value.copy(isLoading = false, error = it.message ?: "课程加载失败")
                }
        }
    }

    fun setCourseKeyword(keyword: String) {
        _uiState.value = _uiState.value.copy(courseKeyword = keyword)
        loadCourses()
    }

    fun createCourse(
        title: String,
        description: String?,
        semester: String?,
        credits: Int?,
        semesterHours: Int?,
        difficulty: Int,
        majorIds: List<String>
    ) {
        if (title.isBlank()) {
            _uiState.value = _uiState.value.copy(message = "课程标题不能为空")
            return
        }
        viewModelScope.launch {
            runCatching {
                courseRepo.createCourse(
                    title.trim(), description, semester, credits, semesterHours, difficulty, majorIds
                )
            }.onSuccess {
                _uiState.value = _uiState.value.copy(message = "课程已创建（草稿），可继续添加内容")
                loadHome()
            }.onFailure {
                _uiState.value = _uiState.value.copy(message = it.message ?: "创建课程失败")
            }
        }
    }

    fun setCoursePublished(courseId: String, published: Boolean) {
        viewModelScope.launch {
            runCatching {
                if (published) courseRepo.publishCourse(courseId) else courseRepo.unpublishCourse(courseId)
            }.onSuccess {
                _uiState.value = _uiState.value.copy(message = if (published) "课程已发布" else "课程已撤回")
                loadHome()
            }.onFailure {
                _uiState.value = _uiState.value.copy(message = it.message ?: "操作失败")
            }
        }
    }

    fun deleteCourse(courseId: String) {
        viewModelScope.launch {
            runCatching { courseRepo.deleteCourse(courseId) }
                .onSuccess {
                    _uiState.value = _uiState.value.copy(message = "课程已删除")
                    loadHome()
                }
                .onFailure {
                    _uiState.value = _uiState.value.copy(message = it.message ?: "删除失败")
                }
        }
    }

    fun openContent(courseId: String) {
        _uiState.value = _uiState.value.copy(
            contentCourseId = courseId,
            chapters = emptyList(),
            exercises = emptyList(),
            resources = emptyList()
        )
        loadContent(courseId)
    }

    private fun loadContent(courseId: String) {
        viewModelScope.launch {
            runCatching {
                val chapters = courseRepo.getChapterTree(courseId)
                val exercises = courseRepo.getExercisesByCourse(courseId)
                val resources = courseRepo.getKnowledgeResources(courseId)
                _uiState.value = _uiState.value.copy(chapters = chapters, exercises = exercises, resources = resources)
            }.onFailure {
                _uiState.value = _uiState.value.copy(message = it.message ?: "课程内容加载失败")
            }
        }
    }

    fun createChapter(title: String, description: String?, parentId: String?, sortOrder: Int) {
        val courseId = _uiState.value.contentCourseId ?: return
        if (title.isBlank()) {
            _uiState.value = _uiState.value.copy(message = "章节标题不能为空")
            return
        }
        viewModelScope.launch {
            runCatching { courseRepo.createChapter(courseId, title.trim(), description, parentId, sortOrder) }
                .onSuccess {
                    _uiState.value = _uiState.value.copy(message = "章节已创建")
                    loadContent(courseId)
                    loadHome()
                }
                .onFailure { _uiState.value = _uiState.value.copy(message = it.message ?: "创建章节失败") }
        }
    }

    fun createExercise(
        chapterId: String?,
        title: String,
        questionContent: String,
        type: Int,
        options: String?,
        answer: String,
        questionAnalysis: String?,
        difficulty: Int,
        score: Int
    ) {
        val courseId = _uiState.value.contentCourseId ?: return
        if (title.isBlank() || questionContent.isBlank()) {
            _uiState.value = _uiState.value.copy(message = "题目标题和题干不能为空")
            return
        }
        viewModelScope.launch {
            runCatching {
                courseRepo.createExercise(
                    courseId, chapterId, title.trim(), questionContent.trim(), type,
                    options, answer, questionAnalysis, difficulty, score
                )
            }.onSuccess {
                _uiState.value = _uiState.value.copy(message = "习题已创建")
                loadContent(courseId)
                loadHome()
            }.onFailure { _uiState.value = _uiState.value.copy(message = it.message ?: "创建习题失败") }
        }
    }

    fun createResource(name: String, description: String?, content: String?) {
        val courseId = _uiState.value.contentCourseId ?: return
        if (name.isBlank()) {
            _uiState.value = _uiState.value.copy(message = "资源名称不能为空")
            return
        }
        viewModelScope.launch {
            runCatching { courseRepo.createKnowledgeResource(courseId, null, name.trim(), description, content) }
                .onSuccess {
                    _uiState.value = _uiState.value.copy(message = "资源已创建")
                    loadContent(courseId)
                    loadHome()
                }
                .onFailure { _uiState.value = _uiState.value.copy(message = it.message ?: "创建资源失败") }
        }
    }

    fun setAiCourseId(courseId: String) {
        _uiState.value = _uiState.value.copy(aiCourseId = courseId)
    }

    fun generateExercises(count: Int, type: Int, difficulty: Int, topicHint: String, customPrompt: String) {
        val courseId = _uiState.value.aiCourseId
            ?: _uiState.value.contentCourseId
            ?: _uiState.value.courses.firstOrNull()?.id
            ?: run {
                _uiState.value = _uiState.value.copy(message = "请先选择或创建课程")
                return
            }
        viewModelScope.launch {
            _uiState.value = _uiState.value.copy(aiBusy = true, aiText = "", aiGeneratedExercises = emptyList(), message = null)
            runCatching { aiRepo.generateExercises(courseId, type, count, difficulty, topicHint, customPrompt) }
                .onSuccess {
                    _uiState.value = _uiState.value.copy(aiBusy = false, aiGeneratedExercises = it)
                }
                .onFailure {
                    _uiState.value = _uiState.value.copy(
                        aiBusy = false,
                        message = it.message ?: "AI 出题失败"
                    )
                }
        }
    }

    fun generateLessonPlan(topic: String, subject: String, grade: String, duration: Int, customPrompt: String) {
        if (topic.isBlank()) {
            _uiState.value = _uiState.value.copy(message = "请输入备课主题")
            return
        }
        viewModelScope.launch {
            _uiState.value = _uiState.value.copy(aiBusy = true, aiText = "", aiGeneratedExercises = emptyList(), message = null)
            runCatching { aiRepo.generateLessonPlan(topic.trim(), subject, grade, duration, customPrompt) }
                .onSuccess { _uiState.value = _uiState.value.copy(aiBusy = false, aiText = it) }
                .onFailure {
                    _uiState.value = _uiState.value.copy(aiBusy = false, message = it.message ?: "AI 备课失败")
                }
        }
    }

    fun aiChat(message: String) {
        if (message.isBlank()) {
            _uiState.value = _uiState.value.copy(message = "请输入问题")
            return
        }
        viewModelScope.launch {
            _uiState.value = _uiState.value.copy(aiBusy = true, aiText = "", aiGeneratedExercises = emptyList(), message = null)
            runCatching { aiRepo.chat(message.trim()) }
                .onSuccess { _uiState.value = _uiState.value.copy(aiBusy = false, aiText = it) }
                .onFailure {
                    _uiState.value = _uiState.value.copy(aiBusy = false, message = it.message ?: "AI 助手暂时不可用")
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
                _uiState.value = _uiState.value.copy(message = it.message ?: "加载学情失败")
            }
        }
    }

    fun clearSelection() {
        _uiState.value = _uiState.value.copy(selectedCourseId = null, overview = null, students = emptyList())
    }

    fun clearMessage() {
        _uiState.value = _uiState.value.copy(message = null)
    }
}
