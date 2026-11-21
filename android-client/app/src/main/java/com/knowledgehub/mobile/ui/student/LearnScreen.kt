package com.knowledgehub.mobile.ui.student

import android.content.Intent
import android.graphics.Bitmap
import android.graphics.Color as AndroidColor
import android.graphics.pdf.PdfRenderer
import android.net.Uri
import android.net.http.SslError
import android.os.ParcelFileDescriptor
import android.view.ViewGroup
import android.webkit.SslErrorHandler
import android.widget.FrameLayout
import android.widget.MediaController
import android.widget.VideoView
import android.webkit.WebView
import android.webkit.WebViewClient
import androidx.compose.foundation.background
import androidx.compose.foundation.border
import androidx.compose.foundation.clickable
import androidx.compose.foundation.Image
import androidx.compose.foundation.layout.*
import androidx.compose.foundation.lazy.LazyColumn
import androidx.compose.foundation.lazy.items
import androidx.compose.foundation.lazy.itemsIndexed
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.filled.*
import androidx.compose.material3.*
import androidx.compose.runtime.*
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.draw.clip
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.graphics.asImageBitmap
import androidx.compose.ui.layout.ContentScale
import androidx.compose.ui.platform.LocalContext
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.text.style.TextOverflow
import androidx.compose.ui.unit.dp
import androidx.compose.ui.unit.sp
import androidx.compose.ui.viewinterop.AndroidView
import androidx.compose.ui.window.Dialog
import androidx.compose.ui.window.DialogProperties
import com.knowledgehub.mobile.data.api.ApiClient
import com.knowledgehub.mobile.data.model.*
import com.knowledgehub.mobile.theme.*
import com.knowledgehub.mobile.ui.common.BrandTopBarWithBack
import kotlinx.serialization.json.Json
import kotlinx.serialization.json.JsonArray
import kotlinx.serialization.json.JsonObject
import kotlinx.serialization.json.JsonPrimitive
import java.io.File
import java.util.UUID
import kotlin.math.min

private data class FlatChapter(
    val chapter: ChapterDto,
    val depth: Int,
    val hasChildren: Boolean,
    val visibleChildCount: Int
)

/** 解析习题选项：兼容 JSON 数组、JSON 对象、换行文本；判断题直接生成“正确/错误” */
private fun parseOptions(raw: String?, type: Int): List<Pair<String, String>> {
    if (type == 2) return listOf("true" to "正确", "false" to "错误")
    if (raw.isNullOrBlank()) return emptyList()

    runCatching {
        when (val element = Json.parseToJsonElement(raw)) {
            is JsonArray -> {
                return element.mapIndexed { idx, item ->
                    val text = when (item) {
                        is JsonPrimitive -> item.content
                        is JsonObject -> (item["content"] ?: item["text"] ?: item["value"])
                            ?.toString()?.trim('"') ?: item.toString()
                        else -> item.toString()
                    }.trim()
                    val explicitKey = if (item is JsonObject) {
                        item["key"]?.toString()?.trim('"')?.uppercase()?.takeIf { it.length == 1 }
                    } else null
                    val key = explicitKey
                        ?: if (text.length >= 2 && text[1] == '.') text[0].uppercase()
                        else ('A' + idx).toString()
                    val display = if (text.length >= 2 && text[1] == '.') text.substring(2).trim() else text
                    key to display
                }
            }
            is JsonObject -> {
                return element.entries.mapIndexed { idx, (k, v) ->
                    val key = k.trim().uppercase().takeIf { it.length == 1 } ?: ('A' + idx).toString()
                    key to v.toString().trim('"').trim()
                }
            }
            else -> Unit
        }
    }

    return raw.lines().mapIndexedNotNull { idx, line ->
        val text = line.trim()
        if (text.isBlank()) return@mapIndexedNotNull null
        if (text.length >= 2 && text[1] == '.') {
            text[0].uppercase() to text.substring(2).trim()
        } else {
            ('A' + idx).toString() to text
        }
    }
}

private fun isAnswerChosen(answer: String?, key: String, type: Int): Boolean {
    if (answer.isNullOrBlank()) return false
    val parts = answer.split(",").map { it.trim().lowercase() }
    if (type == 2) {
        val value = when (parts.firstOrNull()) {
            "a", "true", "正确" -> "true"
            "b", "false", "错误" -> "false"
            else -> parts.firstOrNull()
        }
        return value == key.lowercase()
    }
    return parts.contains(key.lowercase())
}

private fun exerciseTypeLabel(type: Int) = when (type) {
    0 -> "单选题"
    1 -> "多选题"
    2 -> "判断题"
    3 -> "填空题"
    4 -> "简答题"
    else -> "习题"
}

private fun formatBoolAnswer(answer: String?): String = when (answer?.trim()?.lowercase()) {
    "true", "a", "正确" -> "正确"
    "false", "b", "错误" -> "错误"
    else -> answer?.takeIf { it.isNotBlank() } ?: "—"
}

