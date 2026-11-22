package com.knowledgehub.mobile.ui.teacher

import androidx.compose.foundation.background
import androidx.compose.foundation.clickable
import androidx.compose.foundation.layout.*
import androidx.compose.foundation.text.KeyboardOptions
import androidx.compose.foundation.lazy.LazyColumn
import androidx.compose.foundation.lazy.items
import androidx.compose.foundation.rememberScrollState
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.foundation.verticalScroll
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.filled.Add
import androidx.compose.material.icons.filled.Delete
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
import com.knowledgehub.mobile.data.model.CourseDto
import com.knowledgehub.mobile.theme.*

@Composable
fun TeacherCoursesTab(
    state: TeacherUiState,
    padding: PaddingValues,
    viewModel: TeacherViewModel,
    onOpenContent: (String) -> Unit,
    onOpenInsights: (String, String) -> Unit
) {
    var showCreate by remember { mutableStateOf(false) }
    var confirmDelete by remember { mutableStateOf<CourseDto?>(null) }

    Box(Modifier.fillMaxSize().background(SurfaceBg).padding(padding)) {
        LazyColumn(
            modifier = Modifier.fillMaxSize(),
            contentPadding = PaddingValues(16.dp),
            verticalArrangement = Arrangement.spacedBy(12.dp)
        ) {
            item {
                OutlinedTextField(
                    value = state.courseKeyword,
                    onValueChange = { viewModel.setCourseKeyword(it) },
                    modifier = Modifier.fillMaxWidth(),
                    singleLine = true,
                    label = { Text("搜索课程") },
                    placeholder = { Text("按标题筛选") }
                )
            }

            item {
                Button(
                    onClick = { showCreate = true },
                    modifier = Modifier.fillMaxWidth().height(46.dp),
                    shape = RoundedCornerShape(10.dp),
                    colors = ButtonDefaults.buttonColors(containerColor = BrandBlue)
                ) {
                    Icon(Icons.Filled.Add, null)
                    Spacer(Modifier.width(8.dp))
                    Text("创建课程")
                }
            }

            if (state.isLoading && state.courses.isEmpty()) {
                item { Box(Modifier.fillMaxWidth().height(120.dp), Alignment.Center) { CircularProgressIndicator() } }
            }

            items(state.courses) { course ->
                CourseManageCard(
                    course = course,
                    onContent = { onOpenContent(course.id) },
                    onInsights = { onOpenInsights(course.id, course.title) },
                    onPublish = { viewModel.setCoursePublished(course.id, course.status != 5) },
                    onDelete = { confirmDelete = course }
                )
            }

            if (!state.isLoading && state.courses.isEmpty()) {
                item {
                    Card(
                        modifier = Modifier.fillMaxWidth(),
                        colors = CardDefaults.cardColors(containerColor = Color.White),
                        shape = RoundedCornerShape(12.dp)
                    ) {
                        Column(Modifier.padding(24.dp), horizontalAlignment = Alignment.CenterHorizontally) {
                            Text("暂无课程", fontWeight = FontWeight.Medium, fontSize = 15.sp)
                            Spacer(Modifier.height(4.dp))
                            Text("点击上方“创建课程”开始", fontSize = 12.sp, color = TextSecondary)
                        }
                    }
                }
            }

            state.error?.let { item { Text(it, color = MaterialTheme.colorScheme.error, fontSize = 13.sp) } }
        }
    }

    if (showCreate) {
        CreateCourseDialog(
            majors = state.majors,
            onDismiss = { showCreate = false },
            onCreate = { title, desc, semester, credits, hours, difficulty, majorIds ->
                viewModel.createCourse(title, desc, semester, credits, hours, difficulty, majorIds)
                showCreate = false
            }
        )
    }

    confirmDelete?.let { course ->
        AlertDialog(
            onDismissRequest = { confirmDelete = null },
            title = { Text("删除课程") },
            text = { Text("确认删除「${course.title}」？此操作不可恢复。") },
            confirmButton = {
                TextButton(onClick = {
                    viewModel.deleteCourse(course.id)
                    confirmDelete = null
                }) { Text("删除", color = MaterialTheme.colorScheme.error) }
            },
            dismissButton = { TextButton(onClick = { confirmDelete = null }) { Text("取消") } }
        )
    }
}

