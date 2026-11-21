package com.knowledgehub.mobile.ui.student

import androidx.compose.foundation.background
import androidx.compose.foundation.clickable
import androidx.compose.foundation.layout.*
import androidx.compose.foundation.lazy.LazyColumn
import androidx.compose.foundation.lazy.items
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.filled.*
import androidx.compose.material3.*
import androidx.compose.runtime.*
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.layout.ContentScale
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.text.style.TextOverflow
import androidx.compose.ui.unit.dp
import androidx.compose.ui.unit.sp
import coil.compose.AsyncImage
import com.knowledgehub.mobile.theme.*
import com.knowledgehub.mobile.ui.common.BrandTopBar

@OptIn(ExperimentalMaterial3Api::class)
@Composable
fun StudentScreen(
    viewModel: StudentViewModel,
    onLogout: () -> Unit
) {
    val state by viewModel.uiState.collectAsState()
    val snackbar = remember { SnackbarHostState() }
    var tab by remember { mutableIntStateOf(0) }
    var detailCourseId by remember { mutableStateOf<String?>(null) }
    var detailReturnTab by remember { mutableIntStateOf(0) }
    var learningCourseId by remember { mutableStateOf<String?>(null) }
    var learningChapterId by remember { mutableStateOf<String?>(null) }
    var learningFromDetail by remember { mutableStateOf(false) }
    var learningReturnTab by remember { mutableIntStateOf(0) }

    LaunchedEffect(Unit) { viewModel.loadHome() }

    state.message?.let { msg ->
        LaunchedEffect(msg) {
            snackbar.showSnackbar(msg)
            viewModel.clearMessage()
        }
    }

    fun reloadCurrentTab() {
        when (tab) {
            0 -> viewModel.loadHome()
            1 -> viewModel.loadExplore()
            2 -> viewModel.loadStats()
            3 -> viewModel.loadProfile()
        }
    }

    fun openDetail(courseId: String) {
        detailReturnTab = tab
        detailCourseId = courseId
    }

    fun openLearnFromDetail(courseId: String, chapterId: String?) {
        learningFromDetail = true
        learningCourseId = courseId
        learningChapterId = chapterId
    }

    fun openLearnDirect(courseId: String) {
        learningFromDetail = false
        learningReturnTab = tab
        learningCourseId = courseId
        learningChapterId = null
    }

    Box(Modifier.fillMaxSize()) {
        when {
            learningCourseId != null -> {
                val learnVm = remember(learningCourseId) { LearnViewModel(viewModel.courseRepo) }
                LearnScreen(
                    courseId = learningCourseId!!,
                    viewModel = learnVm,
                    onBack = {
                        learningCourseId = null
                        learningChapterId = null
                        if (learningFromDetail) {
                            detailCourseId?.let { viewModel.loadDetail(it) }
                        } else {
                            tab = learningReturnTab
                            reloadCurrentTab()
                        }
                    },
                    initialChapterId = learningChapterId
                )
            }

            detailCourseId != null -> {
                CourseDetailView(
                    courseId = detailCourseId!!,
                    state = state,
                    onBack = {
                        detailCourseId = null
                        tab = detailReturnTab
                        reloadCurrentTab()
                    },
                    onLoad = { viewModel.loadDetail(it) },
                    onStartLearn = { course, chapterId -> openLearnFromDetail(course, chapterId) },
                    onEnroll = { viewModel.enrollForDetail(it) }
                )
            }

            else -> {
                Scaffold(
                    topBar = { BrandTopBar(sectionTitle = "学生端", onLogout = onLogout) },
                    bottomBar = {
                        NavigationBar {
                            NavigationBarItem(
                                selected = tab == 0,
                                onClick = { tab = 0; viewModel.loadHome() },
                                icon = { Icon(Icons.Filled.School, null) },
                                label = { Text("我的学习") }
                            )
                            NavigationBarItem(
                                selected = tab == 1,
                                onClick = { tab = 1; viewModel.loadExplore() },
                                icon = { Icon(Icons.Filled.Explore, null) },
                                label = { Text("课程中心") }
                            )
                            NavigationBarItem(
                                selected = tab == 2,
                                onClick = { tab = 2; viewModel.loadStats() },
                                icon = { Icon(Icons.Filled.InsertChart, null) },
                                label = { Text("学习统计") }
                            )
                            NavigationBarItem(
                                selected = tab == 3,
                                onClick = { tab = 3; viewModel.loadProfile() },
                                icon = { Icon(Icons.Filled.Settings, null) },
                                label = { Text("设置") }
                            )
                        }
                    }
                ) { padding ->
                    when (tab) {
                        0 -> MyLearningTab(state, padding, onOpenDetail = { openDetail(it) })
                        1 -> CourseCenterTab(state, padding, viewModel, onOpenDetail = { openDetail(it) })
                        2 -> LearningStatsTab(
                            state = state,
                            padding = padding,
                            viewModel = viewModel,
                            onOpenDetail = { openDetail(it) },
                            onContinueLearn = { openLearnDirect(it) }
                        )
                        3 -> SettingsTab(state, padding, viewModel, onLogout = onLogout)
                    }
                }
            }
        }

        SnackbarHost(snackbar, Modifier.align(Alignment.BottomCenter))
    }
}