private fun previewKindLabel(kind: PreviewKind) = when (kind) {
    PreviewKind.TEXT -> "文本"
    PreviewKind.IMAGE -> "图片"
    PreviewKind.VIDEO -> "视频"
    PreviewKind.AUDIO -> "音频"
    PreviewKind.PDF -> "PDF"
    PreviewKind.OFFICE -> "Office"
    PreviewKind.UNSUPPORTED -> "文件"
}

private fun formatFileSize(size: Long?): String {
    if (size == null) return "大小未知"
    if (size < 1024) return "$size B"
    if (size < 1024 * 1024) return "%.1f KB".format(size / 1024.0)
    return "%.1f MB".format(size / (1024.0 * 1024.0))
}

private fun findChapter(chapters: List<ChapterDto>, id: String): ChapterDto? {
    for (chapter in chapters) {
        if (chapter.id == id) return chapter
        findChapter(chapter.children, id)?.let { return it }
    }
    return null
}

@OptIn(ExperimentalMaterial3Api::class)
@Composable
fun LearnScreen(
    courseId: String,
    viewModel: LearnViewModel,
    onBack: () -> Unit
) {
    val state by viewModel.uiState.collectAsState()
    var selectedChapterId by remember { mutableStateOf<String?>(null) }

    LaunchedEffect(courseId) { viewModel.load(courseId) }

    Box(Modifier.fillMaxSize()) {
        when {
            state.isLoading && state.detail == null -> {
                Scaffold(topBar = { BrandTopBarWithBack(title = "章节学习", onBack = onBack) }) { padding ->
                    Box(Modifier.fillMaxSize().padding(padding), Alignment.Center) {
                        CircularProgressIndicator()
                    }
                }
            }
            state.detail == null -> {
                Scaffold(topBar = { BrandTopBarWithBack(title = "章节学习", onBack = onBack) }) { padding ->
                    Box(Modifier.fillMaxSize().padding(padding), Alignment.Center) {
                        Text(state.message ?: "课程加载失败", color = TextSecondary)
                    }
                }
            }
            else -> {
                val detail = state.detail!!
                val chapter = selectedChapterId?.let { findChapter(detail.chapters, it) }
                if (chapter != null) {
                    ChapterStudyScreen(
                        state = state,
                        chapter = chapter,
                        courseId = courseId,
                        viewModel = viewModel,
                        onBackToList = { selectedChapterId = null }
                    )
                } else {
                    ChapterListScreen(
                        detail = detail,
                        progress = state.exerciseProgress,
                        onBack = onBack,
                        onOpenChapter = { id, title ->
                            selectedChapterId = id
                            viewModel.selectChapter(courseId, id, title)
                        }
                    )
                }
            }
        }

        state.message?.let { msg ->
            val snackbar = remember { SnackbarHostState() }
            LaunchedEffect(msg) {
                snackbar.showSnackbar(msg)
                viewModel.clearMessage()
            }
            SnackbarHost(snackbar, Modifier.align(Alignment.BottomCenter))
        }

        state.preview?.let { preview ->
            ResourcePreviewDialog(preview = preview, onClose = viewModel::closePreview)
        }
    }
}

@OptIn(ExperimentalMaterial3Api::class)
@Composable
private fun ChapterListScreen(
    detail: CourseDetailDto,
    progress: CourseExerciseProgress?,
    onBack: () -> Unit,
    onOpenChapter: (String, String) -> Unit
) {
    var keyword by remember(detail.id) { mutableStateOf("") }
    var expanded by remember(detail.id) { mutableStateOf<Set<String>>(emptySet()) }

    LaunchedEffect(detail.id) {
        expanded = setOfNotNull(detail.chapters.firstOrNull()?.id)
    }

    val flat = remember(detail, expanded, keyword) {
        flattenChapters(detail.chapters, expanded, keyword)
    }

    Scaffold(
        topBar = { BrandTopBarWithBack(title = "章节目录", onBack = onBack) }
    ) { padding ->
        LazyColumn(
            modifier = Modifier.fillMaxSize().background(SurfaceBg).padding(padding),
            contentPadding = PaddingValues(16.dp),
            verticalArrangement = Arrangement.spacedBy(12.dp)
        ) {
            item {
                Card(
                    modifier = Modifier.fillMaxWidth(),
                    shape = RoundedCornerShape(14.dp),
                    colors = CardDefaults.cardColors(containerColor = Color.White)
                ) {
                    Column(Modifier.padding(16.dp)) {
                        Text(detail.title, fontSize = 17.sp, fontWeight = FontWeight.Bold)
                        if (!detail.description.isNullOrBlank()) {
                            Spacer(Modifier.height(6.dp))
                            Text(
                                detail.description,
                                fontSize = 12.sp,
                                color = TextSecondary,
                                maxLines = 2,
                                overflow = TextOverflow.Ellipsis
                            )
                        }
                        Spacer(Modifier.height(12.dp))
                        Text(
                            if (progress == null) "按章节学习资源和习题"
                            else "习题进度 ${progress.completedCount}/${progress.totalCount}（按已提交习题比例计算）",
                            fontSize = 12.sp,
                            color = BrandBlue,
                            fontWeight = FontWeight.Medium
                        )
                    }
                }
            }

            item {
                OutlinedTextField(
                    value = keyword,
                    onValueChange = { keyword = it },
                    modifier = Modifier.fillMaxWidth(),
                    placeholder = { Text("搜索章节", fontSize = 13.sp) },
                    leadingIcon = { Icon(Icons.Filled.Search, null, tint = TextSecondary) },
                    singleLine = true,
                    shape = RoundedCornerShape(10.dp)
                )
            }

            item {
                Text("全部章节", fontWeight = FontWeight.Bold, fontSize = 15.sp)
            }

            if (flat.isEmpty()) {
                item {
                    EmptyCard(
                        title = "没有匹配的章节",
                        desc = if (keyword.isBlank()) "课程暂未配置章节" else "换个关键词试试"
                    )
                }
            }

            items(flat, key = { it.chapter.id }) { item ->
                ChapterRow(
                    item = item,
                    expanded = expanded.contains(item.chapter.id),
                    onToggle = {
                        expanded = if (expanded.contains(item.chapter.id)) {
                            expanded - item.chapter.id
                        } else {
                            expanded + item.chapter.id
                        }
                    },
                    onOpen = { onOpenChapter(item.chapter.id, item.chapter.title) }
                )
            }
        }
    }
}

