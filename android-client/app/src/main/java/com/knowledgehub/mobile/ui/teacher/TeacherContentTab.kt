package com.knowledgehub.mobile.ui.teacher

import androidx.compose.foundation.background
import androidx.compose.foundation.horizontalScroll
import androidx.compose.foundation.layout.*
import androidx.compose.foundation.text.KeyboardOptions
import androidx.compose.foundation.lazy.LazyColumn
import androidx.compose.foundation.lazy.items
import androidx.compose.foundation.rememberScrollState
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.foundation.verticalScroll
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.filled.Add
import androidx.compose.material3.*
import androidx.compose.runtime.*
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.text.input.KeyboardType
import androidx.compose.ui.text.style.TextOverflow
import androidx.compose.ui.unit.dp
import androidx.compose.ui.unit.sp
import com.knowledgehub.mobile.data.model.ChapterDto
import com.knowledgehub.mobile.data.model.ExerciseDto
import com.knowledgehub.mobile.data.model.KnowledgeResourceDto
import com.knowledgehub.mobile.theme.*

private data class FlatTeacherChapter(val chapter: ChapterDto, val depth: Int)

@Composable
fun TeacherContentTab(
    state: TeacherUiState,
    padding: PaddingValues,
    viewModel: TeacherViewModel
) {
    var showChapter by remember { mutableStateOf(false) }
    var showExercise by remember { mutableStateOf(false) }
    var showResource by remember { mutableStateOf(false) }
    var parentChapterId by remember { mutableStateOf<String?>(null) }
    var exerciseChapterId by remember { mutableStateOf<String?>(null) }

    val flatChapters = remember(state.chapters) { flatten(state.chapters, 0) }
    val selectedCourseId = state.contentCourseId

    Column(Modifier.fillMaxSize().background(SurfaceBg).padding(padding)) {
        Text("选择课程", fontWeight = FontWeight.Bold, fontSize = 16.sp, modifier = Modifier.padding(horizontal = 16.dp, vertical = 10.dp))
        Row(
            Modifier.fillMaxWidth().horizontalScroll(rememberScrollState()).padding(horizontal = 16.dp),
            horizontalArrangement = Arrangement.spacedBy(8.dp)
        ) {
            state.courses.forEach { course ->
                FilterChip(
                    selected = course.id == selectedCourseId,
                    onClick = {
                        exerciseChapterId = null
                        parentChapterId = null
                        viewModel.openContent(course.id)
                    },
                    label = { Text(course.title, maxLines = 1, overflow = TextOverflow.Ellipsis) }
                )
            }
            if (state.courses.isEmpty()) Text("暂无课程，请先到「课程」创建", fontSize = 12.sp, color = TextSecondary)
        }

        if (selectedCourseId == null) {
            Box(Modifier.fillMaxSize(), Alignment.Center) {
                Text("请选择一门课程", color = TextSecondary, fontSize = 14.sp)
            }
            return
        }

        Row(
            Modifier.fillMaxWidth().padding(horizontal = 16.dp, vertical = 10.dp),
            horizontalArrangement = Arrangement.spacedBy(8.dp)
        ) {
            Button(onClick = { showChapter = true }, modifier = Modifier.weight(1f), contentPadding = PaddingValues(4.dp)) {
                Icon(Icons.Filled.Add, null, modifier = Modifier.size(16.dp))
                Text("章节", fontSize = 12.sp)
            }
            Button(onClick = { showExercise = true }, modifier = Modifier.weight(1f), contentPadding = PaddingValues(4.dp)) {
                Icon(Icons.Filled.Add, null, modifier = Modifier.size(16.dp))
                Text("习题", fontSize = 12.sp)
            }
            Button(onClick = { showResource = true }, modifier = Modifier.weight(1f), contentPadding = PaddingValues(4.dp)) {
                Icon(Icons.Filled.Add, null, modifier = Modifier.size(16.dp))
                Text("资源", fontSize = 12.sp)
            }
        }

        LazyColumn(
            modifier = Modifier.fillMaxSize(),
            contentPadding = PaddingValues(horizontal = 16.dp, vertical = 8.dp),
            verticalArrangement = Arrangement.spacedBy(14.dp)
        ) {
            item { ContentHeader("章节目录", "${flatChapters.size} 个") }
            items(flatChapters) { entry ->
                ChapterRow(entry.chapter.title, entry.depth, entry.chapter.knowledgeResources.size)
            }

            item { ContentHeader("课程习题", "${state.exercises.size} 道") }
            items(state.exercises) { exercise -> ExerciseRow(exercise) }

            item { ContentHeader("知识资源", "${state.resources.size} 条") }
            items(state.resources) { resource -> ResourceRow(resource) }
        }
    }

    if (showChapter) {
        CreateChapterDialog(
            chapters = flatChapters,
            parentId = parentChapterId,
            onParentChange = { parentChapterId = it },
            onDismiss = { showChapter = false },
            onCreate = { title, desc, parent, order ->
                viewModel.createChapter(title, desc, parent, order)
                showChapter = false
            }
        )
    }

    if (showExercise) {
        CreateExerciseDialog(
            chapters = flatChapters,
            chapterId = exerciseChapterId,
            onChapterChange = { exerciseChapterId = it },
            onDismiss = { showExercise = false },
            onCreate = { chapterId, title, content, type, options, answer, analysis, difficulty, score ->
                viewModel.createExercise(chapterId, title, content, type, options, answer, analysis, difficulty, score)
                showExercise = false
            }
        )
    }

    if (showResource) {
        CreateResourceDialog(
            onDismiss = { showResource = false },
            onCreate = { name, desc, content ->
                viewModel.createResource(name, desc, content)
                showResource = false
            }
        )
    }
}

