package com.knowledgehub.mobile.ui.teacher

import androidx.activity.compose.BackHandler
import androidx.compose.foundation.background
import androidx.compose.foundation.clickable
import androidx.compose.foundation.layout.*
import androidx.compose.foundation.lazy.LazyColumn
import androidx.compose.foundation.lazy.items
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.filled.MenuBook
import androidx.compose.material3.*
import androidx.compose.runtime.*
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.text.style.TextOverflow
import androidx.compose.ui.unit.dp
import androidx.compose.ui.unit.sp
import com.knowledgehub.mobile.theme.*
import com.knowledgehub.mobile.ui.common.AppBottomNavigation
import com.knowledgehub.mobile.ui.common.AppModules
import com.knowledgehub.mobile.ui.common.BrandTopBar
import com.knowledgehub.mobile.ui.common.BrandTopBarWithBack
import kotlinx.coroutines.launch

@OptIn(ExperimentalMaterial3Api::class)
@Composable
fun TeacherScreen(
    viewModel: TeacherViewModel,
    onLogout: () -> Unit
) {
    val state by viewModel.uiState.collectAsState()
    val snackbar = remember { SnackbarHostState() }
    val scope = rememberCoroutineScope()
    var selectedModuleId by remember { mutableStateOf(AppModules.WORKBENCH) }

    LaunchedEffect(Unit) { viewModel.loadHome() }

    state.message?.let { msg ->
        LaunchedEffect(msg) {
            snackbar.showSnackbar(msg)
            viewModel.clearMessage()
        }
    }

    fun notify(text: String) {
        scope.launch { snackbar.showSnackbar(text) }
    }

    fun reloadModule(id: String) {
        when (id) {
            AppModules.WORKBENCH -> viewModel.loadHome()
            AppModules.TEACHER_COURSES -> viewModel.loadHome()
            AppModules.TEACHER_CONTENT -> viewModel.loadHome()
            AppModules.TEACHER_AI -> viewModel.loadHome()
            AppModules.TEACHER_INSIGHTS -> viewModel.loadHome()
        }
    }

    fun selectModule(id: String) {
        val module = AppModules.find(id) ?: return
        if (!module.enabled) {
            notify("${module.label}即将开放，敬请期待")
            return
        }
        selectedModuleId = id
        reloadModule(id)
    }

    fun openInsights(courseId: String, title: String) {
        selectedModuleId = AppModules.TEACHER_INSIGHTS
        viewModel.selectCourse(courseId, title)
    }

    fun openContent(courseId: String) {
        selectedModuleId = AppModules.TEACHER_CONTENT
        viewModel.openContent(courseId)
    }

    BackHandler(enabled = state.selectedCourseId != null) {
        viewModel.clearSelection()
    }
    BackHandler(enabled = state.selectedCourseId == null && selectedModuleId != AppModules.WORKBENCH) {
        selectedModuleId = AppModules.WORKBENCH
        viewModel.loadHome()
    }

    val currentTitle = AppModules.find(selectedModuleId)?.label ?: "教师端"
    val displayName = viewModel.session.currentUser?.name?.takeIf { it.isNotBlank() }
        ?: viewModel.session.currentUser?.userName
        ?: "老师"

    Box(Modifier.fillMaxSize()) {
        if (state.selectedCourseId != null) {
            CourseInsightView(state, viewModel)
        } else {
            Scaffold(
                topBar = { BrandTopBar(sectionTitle = currentTitle, onLogout = onLogout) },
                bottomBar = {
                    AppBottomNavigation(
                        modules = AppModules.teacherBottomBar,
                        selectedId = selectedModuleId,
                        onSelect = { selectModule(it) }
                    )
                }
            ) { padding ->
                when (selectedModuleId) {
                    AppModules.WORKBENCH -> TeacherWorkbenchTab(
                        state = state,
                        padding = padding,
                        displayName = displayName,
                        tenantName = viewModel.session.tenant,
                        onOpenModule = { selectModule(it) },
                        onOpenCourse = { id, title -> openInsights(id, title) }
                    )

                    AppModules.TEACHER_COURSES -> TeacherCoursesTab(
                        state = state,
                        padding = padding,
                        viewModel = viewModel,
                        onOpenContent = { openContent(it) },
                        onOpenInsights = { id, title -> openInsights(id, title) }
                    )

                    AppModules.TEACHER_CONTENT -> TeacherContentTab(
                        state = state,
                        padding = padding,
                        viewModel = viewModel
                    )

                    AppModules.TEACHER_AI -> TeacherAiTab(
                        state = state,
                        padding = padding,
                        viewModel = viewModel
                    )

                    AppModules.TEACHER_INSIGHTS -> TeacherInsightsTab(
                        state = state,
                        padding = padding,
                        onOpenInsights = { id, title -> openInsights(id, title) }
                    )

                    else -> TeacherWorkbenchTab(
                        state = state,
                        padding = padding,
                        displayName = displayName,
                        tenantName = viewModel.session.tenant,
                        onOpenModule = { selectModule(it) },
                        onOpenCourse = { id, title -> openInsights(id, title) }
                    )
                }
            }
        }

        SnackbarHost(snackbar, Modifier.align(Alignment.BottomCenter))
    }
}

@Composable
private fun TeacherInsightsTab(
    state: TeacherUiState,
    padding: PaddingValues,
    onOpenInsights: (String, String) -> Unit
) {
    LazyColumn(
        modifier = Modifier.fillMaxSize().background(SurfaceBg).padding(padding),
        contentPadding = PaddingValues(16.dp),
        verticalArrangement = Arrangement.spacedBy(12.dp)
    ) {
        item {
            Text("选择课程查看学情", fontWeight = FontWeight.Bold, fontSize = 16.sp)
        }
        if (state.isLoading && state.courses.isEmpty()) {
            item { Box(Modifier.fillMaxWidth().height(120.dp), Alignment.Center) { CircularProgressIndicator() } }
        }
        items(state.courses) { course ->
            Card(
                modifier = Modifier.fillMaxWidth().clickable { onOpenInsights(course.id, course.title) },
                shape = RoundedCornerShape(12.dp),
                colors = CardDefaults.cardColors(containerColor = Color.White)
            ) {
                Row(Modifier.padding(14.dp), verticalAlignment = Alignment.CenterVertically) {
                    Box(
                        Modifier.size(44.dp).background(BrandBlueLight, RoundedCornerShape(10.dp)),
                        Alignment.Center
                    ) {
                        Icon(Icons.Filled.MenuBook, null, tint = BrandBlue)
                    }
                    Spacer(Modifier.width(12.dp))
                    Column(Modifier.weight(1f)) {
                        Text(course.title, fontWeight = FontWeight.Bold, fontSize = 14.sp, maxLines = 1, overflow = TextOverflow.Ellipsis)
                        Spacer(Modifier.height(3.dp))
                        Text("${course.studentCount} 名学生 · ${course.chapterCount} 章", fontSize = 11.sp, color = TextSecondary)
                    }
                    Text("查看 ›", color = BrandBlue, fontSize = 12.sp)
                }
            }
        }
        state.error?.let { item { Text(it, color = MaterialTheme.colorScheme.error, fontSize = 13.sp) } }
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
                            Text(
                                "完成 ${cp.completedCount}/${cp.totalExercises} · ${cp.participantCount} 人参与 · 正确率 ${cp.correctRate.toInt()}%",
                                fontSize = 11.sp,
                                color = TextSecondary
                            )
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