private fun flattenChapters(
    nodes: List<ChapterDto>,
    expanded: Set<String>,
    keyword: String
): List<FlatChapter> {
    val result = mutableListOf<FlatChapter>()

    fun matches(chapter: ChapterDto): Boolean {
        if (keyword.isBlank()) return true
        return chapter.title.contains(keyword, ignoreCase = true) ||
            chapter.description?.contains(keyword, ignoreCase = true) == true
    }

    fun countVisible(list: List<ChapterDto>): Int {
        var count = 0
        for (chapter in list) {
            val children = countVisible(chapter.children)
            if (matches(chapter) || children > 0) count += 1 + children
        }
        return count
    }

    fun walk(list: List<ChapterDto>, depth: Int) {
        for (chapter in list) {
            val childCount = countVisible(chapter.children)
            if (!matches(chapter) && childCount == 0) continue
            result.add(
                FlatChapter(
                    chapter = chapter,
                    depth = depth,
                    hasChildren = chapter.children.isNotEmpty(),
                    visibleChildCount = childCount
                )
            )
            if (keyword.isNotBlank() || expanded.contains(chapter.id)) {
                walk(chapter.children, depth + 1)
            }
        }
    }

    walk(nodes, 0)
    return result
}

@Composable
private fun ChapterRow(
    item: FlatChapter,
    expanded: Boolean,
    onToggle: () -> Unit,
    onOpen: () -> Unit
) {
    Card(
        modifier = Modifier
            .fillMaxWidth()
            .clickable(onClick = onOpen),
        shape = RoundedCornerShape(10.dp),
        colors = CardDefaults.cardColors(containerColor = Color.White),
        elevation = CardDefaults.cardElevation(defaultElevation = 1.dp)
    ) {
        Row(
            Modifier
                .fillMaxWidth()
                .padding(start = (12 + item.depth * 18).dp, end = 12.dp, top = 6.dp, bottom = 6.dp),
            verticalAlignment = Alignment.CenterVertically
        ) {
            if (item.hasChildren) {
                Box(
                    Modifier
                        .size(32.dp)
                        .clip(RoundedCornerShape(8.dp))
                        .clickable(onClick = onToggle),
                    Alignment.Center
                ) {
                    Icon(
                        if (expanded) Icons.Filled.ExpandLess else Icons.Filled.ExpandMore,
                        contentDescription = if (expanded) "收起子章节" else "展开子章节",
                        tint = BrandBlue,
                        modifier = Modifier.size(20.dp)
                    )
                }
            } else {
                Spacer(Modifier.size(32.dp))
            }
            Spacer(Modifier.width(4.dp))
            Column(Modifier.weight(1f)) {
                Text(
                    item.chapter.title,
                    fontSize = 14.sp,
                    fontWeight = if (item.depth == 0) FontWeight.Bold else FontWeight.Medium,
                    maxLines = 2,
                    overflow = TextOverflow.Ellipsis
                )
                val resourceCount = countResources(item.chapter)
                val meta = buildList {
                    add("$resourceCount 个资源")
                    if (item.hasChildren) add("${item.chapter.children.size} 个子章节")
                    if (!item.chapter.description.isNullOrBlank()) add(item.chapter.description!!)
                }.joinToString(" · ")
                Text(meta, fontSize = 11.sp, color = TextSecondary, maxLines = 1, overflow = TextOverflow.Ellipsis)
            }
            Icon(Icons.Filled.ChevronRight, null, tint = TextSecondary, modifier = Modifier.size(20.dp))
        }
    }
}

private fun countResources(chapter: ChapterDto): Int =
    chapter.knowledgeResources.size + chapter.children.sumOf { countResources(it) }