private fun flatten(chapters: List<ChapterDto>, depth: Int): List<FlatTeacherChapter> =
    chapters.flatMap { listOf(FlatTeacherChapter(it, depth)) + flatten(it.children, depth + 1) }

@Composable
private fun ContentHeader(title: String, count: String) {
    Row(Modifier.fillMaxWidth(), horizontalArrangement = Arrangement.SpaceBetween, verticalAlignment = Alignment.CenterVertically) {
        Text(title, fontWeight = FontWeight.Bold, fontSize = 15.sp)
        Text(count, fontSize = 12.sp, color = TextSecondary)
    }
}

@Composable
private fun ChapterRow(title: String, depth: Int, resourceCount: Int) {
    Card(
        modifier = Modifier.fillMaxWidth().padding(start = (depth * 16).dp),
        colors = CardDefaults.cardColors(containerColor = Color.White),
        shape = RoundedCornerShape(10.dp)
    ) {
        Row(Modifier.padding(12.dp), verticalAlignment = Alignment.CenterVertically) {
            Text(if (depth > 0) "└ " else "●", color = BrandBlue, fontSize = 12.sp)
            Spacer(Modifier.width(6.dp))
            Text(title, fontSize = 14.sp, modifier = Modifier.weight(1f), maxLines = 1, overflow = TextOverflow.Ellipsis)
            Text("$resourceCount 资源", fontSize = 11.sp, color = TextSecondary)
        }
    }
}

@Composable
private fun ExerciseRow(exercise: ExerciseDto) {
    Card(
        modifier = Modifier.fillMaxWidth(),
        colors = CardDefaults.cardColors(containerColor = Color.White),
        shape = RoundedCornerShape(10.dp)
    ) {
        Column(Modifier.padding(12.dp)) {
            Text(exercise.title.ifBlank { "未命名习题" }, fontWeight = FontWeight.Medium, fontSize = 14.sp)
            Spacer(Modifier.height(3.dp))
            Text(exercise.questionContent, fontSize = 12.sp, color = TextSecondary, maxLines = 2, overflow = TextOverflow.Ellipsis)
            Spacer(Modifier.height(4.dp))
            Text(
                "${typeLabel(exercise.type)} · 难度 ${exercise.difficulty} · 答案 ${exercise.answer ?: "-"}${if (exercise.isAiGenerated) " · AI" else ""}",
                fontSize = 11.sp,
                color = BrandBlue
            )
        }
    }
}

