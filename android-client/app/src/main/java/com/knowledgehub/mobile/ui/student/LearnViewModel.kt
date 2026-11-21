package com.knowledgehub.mobile.ui.student

import androidx.lifecycle.ViewModel
import androidx.lifecycle.viewModelScope
import com.knowledgehub.mobile.data.api.ApiClient
import com.knowledgehub.mobile.data.model.*
import com.knowledgehub.mobile.data.repository.CourseRepository
import kotlinx.coroutines.delay
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.StateFlow
import kotlinx.coroutines.flow.asStateFlow
import kotlinx.coroutines.launch

/** 资源在线预览类型（与 Web FilePreviewComponent 的 EXTENSION_MAP 口径一致） */
enum class PreviewKind { TEXT, IMAGE, VIDEO, AUDIO, PDF, OFFICE, UNSUPPORTED }

data class ResourcePreviewState(
    val resource: KnowledgeResourceDto? = null,
    val kind: PreviewKind = PreviewKind.UNSUPPORTED,
    val url: String? = null,
    val text: String? = null,
    val pdfBytes: ByteArray? = null,
    val videoBytes: ByteArray? = null,
    val isLoading: Boolean = false,
    val notice: String? = null,
    val error: String? = null
)

data class LearnUiState(
    val isLoading: Boolean = false,
    val detail: CourseDetailDto? = null,
    val exerciseProgress: CourseExerciseProgress? = null,
    // 当前章节内容
    val currentChapterId: String? = null,
    val currentChapterTitle: String = "",
    val exercises: List<ExerciseDto> = emptyList(),
    val records: List<ExerciseRecordDto> = emptyList(),
    val contentLoading: Boolean = false,
    // 答案与提交
    val selectedAnswers: Map<String, String> = emptyMap(),
    val submittedAnswers: Map<String, String> = emptyMap(),
    // 资源预览
    val preview: ResourcePreviewState? = null,
    val message: String? = null
)