@OptIn(ExperimentalMaterial3Api::class)
@Composable
private fun ChapterStudyScreen(
    state: LearnUiState,
    chapter: ChapterDto,
    courseId: String,
    viewModel: LearnViewModel,
    onBackToList: () -> Unit
) {
    var tab by remember(chapter.id) { mutableIntStateOf(0) }
    val tabs = listOf("学习资源", "章节习题", "提交记录")

    Scaffold(
        topBar = {
            BrandTopBarWithBack(
                title = state.currentChapterTitle.ifBlank { chapter.title },
                onBack = onBackToList
            )
        }
    ) { padding ->
        Column(
            Modifier
                .fillMaxSize()
                .background(SurfaceBg)
                .padding(padding)
        ) {
            Card(
                modifier = Modifier.fillMaxWidth().padding(horizontal = 16.dp, vertical = 12.dp),
                shape = RoundedCornerShape(12.dp),
                colors = CardDefaults.cardColors(containerColor = Color.White)
            ) {
                Row(Modifier.fillMaxWidth().padding(14.dp), verticalAlignment = Alignment.CenterVertically) {
                    Box(
                        Modifier.size(40.dp).background(BrandBlueLight, RoundedCornerShape(10.dp)),
                        Alignment.Center
                    ) {
                        Icon(Icons.Filled.MenuBook, null, tint = BrandBlue, modifier = Modifier.size(22.dp))
                    }
                    Spacer(Modifier.width(12.dp))
                    Column(Modifier.weight(1f)) {
                        Text(chapter.title, fontWeight = FontWeight.Bold, fontSize = 15.sp)
                        Text(
                            "${chapter.knowledgeResources.size} 个资源 · ${state.exercises.size} 道习题",
                            fontSize = 11.sp,
                            color = TextSecondary
                        )
                    }
                }
            }

            TabRow(
                selectedTabIndex = tab,
                containerColor = Color.White,
                contentColor = BrandBlue
            ) {
                tabs.forEachIndexed { index, title ->
                    Tab(
                        selected = tab == index,
                        onClick = { tab = index },
                        text = { Text(title, fontSize = 13.sp) },
                        selectedContentColor = BrandBlue,
                        unselectedContentColor = TextSecondary
                    )
                }
            }

            when (tab) {
                0 -> ResourcePane(
                    resources = chapter.knowledgeResources,
                    loading = state.contentLoading,
                    courseId = courseId,
                    chapterId = chapter.id,
                    onPreview = { res -> viewModel.openPreview(courseId, chapter.id, res) }
                )
                1 -> ExercisePane(
                    state = state,
                    courseId = courseId,
                    chapterId = chapter.id,
                    viewModel = viewModel
                )
                2 -> RecordPane(state.records)
            }
        }
    }
}

@Composable
private fun ResourcePane(
    resources: List<KnowledgeResourceDto>,
    loading: Boolean,
    courseId: String,
    chapterId: String,
    onPreview: (KnowledgeResourceDto) -> Unit
) {
    LazyColumn(
        modifier = Modifier.fillMaxSize(),
        contentPadding = PaddingValues(16.dp),
        verticalArrangement = Arrangement.spacedBy(12.dp)
    ) {
        item { Text("点击资源卡片即可在线预览", fontSize = 12.sp, color = TextSecondary) }
        if (loading) {
            item { Box(Modifier.fillMaxWidth().height(100.dp), Alignment.Center) { CircularProgressIndicator() } }
        } else if (resources.isEmpty()) {
            item { EmptyCard("本章节暂无资源", "教师上传资源后，学生可在这里直接预览学习") }
        }
        items(resources, key = { it.id }) { res ->
            MobileResourceCard(res, onPreview = { onPreview(res) })
        }
    }
}

@Composable
private fun MobileResourceCard(res: KnowledgeResourceDto, onPreview: () -> Unit) {
    val kind = detectResourceKind(res)
    Card(
        modifier = Modifier.fillMaxWidth(),
        shape = RoundedCornerShape(12.dp),
        colors = CardDefaults.cardColors(containerColor = Color.White),
        elevation = CardDefaults.cardElevation(defaultElevation = 1.dp)
    ) {
        Column(Modifier.padding(14.dp)) {
            Row(verticalAlignment = Alignment.CenterVertically) {
                Box(
                    Modifier.size(38.dp).background(BrandBlueLight, RoundedCornerShape(9.dp)),
                    Alignment.Center
                ) {
                    Icon(Icons.Filled.Article, null, tint = BrandBlue, modifier = Modifier.size(20.dp))
                }
                Spacer(Modifier.width(10.dp))
                Column(Modifier.weight(1f)) {
                    Text(
                        res.originalFileName?.takeIf { it.isNotBlank() } ?: res.name,
                        fontSize = 14.sp,
                        fontWeight = FontWeight.Medium,
                        maxLines = 2,
                        overflow = TextOverflow.Ellipsis
                    )
                    Text(
                        "${previewKindLabel(kind)} · ${formatFileSize(res.fileSize)}",
                        fontSize = 11.sp,
                        color = TextSecondary
                    )
                }
            }
            if (!res.description.isNullOrBlank()) {
                Spacer(Modifier.height(10.dp))
                Text(res.description, fontSize = 12.sp, color = TextSecondary, maxLines = 3)
            }
            Spacer(Modifier.height(12.dp))
            Button(
                onClick = onPreview,
                modifier = Modifier.fillMaxWidth().height(40.dp),
                shape = RoundedCornerShape(8.dp),
                colors = ButtonDefaults.buttonColors(containerColor = BrandBlue)
            ) {
                Icon(Icons.Filled.Visibility, null, modifier = Modifier.size(17.dp))
                Spacer(Modifier.width(6.dp))
                Text("在线预览", fontSize = 13.sp)
            }
        }
    }
}

