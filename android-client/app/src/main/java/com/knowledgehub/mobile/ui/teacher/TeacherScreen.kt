package com.knowledgehub.mobile.ui.teacher

import androidx.compose.foundation.background
import androidx.compose.foundation.clickable
import androidx.compose.foundation.layout.*
import androidx.compose.foundation.lazy.LazyColumn
import androidx.compose.foundation.lazy.items
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.filled.ArrowBack
import androidx.compose.material.icons.filled.Logout
import androidx.compose.material3.*
import androidx.compose.runtime.*
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.text.style.TextOverflow
import androidx.compose.ui.unit.dp
import androidx.compose.ui.unit.sp
import com.knowledgehub.mobile.data.model.CourseDto
import com.knowledgehub.mobile.theme.*
import com.knowledgehub.mobile.ui.common.BrandTopBar
import com.knowledgehub.mobile.ui.common.BrandTopBarWithBack

@OptIn(ExperimentalMaterial3Api::class)
@Composable
fun TeacherScreen(
    viewModel: TeacherViewModel,
    onLogout: () -> Unit
) {
    val state by viewModel.uiState.collectAsState()
    LaunchedEffect(Unit) { viewModel.loadHome() }

    if (state.selectedCourseId != null) {
        CourseInsightView(state, viewModel)
        return
    }

    Scaffold(
        topBar = { BrandTopBar(sectionTitle = "教师端", onLogout = onLogout) }
    ) { padding ->
        LazyColumn(
            modifier = Modifier.fillMaxSize().background(SurfaceBg).padding(padding),
            contentPadding = PaddingValues(16.dp),
            verticalArrangement = Arrangement.spacedBy(12.dp)
        ) {
            item {
                Row(
                    Modifier.fillMaxWidth(),
                    horizontalArrangement = Arrangement.SpaceBetween,
                    verticalAlignment = Alignment.CenterVertically
                ) {
                    Text("我教的课程", fontWeight = FontWeight.Bold, fontSize = 17.sp)
                    Text("${state.courses.size} 门", fontSize = 12.sp, color = TextSecondary)
                }
            }
            if (state.isLoading) {
                item { Box(Modifier.fillMaxWidth().height(120.dp), Alignment.Center) { CircularProgressIndicator() } }
            }
            items(state.courses) { course ->
                TeacherCourseCard(course) { viewModel.selectCourse(course.id, course.title) }
            }
            if (!state.isLoading && state.courses.isEmpty()) {
                item {
                    Card(
                        shape = RoundedCornerShape(12.dp),
                        colors = CardDefaults.cardColors(containerColor = Color.White)
                    ) {
                        Column(Modifier.padding(24.dp), horizontalAlignment = Alignment.CenterHorizontally) {
                            Text("暂无授课课程", fontWeight = FontWeight.Medium, fontSize = 15.sp)
                            Spacer(Modifier.height(4.dp))
                            Text("在 Web 端创建并发布课程后会显示在这里", fontSize = 12.sp, color = TextSecondary)
                        }
                    }
                }
            }
            state.error?.let { item { Text(it, color = MaterialTheme.colorScheme.error, fontSize = 13.sp) } }
        }
    }
}

@Composable
private fun TeacherCourseCard(course: CourseDto, onClick: () -> Unit) {
    Card(
        modifier = Modifier.fillMaxWidth().clickable(onClick = onClick),
        shape = RoundedCornerShape(12.dp),
        colors = CardDefaults.cardColors(containerColor = Color.White),
        elevation = CardDefaults.cardElevation(defaultElevation = 1.dp)
    ) {
        Column(Modifier.padding(16.dp)) {
            Text(course.title, fontWeight = FontWeight.Bold, fontSize = 15.sp, maxLines = 1, overflow = TextOverflow.Ellipsis)
            Spacer(Modifier.height(6.dp))
            Text("${course.majorName ?: "公共课"} · ${course.chapterCount} 章 · ${course.studentCount} 名学生", fontSize = 12.sp, color = TextSecondary)
            Spacer(Modifier.height(10.dp))
            Row(verticalAlignment = Alignment.CenterVertically) {
                LinearProgressIndicator(
                    progress = { (course.progress / 100.0).coerceIn(0.0, 1.0).toFloat() },
                    modifier = Modifier.weight(1f).height(6.dp),
                    color = AccentTeal,
                    trackColor = BrandBlueLight
                )
                Spacer(Modifier.width(10.dp))
                Text("查看学情 ›", color = BrandBlue, fontSize = 12.sp, fontWeight = FontWeight.Medium)
            }
        }
    }
}