class LearnViewModel(
    private val courseRepo: CourseRepository
) : ViewModel() {

    private val _uiState = MutableStateFlow(LearnUiState())
    val uiState: StateFlow<LearnUiState> = _uiState.asStateFlow()

    /** 加载课程详情（含章节与资源）+ 习题进度 */
    fun load(courseId: String) {
        viewModelScope.launch {
            _uiState.value = _uiState.value.copy(isLoading = true)
            runCatching {
                val detail = courseRepo.getDetail(courseId)
                val progress = runCatching { courseRepo.getExerciseProgress(courseId) }.getOrNull()
                _uiState.value = _uiState.value.copy(
                    isLoading = false,
                    detail = detail,
                    exerciseProgress = progress
                )
            }.onFailure {
                _uiState.value = _uiState.value.copy(isLoading = false, message = it.message ?: "加载失败")
            }
        }
    }

    /** 选中章节：加载习题 + 该章提交记录 */
    fun selectChapter(courseId: String, chapterId: String, title: String) {
        _uiState.value = _uiState.value.copy(
            currentChapterId = chapterId,
            currentChapterTitle = title,
            exercises = emptyList(),
            records = emptyList(),
            selectedAnswers = emptyMap(),
            submittedAnswers = emptyMap(),
            preview = null,
            contentLoading = true,
            message = null
        )
        viewModelScope.launch {
            runCatching {
                val exercises = courseRepo.getExercises(chapterId)
                val records = courseRepo.getExerciseRecords(courseId, chapterId)
                val submitted = records.associate { it.exerciseId.orEmpty() to normalizeStoredAnswer(it.studentAnswer, it.exerciseType) }
                _uiState.value = _uiState.value.copy(
                    exercises = exercises,
                    records = records,
                    submittedAnswers = submitted,
                    contentLoading = false
                )
            }.onFailure {
                _uiState.value = _uiState.value.copy(contentLoading = false, message = it.message ?: "加载章节内容失败")
            }
        }
    }

    fun chooseAnswer(exerciseId: String, answer: String, multiSelect: Boolean = false) {
        val current = _uiState.value.selectedAnswers.toMutableMap()
        if (multiSelect) {
            val existing = current[exerciseId].orEmpty()
                .split(",").map { it.trim() }.filter { it.isNotBlank() }.toMutableSet()
            if (existing.contains(answer)) existing.remove(answer) else existing.add(answer)
            current[exerciseId] = existing.sorted().joinToString(",")
        } else {
            current[exerciseId] = answer
        }
        _uiState.value = _uiState.value.copy(selectedAnswers = current)
    }

    /** 重新作答：把历史答案回填到输入框，并解除“已提交”锁定 */
    fun editAgain(exercise: ExerciseDto) {
        val record = _uiState.value.records.find { it.exerciseId == exercise.id }
        val answer = normalizeStoredAnswer(record?.studentAnswer, exercise.type)
        val selected = _uiState.value.selectedAnswers.toMutableMap()
        selected[exercise.id] = answer
        val submitted = _uiState.value.submittedAnswers.toMutableMap()
        submitted.remove(exercise.id)
        _uiState.value = _uiState.value.copy(
            selectedAnswers = selected,
            submittedAnswers = submitted,
            message = "可重新作答，提交后会更新记录"
        )
    }

    /** 提交答案并刷新记录与进度 */
    fun submit(courseId: String, chapterId: String, exercise: ExerciseDto) {
        var answer = _uiState.value.selectedAnswers[exercise.id]?.trim()
        if (answer.isNullOrBlank()) {
            _uiState.value = _uiState.value.copy(message = "请先选择或输入答案")
            return
        }
        answer = normalizeStoredAnswer(answer, exercise.type)
        viewModelScope.launch {
            runCatching {
                courseRepo.submitAnswer(courseId, chapterId, exercise.id, answer!!)
                val records = courseRepo.getExerciseRecords(courseId, chapterId)
                val progress = courseRepo.getExerciseProgress(courseId)
                val submitted = records.associate { it.exerciseId.orEmpty() to normalizeStoredAnswer(it.studentAnswer, it.exerciseType) }
                _uiState.value = _uiState.value.copy(
                    records = records,
                    submittedAnswers = submitted,
                    exerciseProgress = progress,
                    message = "已提交，可查看结果"
                )
            }.onFailure {
                _uiState.value = _uiState.value.copy(message = it.message ?: "提交失败")
            }
        }
    }

    /**
     * 打开资源预览：记录学习行为，按文件类型走不同预览方式。
     * 文本直接取内容；图片/音视频/PDF 走带 access_token 的 WebView；
     * Office 等后端转 PDF 的资源先轮询 preview-pdf-info，就绪后加载 preview-pdf。
     */
    fun openPreview(courseId: String, chapterId: String, res: KnowledgeResourceDto) {
        val rid = res.resourceId?.takeIf { it.isNotBlank() }
        if (rid == null) {
            _uiState.value = _uiState.value.copy(message = "该资源没有关联文件，暂不能预览")
            return
        }
        viewModelScope.launch {
            _uiState.value = _uiState.value.copy(
                preview = ResourcePreviewState(
                    resource = res,
                    kind = detectResourceKind(res),
                    isLoading = true,
                    notice = "正在准备预览…"
                )
            )
            runCatching {
                // 学习行为只记录一次，不改变习题完成比例
                courseRepo.recordResourceStudy(courseId, chapterId, rid)

                var effective = res
                var kind = detectResourceKind(effective)
                // 旧资源可能没有扩展名：从资源详情接口回填
                if (kind == PreviewKind.UNSUPPORTED && effective.fileExtension.isNullOrBlank()) {
                    val info = courseRepo.getResourceInfo(rid)
                    if (info != null) {
                        effective = effective.copy(
                            fileExtension = info.fileExtension ?: effective.fileExtension,
                            originalFileName = info.originalFileName ?: effective.originalFileName,
                            fileSize = info.fileSize ?: effective.fileSize
                        )
                        kind = detectResourceKind(effective)
                    }
                }

                when (kind) {
                    PreviewKind.TEXT -> {
                        val text = courseRepo.getResourceText(rid)
                        _uiState.value = _uiState.value.copy(
                            preview = ResourcePreviewState(resource = effective, kind = kind, text = text)
                        )
                    }
                    PreviewKind.OFFICE -> {
                        _uiState.value = _uiState.value.copy(
                            preview = ResourcePreviewState(
                                resource = effective,
                                kind = kind,
                                isLoading = true,
                                notice = "正在准备文档预览…"
                            )
                        )
                        var ready = false
                        for (i in 0 until 20) {
                            if (courseRepo.getPreviewPdfReady(rid) == true) {
                                ready = true
                                break
                            }
                            delay(1000)
                        }
                        if (ready) {
                            val bytes = courseRepo.getPreviewBytes(rid, "preview-pdf")
                            _uiState.value = _uiState.value.copy(
                                preview = ResourcePreviewState(
                                    resource = effective,
                                    kind = kind,
                                    url = ApiClient.resourceFileUrl(rid, "preview-pdf"),
                                    pdfBytes = bytes
                                )
                            )
                        } else {
                            _uiState.value = _uiState.value.copy(
                                preview = ResourcePreviewState(
                                    resource = effective,
                                    kind = kind,
                                    error = "文档正在转换，请稍后再试"
                                )
                            )
                        }
                    }
                    PreviewKind.PDF -> {
                        val bytes = courseRepo.getPreviewBytes(rid, "preview")
                        _uiState.value = _uiState.value.copy(
                            preview = ResourcePreviewState(
                                resource = effective,
                                kind = kind,
                                url = ApiClient.resourceFileUrl(rid, "preview"),
                                pdfBytes = bytes
                            )
                        )
                    }
                    PreviewKind.VIDEO, PreviewKind.AUDIO -> {
                        val bytes = courseRepo.getPreviewBytes(rid, "preview")
                        _uiState.value = _uiState.value.copy(
                            preview = ResourcePreviewState(
                                resource = effective,
                                kind = kind,
                                url = ApiClient.resourceFileUrl(rid, "preview"),
                                videoBytes = bytes
                            )
                        )
                    }
                    PreviewKind.IMAGE -> {
                        _uiState.value = _uiState.value.copy(
                            preview = ResourcePreviewState(
                                resource = effective,
                                kind = kind,
                                url = ApiClient.resourceFileUrl(rid, "preview")
                            )
                        )
                    }
                    PreviewKind.UNSUPPORTED -> {
                        _uiState.value = _uiState.value.copy(
                            preview = ResourcePreviewState(
                                resource = effective,
                                kind = kind,
                                error = "暂不支持在线预览该类型文件"
                            )
                        )
                    }
                }
            }.onFailure {
                _uiState.value = _uiState.value.copy(
                    preview = ResourcePreviewState(resource = res, error = it.message ?: "预览加载失败")
                )
            }
        }
    }

    fun closePreview() {
        _uiState.value = _uiState.value.copy(preview = null)
    }

    fun clearMessage() {
        _uiState.value = _uiState.value.copy(message = null)
    }

    /** 判断题的历史答案可能是 A/B、true/false、正确/错误，统一成后端口径；其他题型原样保留 */
    private fun normalizeStoredAnswer(answer: String?, exerciseType: Int): String {
        val raw = answer?.trim()?.takeIf { it.isNotBlank() } ?: return ""
        if (exerciseType != 2) return raw
        return when (raw.lowercase()) {
            "a", "true", "正确" -> "true"
            "b", "false", "错误" -> "false"
            else -> raw
        }
    }
}