@Composable
private fun CourseManageCard(
    course: CourseDto,
    onContent: () -> Unit,
    onInsights: () -> Unit,
    onPublish: () -> Unit,
    onDelete: () -> Unit
) {
    Card(
        modifier = Modifier.fillMaxWidth(),
        shape = RoundedCornerShape(12.dp),
        colors = CardDefaults.cardColors(containerColor = Color.White),
        elevation = CardDefaults.cardElevation(defaultElevation = 1.dp)
    ) {
        Column(Modifier.padding(14.dp)) {
            Row(verticalAlignment = Alignment.CenterVertically) {
                Text(
                    course.title.ifBlank { "未命名课程" },
                    fontWeight = FontWeight.Bold,
                    fontSize = 15.sp,
                    modifier = Modifier.weight(1f),
                    maxLines = 1,
                    overflow = TextOverflow.Ellipsis
                )
                Spacer(Modifier.width(8.dp))
                StatusChip(course.status)
            }
            Spacer(Modifier.height(6.dp))
            Text(
                "${course.majorName ?: "公共课"} · ${course.chapterCount} 章 · ${course.studentCount} 人 · 难度 ${course.difficulty}",
                fontSize = 12.sp,
                color = TextSecondary
            )
            Spacer(Modifier.height(12.dp))
            Row(verticalAlignment = Alignment.CenterVertically) {
                TextButton(onClick = onContent, contentPadding = PaddingValues(horizontal = 8.dp)) {
                    Text("内容", fontSize = 12.sp, color = BrandBlue)
                }
                TextButton(onClick = onInsights, contentPadding = PaddingValues(horizontal = 8.dp)) {
                    Text("学情", fontSize = 12.sp, color = BrandBlue)
                }
                TextButton(onClick = onPublish, contentPadding = PaddingValues(horizontal = 8.dp)) {
                    Text(if (course.status == 5) "撤回" else "发布", fontSize = 12.sp, color = AccentTeal)
                }
                Spacer(Modifier.weight(1f))
                IconButton(onClick = onDelete) {
                    Icon(Icons.Filled.Delete, null, tint = MaterialTheme.colorScheme.error)
                }
            }
        }
    }
}

@Composable
private fun StatusChip(status: Int) {
    val (label, color) = when (status) {
        5 -> "已发布" to AccentTeal
        0 -> "草稿" to TextSecondary
        1 -> "待审核" to WarnOrange
        2, 3 -> "已审核" to BrandBlue
        4 -> "已驳回" to MaterialTheme.colorScheme.error
        else -> "未知" to TextSecondary
    }
    Surface(color = color.copy(alpha = 0.12f), shape = RoundedCornerShape(12.dp)) {
        Text(label, color = color, fontSize = 11.sp, modifier = Modifier.padding(horizontal = 8.dp, vertical = 3.dp))
    }
}

@OptIn(ExperimentalMaterial3Api::class)
@Composable
private fun CreateCourseDialog(
    majors: List<com.knowledgehub.mobile.data.model.MajorLookupDto>,
    onDismiss: () -> Unit,
    onCreate: (String, String?, String?, Int?, Int?, Int, List<String>) -> Unit
) {
    var title by remember { mutableStateOf("") }
    var description by remember { mutableStateOf("") }
    var semester by remember { mutableStateOf("") }
    var credits by remember { mutableStateOf("") }
    var hours by remember { mutableStateOf("") }
    var difficulty by remember { mutableStateOf("1") }
    var selectedMajors by remember { mutableStateOf(setOf<String>()) }

    AlertDialog(
        onDismissRequest = onDismiss,
        title = { Text("创建课程") },
        text = {
            Column(
                Modifier
                    .fillMaxWidth()
                    .heightIn(max = 460.dp)
                    .verticalScroll(rememberScrollState()),
                verticalArrangement = Arrangement.spacedBy(8.dp)
            ) {
                OutlinedTextField(value = title, onValueChange = { title = it }, label = { Text("课程标题 *") }, singleLine = true)
                OutlinedTextField(value = description, onValueChange = { description = it }, label = { Text("课程简介") }, maxLines = 3)
                OutlinedTextField(value = semester, onValueChange = { semester = it }, label = { Text("学期（如 2026-2027-1）") }, singleLine = true)
                Row(horizontalArrangement = Arrangement.spacedBy(8.dp)) {
                    OutlinedTextField(
                        value = credits,
                        onValueChange = { credits = it },
                        label = { Text("学分") },
                        singleLine = true,
                        keyboardOptions = KeyboardOptions(keyboardType = KeyboardType.Number),
                        modifier = Modifier.weight(1f)
                    )
                    OutlinedTextField(
                        value = hours,
                        onValueChange = { hours = it },
                        label = { Text("学时") },
                        singleLine = true,
                        keyboardOptions = KeyboardOptions(keyboardType = KeyboardType.Number),
                        modifier = Modifier.weight(1f)
                    )
                    OutlinedTextField(
                        value = difficulty,
                        onValueChange = { difficulty = it },
                        label = { Text("难度 1-5") },
                        singleLine = true,
                        keyboardOptions = KeyboardOptions(keyboardType = KeyboardType.Number),
                        modifier = Modifier.weight(1f)
                    )
                }
                Text("专业（可多选，不选为公共课）", fontSize = 12.sp, color = TextSecondary)
                Column {
                    majors.forEach { major ->
                        FilterChip(
                            selected = selectedMajors.contains(major.id),
                            onClick = {
                                selectedMajors = if (selectedMajors.contains(major.id)) selectedMajors - major.id
                                else selectedMajors + major.id
                            },
                            label = { Text(major.name, fontSize = 12.sp) },
                            modifier = Modifier.padding(end = 6.dp)
                        )
                    }
                    if (majors.isEmpty()) Text("暂无专业字典，可保存为公共课", fontSize = 11.sp, color = TextSecondary)
                }
            }
        },
        confirmButton = {
            TextButton(onClick = {
                onCreate(
                    title,
                    description.takeIf { it.isNotBlank() },
                    semester.takeIf { it.isNotBlank() },
                    credits.toIntOrNull(),
                    hours.toIntOrNull(),
                    difficulty.toIntOrNull()?.coerceIn(1, 5) ?: 1,
                    selectedMajors.toList()
                )
            }) { Text("创建") }
        },
        dismissButton = { TextButton(onClick = onDismiss) { Text("取消") } }
    )
}