@Composable
private fun ResourceRow(resource: KnowledgeResourceDto) {
    Card(
        modifier = Modifier.fillMaxWidth(),
        colors = CardDefaults.cardColors(containerColor = Color.White),
        shape = RoundedCornerShape(10.dp)
    ) {
        Column(Modifier.padding(12.dp)) {
            Text(resource.name.ifBlank { "未命名资源" }, fontWeight = FontWeight.Medium, fontSize = 14.sp)
            resource.description?.takeIf { it.isNotBlank() }?.let {
                Spacer(Modifier.height(3.dp))
                Text(it, fontSize = 12.sp, color = TextSecondary, maxLines = 2, overflow = TextOverflow.Ellipsis)
            }
        }
    }
}

private fun typeLabel(type: Int) = when (type) {
    0 -> "单选"
    1 -> "多选"
    2 -> "判断"
    3 -> "填空"
    4 -> "简答"
    else -> "习题"
}

@Composable
private fun ChapterPicker(
    chapters: List<FlatTeacherChapter>,
    chapterId: String?,
    onChapterChange: (String?) -> Unit
) {
    Column {
        FilterChip(
            selected = chapterId == null,
            onClick = { onChapterChange(null) },
            label = { Text("无 / 课程级", fontSize = 12.sp) }
        )
        chapters.forEach { entry ->
            FilterChip(
                selected = chapterId == entry.chapter.id,
                onClick = { onChapterChange(entry.chapter.id) },
                label = { Text("${"· ".repeat(entry.depth)}${entry.chapter.title}", fontSize = 12.sp) }
            )
        }
    }
}

@OptIn(ExperimentalMaterial3Api::class)
@Composable
private fun CreateChapterDialog(
    chapters: List<FlatTeacherChapter>,
    parentId: String?,
    onParentChange: (String?) -> Unit,
    onDismiss: () -> Unit,
    onCreate: (String, String?, String?, Int) -> Unit
) {
    var title by remember { mutableStateOf("") }
    var description by remember { mutableStateOf("") }
    var order by remember { mutableStateOf("0") }
    AlertDialog(
        onDismissRequest = onDismiss,
        title = { Text("新建章节") },
        text = {
            Column(Modifier.fillMaxWidth().heightIn(max = 420.dp).verticalScroll(rememberScrollState()), verticalArrangement = Arrangement.spacedBy(8.dp)) {
                OutlinedTextField(value = title, onValueChange = { title = it }, label = { Text("章节标题 *") }, singleLine = true)
                OutlinedTextField(value = description, onValueChange = { description = it }, label = { Text("描述") }, maxLines = 3)
                OutlinedTextField(
                    value = order,
                    onValueChange = { order = it },
                    label = { Text("排序号") },
                    singleLine = true,
                    keyboardOptions = KeyboardOptions(keyboardType = KeyboardType.Number)
                )
                Text("父章节", fontSize = 12.sp, color = TextSecondary)
                ChapterPicker(chapters, parentId, onParentChange)
            }
        },
        confirmButton = {
            TextButton(onClick = {
                onCreate(title, description.takeIf { it.isNotBlank() }, parentId, order.toIntOrNull() ?: 0)
            }) { Text("创建") }
        },
        dismissButton = { TextButton(onClick = onDismiss) { Text("取消") } }
    )
}