/** 从 fileExtension / originalFileName / name 推断文件类型 */
fun detectResourceKind(res: KnowledgeResourceDto): PreviewKind {
    val ext = res.fileExtension?.trim()?.removePrefix(".")?.lowercase()?.takeIf { it.isNotBlank() }
        ?: listOfNotNull(res.originalFileName, res.content, res.name)
            .firstNotNullOfOrNull { name ->
                val dot = name.lastIndexOf('.')
                if (dot >= 0 && dot < name.length - 1) {
                    name.substring(dot + 1).trim().lowercase().takeIf { it.isNotBlank() }
                } else null
            }
        ?: return PreviewKind.UNSUPPORTED

    return when (ext) {
        "txt", "json", "xml", "md", "log", "csv", "tsv", "js", "ts", "css", "html", "htm",
        "yml", "yaml", "ini", "cfg", "conf", "sh", "bat", "py", "java", "c", "cpp", "h",
        "cs", "go", "rs", "sql" -> PreviewKind.TEXT
        "jpg", "jpeg", "png", "gif", "bmp", "webp", "svg" -> PreviewKind.IMAGE
        "mp4", "webm", "avi", "mov" -> PreviewKind.VIDEO
        "mp3", "wav", "ogg", "flac" -> PreviewKind.AUDIO
        "pdf" -> PreviewKind.PDF
        "doc", "docx", "dotx", "ppt", "pptx", "potx" -> PreviewKind.OFFICE
        else -> PreviewKind.UNSUPPORTED
    }
}