@Composable
private fun ExercisePane(
    state: LearnUiState,
    courseId: String,
    chapterId: String,
    viewModel: LearnViewModel
) {
    LazyColumn(
        modifier = Modifier.fillMaxSize(),
        contentPadding = PaddingValues(16.dp),
        verticalArrangement = Arrangement.spacedBy(12.dp)
    ) {
        item {
            Text(
                if (state.exercises.isEmpty()) "本章节暂无习题"
                else "共 ${state.exercises.size} 道题 · 提交后自动计入学习进度",
                fontSize = 12.sp,
                color = TextSecondary
            )
        }
        if (state.contentLoading) {
            item { Box(Modifier.fillMaxWidth().height(100.dp), Alignment.Center) { CircularProgressIndicator() } }
        } else if (state.exercises.isEmpty()) {
            item { EmptyCard("本章节暂无习题", "教师配置习题后，可在这里作答并查看解析") }
        }
        itemsIndexed(state.exercises, key = { _, ex -> ex.id }) { index, ex ->
            MobileExerciseCard(
                index = index + 1,
                exercise = ex,
                selected = state.selectedAnswers[ex.id],
                submitted = state.submittedAnswers[ex.id],
                record = state.records.find { it.exerciseId == ex.id },
                onChoose = { key -> viewModel.chooseAnswer(ex.id, key, multiSelect = ex.type == 1) },
                onTextChange = { text -> viewModel.chooseAnswer(ex.id, text) },
                onSubmit = { viewModel.submit(courseId, chapterId, ex) },
                onEditAgain = { viewModel.editAgain(ex) }
            )
        }
    }
}