@Composable
private fun MyLearningTab(
    state: StudentUiState,
    padding: PaddingValues,
    onOpenDetail: (String) -> Unit
) {
    LazyColumn(
        modifier = Modifier.fillMaxSize().background(SurfaceBg).padding(padding),
        contentPadding = PaddingValues(16.dp),
        verticalArrangement = Arrangement.spacedBy(12.dp)
    ) {
        item {
            state.dashboard?.let { dash ->
                Card(
                    modifier = Modifier.fillMaxWidth(),
                    shape = RoundedCornerShape(12.dp),
                    colors = CardDefaults.cardColors(containerColor = Color.White)
                ) {
                    Column(Modifier.padding(16.dp)) {
                        Text("学习概览", fontWeight = FontWeight.Bold, fontSize = 16.sp)
                        Spacer(Modifier.height(12.dp))
                        Row(Modifier.fillMaxWidth(), horizontalArrangement = Arrangement.SpaceEvenly) {
                            StatItem("${dash.totalCourses}", "选课数")
                            StatItem("${dash.inProgressCourses}", "学习中")
                            StatItem("${dash.completedCourses}", "已完成")
                            StatItem("${dash.averageProgress.toInt()}%", "平均进度")
                        }
                    }
                }
            }
        }

        item {
            Row(
                Modifier.fillMaxWidth().padding(top = 4.dp),
                horizontalArrangement = Arrangement.SpaceBetween,
                verticalAlignment = Alignment.CenterVertically
            ) {
                Text("我的课程", fontWeight = FontWeight.Bold, fontSize = 17.sp)
                Text("${state.myCourses.size} 门", fontSize = 12.sp, color = TextSecondary)
            }
        }

        if (state.isLoading && state.myCourses.isEmpty()) {
            item { Box(Modifier.fillMaxWidth().height(120.dp), Alignment.Center) { CircularProgressIndicator() } }
        }

        items(state.myCourses) { course ->
            CourseCard(
                title = course.courseTitle,
                subtitle = "${course.majorName ?: "公共课"} · ${course.chapterCount} 章",
                progress = course.progress,
                coverUrl = course.courseCoverImageUrl,
                status = course.status,
                onClick = { onOpenDetail(course.courseId) }
            )
        }

        if (!state.isLoading && state.myCourses.isEmpty() && state.error == null) {
            item {
                EmptyHint("还没有选课", "去「课程中心」选一门开始学习吧")
            }
        }

        state.error?.let { item { Text(it, color = MaterialTheme.colorScheme.error, fontSize = 13.sp) } }
    }
}

@Composable
private fun StatItem(value: String, label: String) {
    Column(horizontalAlignment = Alignment.CenterHorizontally) {
        Text(value, fontWeight = FontWeight.Bold, fontSize = 20.sp, color = BrandBlue)
        Text(label, fontSize = 12.sp, color = TextSecondary)
    }
}

@Composable
private fun CourseCard(
    title: String,
    subtitle: String,
    progress: Double,
    coverUrl: String?,
    status: Int,
    onClick: () -> Unit
) {
    Card(
        modifier = Modifier.fillMaxWidth().clickable(onClick = onClick),
        shape = RoundedCornerShape(12.dp),
        colors = CardDefaults.cardColors(containerColor = Color.White),
        elevation = CardDefaults.cardElevation(defaultElevation = 1.dp)
    ) {
        Row(Modifier.padding(14.dp), verticalAlignment = Alignment.CenterVertically) {
            Box(
                Modifier.size(56.dp).background(BrandBlueLight, RoundedCornerShape(8.dp)),
                Alignment.Center
            ) {
                if (!coverUrl.isNullOrBlank()) {
                    AsyncImage(
                        model = coverUrl,
                        contentDescription = null,
                        contentScale = ContentScale.Crop,
                        modifier = Modifier.size(56.dp).background(BrandBlueLight, RoundedCornerShape(8.dp))
                    )
                } else {
                    Text("课", color = BrandBlue, fontWeight = FontWeight.Bold, fontSize = 20.sp)
                }
            }
            Spacer(Modifier.width(14.dp))
            Column(Modifier.weight(1f)) {
                Text(title, fontWeight = FontWeight.Bold, fontSize = 15.sp, maxLines = 1, overflow = TextOverflow.Ellipsis)
                Spacer(Modifier.height(4.dp))
                Text(subtitle, fontSize = 12.sp, color = TextSecondary, maxLines = 1, overflow = TextOverflow.Ellipsis)
                Spacer(Modifier.height(8.dp))
                LinearProgressIndicator(
                    progress = { (progress / 100.0).coerceIn(0.0, 1.0).toFloat() },
                    modifier = Modifier.fillMaxWidth().height(5.dp),
                    color = AccentTeal,
                    trackColor = BrandBlueLight
                )
                Spacer(Modifier.height(3.dp))
                Text("进度 ${progress.toInt()}%", fontSize = 11.sp, color = TextSecondary)
            }
        }
    }
}

@Composable
private fun EmptyHint(title: String, desc: String) {
    Column(
        Modifier.fillMaxWidth().padding(40.dp),
        horizontalAlignment = Alignment.CenterHorizontally
    ) {
        Icon(Icons.Filled.School, null, tint = TextSecondary, modifier = Modifier.size(40.dp))
        Spacer(Modifier.height(12.dp))
        Text(title, fontWeight = FontWeight.Medium, fontSize = 15.sp)
        Spacer(Modifier.height(4.dp))
        Text(desc, fontSize = 12.sp, color = TextSecondary)
    }
}
