package com.knowledgehub.mobile.ui.student

import androidx.activity.compose.BackHandler
import androidx.compose.foundation.background
import androidx.compose.foundation.clickable
import androidx.compose.foundation.layout.*
import androidx.compose.foundation.lazy.LazyColumn
import androidx.compose.foundation.lazy.items
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.filled.School
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
import com.knowledgehub.mobile.ui.common.AppBottomNavigation
import com.knowledgehub.mobile.ui.common.AppModules
import com.knowledgehub.mobile.ui.common.BrandTopBar
import kotlinx.coroutines.launch

@OptIn(ExperimentalMaterial3Api::class)
@Composable
fun StudentScreen(
    viewModel: StudentViewModel,
    onLogout: () -> Unit
) {
    val state by viewModel.uiState.collectAsState()
    val snackbar = remember { SnackbarHostState() }
    val scope = rememberCoroutineScope()

    // 一级页面使用模块 ID，而不是数组下标：新增模块只需在 AppModules 注册。
    var selectedModuleId by remember { mutableStateOf(AppModules.WORKBENCH) }
    var detailCourseId by remember { mutableStateOf<String?>(null) }
    var detailReturnModuleId by remember { mutableStateOf(AppModules.WORKBENCH) }
    var learningCourseId by remember { mutableStateOf<String?>(null) }
    var learningChapterId by remember { mutableStateOf<String?>(null) }
    var learningFromDetail by remember { mutableStateOf(false) }
    var learningReturnModuleId by remember { mutableStateOf(AppModules.WORKBENCH) }

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
            AppModules.WORKBENCH, AppModules.MY_LEARNING -> viewModel.loadHome()
            AppModules.COURSE_CENTER -> viewModel.loadExplore()
            AppModules.LEARNING_STATS -> viewModel.loadStats()
            AppModules.SETTINGS -> viewModel.loadProfile()
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

    fun openDetail(courseId: String) {
        detailReturnModuleId = selectedModuleId
        detailCourseId = courseId
    }

    fun openLearnFromDetail(courseId: String, chapterId: String?) {
        learningFromDetail = true
        learningCourseId = courseId
        learningChapterId = chapterId
    }

    fun openLearnDirect(courseId: String) {
        learningFromDetail = false
        learningReturnModuleId = selectedModuleId
        learningCourseId = courseId
        learningChapterId = null
    }

    fun handleLearnBack() {
        learningCourseId = null
        learningChapterId = null
        if (learningFromDetail) {
            detailCourseId?.let { viewModel.loadDetail(it) }
        } else {
            selectedModuleId = learningReturnModuleId
            reloadModule(learningReturnModuleId)
        }
    }

    fun handleDetailBack() {
        detailCourseId = null
        selectedModuleId = detailReturnModuleId
        reloadModule(detailReturnModuleId)
    }

    // 与常见安卓 App 一致：逐级返回，非工作台 Tab 先回到工作台。
    BackHandler(enabled = learningCourseId != null) { handleLearnBack() }
    BackHandler(enabled = learningCourseId == null && detailCourseId != null) { handleDetailBack() }
    BackHandler(
        enabled = learningCourseId == null &&
            detailCourseId == null &&
            selectedModuleId != AppModules.WORKBENCH
    ) {
        selectedModuleId = AppModules.WORKBENCH
        reloadModule(AppModules.WORKBENCH)
    }

    val currentTitle = AppModules.find(selectedModuleId)?.label ?: "学生端"
    val displayName = viewModel.session.currentUser?.name?.takeIf { it.isNotBlank() }
        ?: viewModel.session.currentUser?.userName
        ?: "同学"

    Box(Modifier.fillMaxSize()) {
        when {
            learningCourseId != null -> {
                val learnVm = remember(learningCourseId) { LearnViewModel(viewModel.courseRepo) }
                LearnScreen(
                    courseId = learningCourseId!!,
                    viewModel = learnVm,
                    onBack = { handleLearnBack() },
                    initialChapterId = learningChapterId
                )
            }

            detailCourseId != null -> {
                CourseDetailView(
                    courseId = detailCourseId!!,
                    state = state,
                    onBack = { handleDetailBack() },
                    onLoad = { viewModel.loadDetail(it) },
                    onStartLearn = { course, chapterId -> openLearnFromDetail(course, chapterId) },
                    onEnroll = { viewModel.enrollForDetail(it) }
                )
            }

            else -> {
                Scaffold(
                    topBar = { BrandTopBar(sectionTitle = currentTitle, onLogout = onLogout) },
                    bottomBar = {
                        AppBottomNavigation(
                            modules = AppModules.studentBottomBar,
                            selectedId = selectedModuleId,
                            onSelect = { selectModule(it) }
                        )
                    }
                ) { padding ->
                    when (selectedModuleId) {
                        AppModules.WORKBENCH -> WorkbenchTab(
                            state = state,
                            padding = padding,
                            displayName = displayName,
                            tenantName = viewModel.session.tenant,
                            onOpenModule = { selectModule(it) },
                            onOpenDetail = { openDetail(it) },
                            onContinueLearn = { openLearnDirect(it) }
                        )

                        AppModules.MY_LEARNING -> MyLearningTab(
                            state = state,
                            padding = padding,
                            onOpenDetail = { openDetail(it) }
                        )

                        AppModules.COURSE_CENTER -> CourseCenterTab(
                            state = state,
                            padding = padding,
                            viewModel = viewModel,
                            onOpenDetail = { openDetail(it) }
                        )

                        AppModules.LEARNING_STATS -> LearningStatsTab(
                            state = state,
                            padding = padding,
                            viewModel = viewModel,
                            onOpenDetail = { openDetail(it) },
                            onContinueLearn = { openLearnDirect(it) }
                        )

                        AppModules.SETTINGS -> SettingsTab(
                            state = state,
                            padding = padding,
                            viewModel = viewModel,
                            onLogout = onLogout
                        )

                        else -> WorkbenchTab(
                            state = state,
                            padding = padding,
                            displayName = displayName,
                            tenantName = viewModel.session.tenant,
                            onOpenModule = { selectModule(it) },
                            onOpenDetail = { openDetail(it) },
                            onContinueLearn = { openLearnDirect(it) }
                        )
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