@Composable
private fun MobileExerciseCard(
    index: Int,
    exercise: ExerciseDto,
    selected: String?,
    submitted: String?,
    record: ExerciseRecordDto?,
    onChoose: (String) -> Unit,
    onTextChange: (String) -> Unit,
    onSubmit: () -> Unit,
    onEditAgain: () -> Unit
) {
    val options = remember(exercise.options, exercise.type) { parseOptions(exercise.options, exercise.type) }
    val isDone = submitted != null
    val isTextAnswer = exercise.type == 3 || exercise.type == 4 || options.isEmpty()

    Card(
        modifier = Modifier.fillMaxWidth(),
        shape = RoundedCornerShape(12.dp),
        colors = CardDefaults.cardColors(containerColor = Color.White),
        elevation = CardDefaults.cardElevation(defaultElevation = 1.dp)
    ) {
        Column(Modifier.padding(16.dp)) {
            Row(verticalAlignment = Alignment.CenterVertically) {
                Text("第 $index 题", fontSize = 12.sp, color = TextSecondary)
                Spacer(Modifier.width(8.dp))
                Text(exerciseTypeLabel(exercise.type), fontSize = 11.sp, color = BrandBlue)
                Spacer(Modifier.weight(1f))
                if (isDone) {
                    Text("已提交", fontSize = 11.sp, color = SuccessGreen, fontWeight = FontWeight.Bold)
                }
            }
            Spacer(Modifier.height(10.dp))
            Text(
                exercise.title.ifBlank { exercise.questionContent }.ifBlank { "（题目内容为空）" },
                fontSize = 15.sp,
                fontWeight = FontWeight.Medium,
                lineHeight = 22.sp
            )
            Spacer(Modifier.height(14.dp))

            if (isTextAnswer) {
                OutlinedTextField(
                    value = selected.orEmpty(),
                    onValueChange = onTextChange,
                    enabled = !isDone,
                    modifier = Modifier.fillMaxWidth().heightIn(min = 96.dp),
                    placeholder = {
                        Text(
                            if (exercise.type == 3) "请输入答案" else "请输入你的解答",
                            fontSize = 13.sp
                        )
                    },
                    shape = RoundedCornerShape(10.dp)
                )
            } else {
                options.forEach { (key, text) ->
                    val chosen = isAnswerChosen(
                        if (isDone) submitted else selected,
                        key,
                        exercise.type
                    )
                    val shape = if (exercise.type == 1) RoundedCornerShape(8.dp) else RoundedCornerShape(22.dp)
                    Row(
                        Modifier
                            .fillMaxWidth()
                            .clip(shape)
                            .background(if (chosen) BrandBlueLight else Color(0xFFF8F9FC))
                            .border(1.dp, if (chosen) BrandBlue else Divider, shape)
                            .clickable(enabled = !isDone) { onChoose(key) }
                            .padding(horizontal = 12.dp, vertical = 12.dp),
                        verticalAlignment = Alignment.CenterVertically
                    ) {
                        Box(
                            Modifier
                                .size(24.dp)
                                .background(if (chosen) BrandBlue else Color.Transparent, shape),
                            Alignment.Center
                        ) {
                            Text(
                                key,
                                color = if (chosen) Color.White else TextSecondary,
                                fontSize = 12.sp,
                                fontWeight = FontWeight.Bold
                            )
                        }
                        Spacer(Modifier.width(12.dp))
                        Text(
                            text,
                            fontSize = 14.sp,
                            color = if (chosen) BrandBlue else TextPrimary,
                            modifier = Modifier.weight(1f)
                        )
                    }
                    Spacer(Modifier.height(8.dp))
                }
            }

            Spacer(Modifier.height(6.dp))
            if (!isDone) {
                Button(
                    onClick = onSubmit,
                    modifier = Modifier.fillMaxWidth().height(44.dp),
                    shape = RoundedCornerShape(9.dp),
                    colors = ButtonDefaults.buttonColors(containerColor = AccentTeal)
                ) {
                    Text("提交答案", fontSize = 14.sp)
                }
            } else {
                val correct = record?.isCorrect == true
                Surface(
                    modifier = Modifier.fillMaxWidth(),
                    shape = RoundedCornerShape(10.dp),
                    color = if (correct) Color(0xFFEFF8F3) else Color(0xFFFFF2F2)
                ) {
                    Column(Modifier.padding(12.dp)) {
                        Text(
                            if (correct) "回答正确" else "回答错误",
                            fontSize = 13.sp,
                            fontWeight = FontWeight.Bold,
                            color = if (correct) SuccessGreen else Color(0xFFD93A3A)
                        )
                        if (!exercise.answer.isNullOrBlank()) {
                            Spacer(Modifier.height(4.dp))
                            Text(
                                "正确答案：" + if (exercise.type == 2) formatBoolAnswer(exercise.answer) else exercise.answer,
                                fontSize = 12.sp,
                                color = TextSecondary
                            )
                        }
                        if (!exercise.questionAnalysis.isNullOrBlank()) {
                            Spacer(Modifier.height(4.dp))
                            Text("解析：${exercise.questionAnalysis}", fontSize = 12.sp, color = TextSecondary)
                        }
                    }
                }
                Spacer(Modifier.height(8.dp))
                TextButton(onClick = onEditAgain, modifier = Modifier.fillMaxWidth()) {
                    Text("重新作答", fontSize = 13.sp)
                }
            }
        }
    }
}

@Composable
private fun RecordPane(records: List<ExerciseRecordDto>) {
    LazyColumn(
        modifier = Modifier.fillMaxSize(),
        contentPadding = PaddingValues(16.dp),
        verticalArrangement = Arrangement.spacedBy(10.dp)
    ) {
        item { Text("共 ${records.size} 条提交记录", fontSize = 12.sp, color = TextSecondary) }
        if (records.isEmpty()) {
            item { EmptyCard("暂无提交记录", "完成习题作答后，记录会显示在这里") }
        }
        items(records, key = { it.id.ifBlank { it.exerciseId ?: it.hashCode().toString() } }) { rec ->
            Card(
                modifier = Modifier.fillMaxWidth(),
                shape = RoundedCornerShape(10.dp),
                colors = CardDefaults.cardColors(containerColor = Color.White)
            ) {
                Row(Modifier.fillMaxWidth().padding(14.dp), verticalAlignment = Alignment.CenterVertically) {
                    Icon(
                        if (rec.isCorrect == true) Icons.Filled.CheckCircle else Icons.Filled.Cancel,
                        null,
                        tint = if (rec.isCorrect == true) SuccessGreen else Color(0xFFD93A3A),
                        modifier = Modifier.size(22.dp)
                    )
                    Spacer(Modifier.width(10.dp))
                    Column(Modifier.weight(1f)) {
                        Text(
                            rec.exerciseTitle?.ifBlank { "习题作答" } ?: "习题作答",
                            fontSize = 13.sp,
                            maxLines = 1,
                            overflow = TextOverflow.Ellipsis
                        )
                        Text(
                            "作答：" + (if (rec.exerciseType == 2) formatBoolAnswer(rec.studentAnswer) else rec.studentAnswer.orEmpty()) +
                                " · ${rec.completedAt?.take(16) ?: ""}",
                            fontSize = 11.sp,
                            color = TextSecondary
                        )
                    }
                }
            }
        }
    }
}