@OptIn(ExperimentalMaterial3Api::class)
@Composable
private fun CourseInsightView(state: TeacherUiState, viewModel: TeacherViewModel) {
    Scaffold(
        topBar = {
            BrandTopBarWithBack(
                title = state.selectedCourseTitle,
                onBack = { viewModel.clearSelection() }
            )
        }
    ) { padding ->
        LazyColumn(
            modifier = Modifier.fillMaxSize().background(SurfaceBg).padding(padding),
            contentPadding = PaddingValues(16.dp),
            verticalArrangement = Arrangement.spacedBy(12.dp)
        ) {
            state.overview?.let { ov ->
                item {
                    Card(shape = RoundedCornerShape(12.dp), colors = CardDefaults.cardColors(containerColor = Color.White)) {
                        Column(Modifier.padding(16.dp)) {
                            Text("学习概览", fontWeight = FontWeight.Bold, fontSize = 16.sp)
                            Spacer(Modifier.height(12.dp))
                            Row(Modifier.fillMaxWidth(), horizontalArrangement = Arrangement.SpaceEvenly) {
                                Stat("${ov.totalStudents}", "学生数")
                                Stat("${ov.activeStudents}", "活跃")
                                Stat("${ov.totalExercises}", "习题")
                                Stat("${ov.averageCompletionRate.toInt()}%", "完成率")
                            }
                            Spacer(Modifier.height(8.dp))
                            Row(Modifier.fillMaxWidth(), horizontalArrangement = Arrangement.SpaceEvenly) {
                                Stat("${ov.averageCorrectRate.toInt()}%", "正确率")
                                Stat("${ov.totalLearningMinutes.toInt()}分", "总学时")
                            }
                        }
                    }
                }
                items(ov.chapterProgress) { cp ->
                    Card(shape = RoundedCornerShape(10.dp), colors = CardDefaults.cardColors(containerColor = Color.White)) {
                        Column(Modifier.padding(14.dp)) {
                            Text(cp.chapterName, fontWeight = FontWeight.Medium, fontSize = 14.sp)
                            Spacer(Modifier.height(8.dp))
                            LinearProgressIndicator(
                                progress = { (cp.completionRate / 100.0).coerceIn(0.0, 1.0).toFloat() },
                                modifier = Modifier.fillMaxWidth().height(6.dp),
                                color = AccentTeal,
                                trackColor = BrandBlueLight
                            )
                            Spacer(Modifier.height(4.dp))
                            Text("完成 ${cp.completedCount}/${cp.totalExercises} · ${cp.participantCount} 人参与 · 正确率 ${cp.correctRate.toInt()}%", fontSize = 11.sp, color = TextSecondary)
                        }
                    }
                }
            }

            item {
                Text("学生学习情况", fontWeight = FontWeight.Bold, fontSize = 16.sp, modifier = Modifier.padding(top = 4.dp))
            }
            items(state.students) { s ->
                Card(shape = RoundedCornerShape(10.dp), colors = CardDefaults.cardColors(containerColor = Color.White)) {
                    Row(Modifier.fillMaxWidth().padding(14.dp), verticalAlignment = Alignment.CenterVertically) {
                        Box(Modifier.size(36.dp).background(BrandBlueLight, RoundedCornerShape(18.dp)), Alignment.Center) {
                            Text(s.studentName.take(1), color = BrandBlue, fontWeight = FontWeight.Bold)
                        }
                        Spacer(Modifier.width(12.dp))
                        Column(Modifier.weight(1f)) {
                            Text(s.studentName, fontWeight = FontWeight.Medium, fontSize = 14.sp)
                            Text("完成 ${s.completedCount}/${s.totalCount} · 正确率 ${s.correctRate.toInt()}%", fontSize = 11.sp, color = TextSecondary)
                        }
                        Text("${s.completionRate.toInt()}%", color = BrandBlue, fontWeight = FontWeight.Bold, fontSize = 14.sp)
                    }
                }
            }
            if (state.students.isEmpty() && state.overview != null) {
                item { Text("暂无学生学习记录", fontSize = 13.sp, color = TextSecondary, modifier = Modifier.padding(16.dp)) }
            }
        }
    }
}

@Composable
private fun Stat(value: String, label: String) {
    Column(horizontalAlignment = Alignment.CenterHorizontally) {
        Text(value, fontWeight = FontWeight.Bold, fontSize = 18.sp, color = BrandBlue)
        Text(label, fontSize = 11.sp, color = TextSecondary)
    }
}