@OptIn(ExperimentalMaterial3Api::class)
@Composable
private fun CreateExerciseDialog(
    chapters: List<FlatTeacherChapter>,
    chapterId: String?,
    onChapterChange: (String?) -> Unit,
    onDismiss: () -> Unit,
    onCreate: (String?, String, String, Int, String?, String, String?, Int, Int) -> Unit
) {
    var title by remember { mutableStateOf("") }
    var content by remember { mutableStateOf("") }
    var type by remember { mutableIntStateOf(0) }
    var options by remember { mutableStateOf("") }
    var answer by remember { mutableStateOf("") }
    var analysis by remember { mutableStateOf("") }
    var difficulty by remember { mutableStateOf("1") }
    var score by remember { mutableStateOf("1") }
    AlertDialog(
        onDismissRequest = onDismiss,
        title = { Text("新建习题") },
        text = {
            Column(Modifier.fillMaxWidth().heightIn(max = 460.dp).verticalScroll(rememberScrollState()), verticalArrangement = Arrangement.spacedBy(8.dp)) {
                OutlinedTextField(value = title, onValueChange = { title = it }, label = { Text("题目标题 *") }, singleLine = true)
                OutlinedTextField(value = content, onValueChange = { content = it }, label = { Text("题干 *") }, maxLines = 3)
                Text("题型", fontSize = 12.sp, color = TextSecondary)
                Row {
                    (0..4).forEach { value ->
                        FilterChip(
                            selected = type == value,
                            onClick = { type = value },
                            label = { Text(typeLabel(value), fontSize = 11.sp) },
                            modifier = Modifier.padding(end = 4.dp)
                        )
                    }
                }
                OutlinedTextField(value = options, onValueChange = { options = it }, label = { Text("选项（多个用换行分隔，可空）") }, maxLines = 3)
                OutlinedTextField(value = answer, onValueChange = { answer = it }, label = { Text("标准答案") }, singleLine = true)
                OutlinedTextField(value = analysis, onValueChange = { analysis = it }, label = { Text("解析（可空）") }, maxLines = 2)
                Row(horizontalArrangement = Arrangement.spacedBy(8.dp)) {
                    OutlinedTextField(
                        value = difficulty, onValueChange = { difficulty = it }, label = { Text("难度") }, singleLine = true,
                        keyboardOptions = KeyboardOptions(keyboardType = KeyboardType.Number),
                        modifier = Modifier.weight(1f)
                    )
                    OutlinedTextField(
                        value = score, onValueChange = { score = it }, label = { Text("分值") }, singleLine = true,
                        keyboardOptions = KeyboardOptions(keyboardType = KeyboardType.Number),
                        modifier = Modifier.weight(1f)
                    )
                }
                Text("所属章节", fontSize = 12.sp, color = TextSecondary)
                ChapterPicker(chapters, chapterId, onChapterChange)
            }
        },
        confirmButton = {
            TextButton(onClick = {
                onCreate(
                    chapterId, title, content, type,
                    options.takeIf { it.isNotBlank() }, answer,
                    analysis.takeIf { it.isNotBlank() },
                    difficulty.toIntOrNull()?.coerceIn(1, 5) ?: 1,
                    score.toIntOrNull()?.coerceIn(1, 100) ?: 1
                )
            }) { Text("创建") }
        },
        dismissButton = { TextButton(onClick = onDismiss) { Text("取消") } }
    )
}

@OptIn(ExperimentalMaterial3Api::class)
@Composable
private fun CreateResourceDialog(
    onDismiss: () -> Unit,
    onCreate: (String, String?, String?) -> Unit
) {
    var name by remember { mutableStateOf("") }
    var description by remember { mutableStateOf("") }
    var content by remember { mutableStateOf("") }
    AlertDialog(
        onDismissRequest = onDismiss,
        title = { Text("新建知识资源") },
        text = {
            Column(verticalArrangement = Arrangement.spacedBy(8.dp)) {
                OutlinedTextField(value = name, onValueChange = { name = it }, label = { Text("资源名称 *") }, singleLine = true)
                OutlinedTextField(value = description, onValueChange = { description = it }, label = { Text("描述") }, maxLines = 2)
                OutlinedTextField(value = content, onValueChange = { content = it }, label = { Text("文本内容") }, maxLines = 4)
            }
        },
        confirmButton = { TextButton(onClick = { onCreate(name, description.takeIf { it.isNotBlank() }, content.takeIf { it.isNotBlank() }) }) { Text("创建") } },
        dismissButton = { TextButton(onClick = onDismiss) { Text("取消") } }
    )
}