@Composable
private fun EmptyCard(title: String, desc: String) {
    Card(
        modifier = Modifier.fillMaxWidth(),
        shape = RoundedCornerShape(12.dp),
        colors = CardDefaults.cardColors(containerColor = Color.White)
    ) {
        Column(
            Modifier.fillMaxWidth().padding(36.dp),
            horizontalAlignment = Alignment.CenterHorizontally
        ) {
            Icon(Icons.Filled.Inbox, null, tint = TextSecondary, modifier = Modifier.size(36.dp))
            Spacer(Modifier.height(10.dp))
            Text(title, fontSize = 14.sp, fontWeight = FontWeight.Medium)
            Spacer(Modifier.height(4.dp))
            Text(desc, fontSize = 12.sp, color = TextSecondary)
        }
    }
}

private class MediaHolder(
    val file: File,
    val videoView: VideoView
)

/** 音视频用系统播放器本地播放：避开 WebView 黑屏和主线程阻塞 */
@Composable
private fun MediaPreviewView(bytes: ByteArray, kind: PreviewKind) {
    val context = LocalContext.current
    val holder = remember(bytes) {
        val file = File(context.cacheDir, "kh_media_${UUID.randomUUID()}.mp4")
        file.writeBytes(bytes)
        val videoView = VideoView(context).apply {
            layoutParams = FrameLayout.LayoutParams(
                ViewGroup.LayoutParams.MATCH_PARENT,
                ViewGroup.LayoutParams.MATCH_PARENT
            )
            if (kind == PreviewKind.AUDIO) setBackgroundColor(AndroidColor.WHITE)
            val controller = MediaController(context).apply { setAnchorView(this@apply) }
            setMediaController(controller)
            setVideoPath(file.absolutePath)
            setOnPreparedListener { it.start() }
            requestFocus()
        }
        MediaHolder(file, videoView)
    }
    DisposableEffect(holder) {
        onDispose {
            holder.videoView.stopPlayback()
            holder.file.delete()
        }
    }
    AndroidView(factory = { holder.videoView }, modifier = Modifier.fillMaxSize())
}

private class PdfHolder(
    val file: File,
    val pfd: ParcelFileDescriptor,
    val renderer: PdfRenderer
)

/** 原生逐页渲染 PDF：避免 Android WebView 不支持内嵌 PDF 导致空白 */
@Composable
private fun PdfPagePreview(bytes: ByteArray) {
    val context = LocalContext.current
    val holder = remember(bytes) {
        val file = File(context.cacheDir, "kh_preview_${UUID.randomUUID()}.pdf")
        file.writeBytes(bytes)
        val pfd = ParcelFileDescriptor.open(file, ParcelFileDescriptor.MODE_READ_ONLY)
        PdfHolder(file, pfd, PdfRenderer(pfd))
    }
    DisposableEffect(holder) {
        onDispose {
            holder.renderer.close()
            holder.pfd.close()
            holder.file.delete()
        }
    }

    var pageIndex by remember(bytes) { mutableIntStateOf(0) }
    var bitmap by remember(bytes) { mutableStateOf<Bitmap?>(null) }

    LaunchedEffect(holder, pageIndex) {
        val page = holder.renderer.openPage(pageIndex)
        val scale = min(2f, 1600f / page.width.coerceAtLeast(1))
        val width = (page.width * scale).toInt().coerceAtLeast(1)
        val height = (page.height * scale).toInt().coerceAtLeast(1)
        val rendered = Bitmap.createBitmap(width, height, Bitmap.Config.ARGB_8888)
        rendered.eraseColor(AndroidColor.WHITE)
        page.render(rendered, null, null, PdfRenderer.Page.RENDER_MODE_FOR_DISPLAY)
        page.close()
        bitmap = rendered
    }

    Column(Modifier.fillMaxSize().background(Color(0xFFECEEF3))) {
        Box(Modifier.weight(1f).fillMaxWidth(), Alignment.Center) {
            val current = bitmap
            if (current != null) {
                Image(
                    bitmap = current.asImageBitmap(),
                    contentDescription = "PDF 第 ${pageIndex + 1} 页",
                    contentScale = ContentScale.Fit,
                    modifier = Modifier.fillMaxWidth().padding(8.dp)
                )
            } else {
                CircularProgressIndicator()
            }
        }
        Row(
            Modifier.fillMaxWidth().background(Color.White).padding(8.dp),
            verticalAlignment = Alignment.CenterVertically,
            horizontalArrangement = Arrangement.Center
        ) {
            IconButton(
                onClick = { if (pageIndex > 0) pageIndex-- },
                enabled = pageIndex > 0
            ) {
                Icon(Icons.Filled.ChevronLeft, "上一页")
            }
            Text(
                "${pageIndex + 1} / ${holder.renderer.pageCount}",
                fontSize = 13.sp,
                color = TextPrimary,
                modifier = Modifier.padding(horizontal = 16.dp)
            )
            IconButton(
                onClick = { if (pageIndex < holder.renderer.pageCount - 1) pageIndex++ },
                enabled = pageIndex < holder.renderer.pageCount - 1
            ) {
                Icon(Icons.Filled.ChevronRight, "下一页")
            }
        }
    }
}

@Composable
private fun ResourcePreviewDialog(
    preview: ResourcePreviewState,
    onClose: () -> Unit
) {
    val context = LocalContext.current
    Dialog(
        onDismissRequest = onClose,
        properties = DialogProperties(usePlatformDefaultWidth = false)
    ) {
        Surface(Modifier.fillMaxSize(), color = Color.White) {
            Column(Modifier.fillMaxSize()) {
                Row(
                    Modifier.fillMaxWidth().height(56.dp).background(BrandBlue).padding(horizontal = 8.dp),
                    verticalAlignment = Alignment.CenterVertically
                ) {
                    IconButton(onClick = onClose) {
                        Icon(Icons.Filled.Close, "关闭预览", tint = Color.White)
                    }
                    Column(Modifier.weight(1f)) {
                        Text(
                            preview.resource?.originalFileName?.takeIf { it.isNotBlank() }
                                ?: preview.resource?.name ?: "资源预览",
                            color = Color.White,
                            fontSize = 14.sp,
                            fontWeight = FontWeight.Bold,
                            maxLines = 1,
                            overflow = TextOverflow.Ellipsis
                        )
                        Text(
                            previewKindLabel(preview.kind),
                            color = Color.White.copy(alpha = 0.8f),
                            fontSize = 11.sp
                        )
                    }
                    if (preview.url != null) {
                        IconButton(onClick = {
                            runCatching {
                                context.startActivity(Intent(Intent.ACTION_VIEW, Uri.parse(preview.url)))
                            }
                        }) {
                            Icon(Icons.Filled.OpenInNew, "外部打开", tint = Color.White)
                        }
                    }
                }

                when {
                    preview.isLoading -> {
                        Box(Modifier.fillMaxSize(), Alignment.Center) {
                            Column(horizontalAlignment = Alignment.CenterHorizontally) {
                                CircularProgressIndicator()
                                Spacer(Modifier.height(12.dp))
                                Text(preview.notice ?: "正在加载预览…", fontSize = 13.sp, color = TextSecondary)
                            }
                        }
                    }
                    preview.error != null -> {
                        Box(Modifier.fillMaxSize(), Alignment.Center) {
                            Column(horizontalAlignment = Alignment.CenterHorizontally, modifier = Modifier.padding(32.dp)) {
                                Icon(Icons.Filled.ErrorOutline, null, tint = Color(0xFFD93A3A), modifier = Modifier.size(40.dp))
                                Spacer(Modifier.height(10.dp))
                                Text(preview.error!!, fontSize = 14.sp)
                                Spacer(Modifier.height(16.dp))
                                Button(onClick = onClose) { Text("返回") }
                            }
                        }
                    }
                    preview.kind == PreviewKind.TEXT -> {
                        LazyColumn(
                            modifier = Modifier.fillMaxSize().background(Color(0xFFFAFAFC)),
                            contentPadding = PaddingValues(16.dp)
                        ) {
                            item {
                                Text(
                                    preview.text ?: "（空文件）",
                                    fontSize = 13.sp,
                                    lineHeight = 21.sp,
                                    color = TextPrimary
                                )
                            }
                        }
                    }
                    (preview.kind == PreviewKind.VIDEO || preview.kind == PreviewKind.AUDIO) &&
                        preview.videoBytes != null -> {
                        MediaPreviewView(preview.videoBytes!!, preview.kind)
                    }
                    (preview.kind == PreviewKind.PDF || preview.kind == PreviewKind.OFFICE) &&
                        preview.pdfBytes != null -> {
                        PdfPagePreview(preview.pdfBytes!!)
                    }
                    preview.url != null -> {
                        PreviewWebView(url = preview.url!!, kind = preview.kind)
                    }
                    else -> {
                        Box(Modifier.fillMaxSize(), Alignment.Center) {
                            Text("暂无可显示内容", color = TextSecondary)
                        }
                    }
                }
            }
        }
    }
}

@Suppress("SetJavaScriptEnabled")
@Composable
private fun PreviewWebView(url: String, kind: PreviewKind) {
    AndroidView(
        modifier = Modifier.fillMaxSize(),
        factory = { context ->
            WebView(context).apply {
                settings.javaScriptEnabled = true
                settings.domStorageEnabled = true
                settings.useWideViewPort = true
                settings.loadWithOverviewMode = true
                settings.mediaPlaybackRequiresUserGesture = false
                setBackgroundColor(AndroidColor.WHITE)
                webViewClient = object : WebViewClient() {
                    override fun onReceivedSslError(
                        view: WebView?,
                        handler: SslErrorHandler?,
                        error: SslError?
                    ) {
                        // 本地联调后端使用自签证书；生产前需改用正式 CA 并移除此分支。
                        handler?.proceed()
                    }
                }
            }
        },
        update = { webView ->
            // 直接加载资源 URL：loadDataWithBaseURL 是同步 JNI 调用，
            // 在模拟器上加载媒体页会阻塞主线程触发 ANR；loadUrl 为异步导航。
            webView.loadUrl(url)
        },
        onRelease = { it.destroy() }
    )
}
