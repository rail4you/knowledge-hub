package com.knowledgehub.mobile.ui.student

import androidx.compose.foundation.background
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
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.text.style.TextOverflow
import androidx.compose.ui.unit.dp
import androidx.compose.ui.unit.sp
import com.knowledgehub.mobile.data.model.ChapterDto
import com.knowledgehub.mobile.data.model.CourseExerciseProgress
import com.knowledgehub.mobile.theme.*
import com.knowledgehub.mobile.ui.common.BrandTopBarWithBack

@OptIn(ExperimentalMaterial3Api::class)
@Composable
fun CourseDetailView(
    courseId: String,
    state: StudentUiState,
    onBack: () -> Unit,
    onLoad: (String) -> Unit,
    onStartLearn: (String) -> Unit
) {
    LaunchedEffect(courseId) { onLoad(courseId) }

    Scaffold(
        topBar = {
            BrandTopBarWithBack(
                title = state.detail?.title ?: "课程详情",
                onBack = onBack
            )
        }
    ) { padding ->
        if (state.detailLoading) {
            Box(Modifier.fillMaxSize().padding(padding), Alignment.Center) { CircularProgressIndicator() }
            return@Scaffold
        }
        val detail = state.detail
        if (detail == null) {
            Box(Modifier.fillMaxSize().padding(padding), Alignment.Center) {
                Text(state.error ?: "课程加载失败", color = TextSecondary)
            }
            return@Scaffold
        }

        var selectedTab by remember { mutableIntStateOf(0) }

        Column(modifier = Modifier.fillMaxSize().background(SurfaceBg).padding(padding)) {
            LazyColumn(
                modifier = Modifier.weight(1f),
                contentPadding = PaddingValues(16.dp),
                verticalArrangement = Arrangement.spacedBy(12.dp)
            ) {
                // 课程简介卡片
                item {
                    Card(
                        shape = RoundedCornerShape(12.dp),
                        colors = CardDefaults.cardColors(containerColor = Color.White)
                    ) {
                        Column(Modifier.padding(16.dp)) {
                            Text(detail.title, fontWeight = FontWeight.Bold, fontSize = 20.sp)
                            Spacer(Modifier.height(8.dp))
                            Text(
                                detail.description?.takeIf { it.isNotBlank() } ?: "暂无课程简介",
                                fontSize = 13.sp,
                                color = TextSecondary,
                                lineHeight = 20.sp
                            )
                            Spacer(Modifier.height(14.dp))
                            HorizontalDivider(color = Divider)
                            Spacer(Modifier.height(12.dp))
                            // 主讲教师突出显示
                            Row(verticalAlignment = Alignment.CenterVertically) {
                                Box(
                                    Modifier.size(38.dp).background(BrandBlueLight, RoundedCornerShape(19.dp)),
                                    Alignment.Center
                                ) {
                                    Text(
                                        (detail.teacherName ?: "师").take(1),
                                        color = BrandBlue,
                                        fontWeight = FontWeight.Bold,
                                        fontSize = 17.sp
                                    )
                                }
                                Spacer(Modifier.width(10.dp))
                                Column {
                                    Text("主讲教师", fontSize = 11.sp, color = TextSecondary)
                                    Text(
                                        detail.teacherName ?: "未指定",
                                        fontSize = 14.sp,
                                        fontWeight = FontWeight.Bold
                                    )
                                }
                            }
                            Spacer(Modifier.height(12.dp))
                            Row(horizontalArrangement = Arrangement.spacedBy(20.dp)) {
                                MetaText("专业", detail.majorName ?: "公共课")
                                MetaText("章节", "${detail.chapterCount}")
                                MetaText("学生", "${detail.studentCount}")
                            }
                        }
                    }
                }

                // 学习进度（按习题比例）
                item {
                    Card(
                        shape = RoundedCornerShape(12.dp),
                        colors = CardDefaults.cardColors(containerColor = Color.White)
                    ) {
                        Column(Modifier.padding(16.dp)) {
                            Row(
                                Modifier.fillMaxWidth(),
                                horizontalArrangement = Arrangement.SpaceBetween,
                                verticalAlignment = Alignment.CenterVertically
                            ) {
                                Text("学习进度", fontWeight = FontWeight.Bold, fontSize = 15.sp)
                                Text(
                                    "${exercisePercent(state.exerciseProgress)}%",
                                    color = BrandBlue,
                                    fontWeight = FontWeight.Bold,
                                    fontSize = 16.sp
                                )
                            }
                            Spacer(Modifier.height(10.dp))
                            LinearProgressIndicator(
                                progress = { exercisePercent(state.exerciseProgress) / 100f },
                                modifier = Modifier.fillMaxWidth().height(8.dp),
                                color = AccentTeal,
                                trackColor = BrandBlueLight
                            )
                            Spacer(Modifier.height(8.dp))
                            Text(
                                exerciseProgressText(state.exerciseProgress),
                                fontSize = 11.sp,
                                color = TextSecondary
                            )
                        }
                    }
                }

                // Tab：课程资源 / 章节目录
                item {
                    TabRow(selectedTabIndex = selectedTab, containerColor = Color.White) {
                        Tab(
                            selected = selectedTab == 0,
                            onClick = { selectedTab = 0 },
                            text = { Text("课程资源") }
                        )
                        Tab(
                            selected = selectedTab == 1,
                            onClick = { selectedTab = 1 },
                            text = { Text("章节目录") }
                        )
                    }
                }

                if (selectedTab == 0) {
                    // 课程资源：汇总所有章节的 knowledgeResources
                    val resourceChapters = chaptersWithResources(detail.chapters)
                    if (resourceChapters.isEmpty()) {
                        item {
                            Card(shape = RoundedCornerShape(10.dp), colors = CardDefaults.cardColors(containerColor = Color.White)) {
                                Text("本课程暂无资源", fontSize = 13.sp, color = TextSecondary, modifier = Modifier.padding(20.dp))
                            }
                        }
                    }
                    resourceChapters.forEach { (chapter, resources) ->
                        item {
                            Text(chapter.title, fontWeight = FontWeight.Bold, fontSize = 13.sp, modifier = Modifier.padding(top = 4.dp))
                        }
                        items(resources) { res ->
                            Card(
                                shape = RoundedCornerShape(10.dp),
                                colors = CardDefaults.cardColors(containerColor = Color.White)
                            ) {
                                Row(Modifier.padding(12.dp), verticalAlignment = Alignment.CenterVertically) {
                                    Icon(Icons.Filled.Description, null, tint = BrandBlue, modifier = Modifier.size(20.dp))
                                    Spacer(Modifier.width(10.dp))
                                    Column(Modifier.weight(1f)) {
                                        Text(res.name, fontWeight = FontWeight.Medium, fontSize = 13.sp)
                                        res.description?.takeIf { it.isNotBlank() }?.let {
                                            Text(it, fontSize = 11.sp, color = TextSecondary, maxLines = 1, overflow = TextOverflow.Ellipsis)
                                        }
                                    }
                                    if (res.resourceId != null) {
                                        Text("可预览", fontSize = 10.sp, color = AccentTeal)
                                    }
                                }
                            }
                        }
                    }
                } else {
                    // 章节目录
                    items(flatten(detail.chapters)) { chapter ->
                        Card(shape = RoundedCornerShape(10.dp), colors = CardDefaults.cardColors(containerColor = Color.White)) {
                            Row(Modifier.padding(12.dp), verticalAlignment = Alignment.CenterVertically) {
                                Icon(Icons.Filled.PlayCircle, null, tint = BrandBlue, modifier = Modifier.size(20.dp))
                                Spacer(Modifier.width(10.dp))
                                Text(chapter.title, fontSize = 13.sp, fontWeight = FontWeight.Medium)
                            }
                        }
                    }
                }
            }

            // 底部开始学习按钮
            Surface(shadowElevation = 8.dp, color = Color.White) {
                Button(
                    onClick = { onStartLearn(courseId) },
                    modifier = Modifier
                        .fillMaxWidth()
                        .padding(16.dp)
                        .height(48.dp),
                    shape = RoundedCornerShape(8.dp),
                    colors = ButtonDefaults.buttonColors(containerColor = BrandBlue)
                ) {
                    Icon(Icons.Filled.PlayArrow, null)
                    Spacer(Modifier.width(8.dp))
                    Text("开始学习", fontSize = 15.sp, fontWeight = FontWeight.Medium)
                }
            }
        }
    }
}

private fun exercisePercent(p: CourseExerciseProgress?): Int {
    if (p == null || p.totalCount <= 0) return 0
    return ((p.completedCount.toFloat() / p.totalCount) * 100).toInt().coerceIn(0, 100)
}

private fun exerciseProgressText(p: CourseExerciseProgress?): String {
    if (p == null) return "进度由习题完成情况自动计算"
    return "已学习习题 ${p.completedCount} / 全部习题 ${p.totalCount}（进度按习题完成比例自动计算）"
}

private fun flatten(chapters: List<ChapterDto>): List<ChapterDto> {
    val result = mutableListOf<ChapterDto>()
    fun walk(list: List<ChapterDto>) {
        for (c in list) { result.add(c); walk(c.children) }
    }
    walk(chapters)
    return result
}

private fun chaptersWithResources(chapters: List<ChapterDto>): List<Pair<ChapterDto, List<com.knowledgehub.mobile.data.model.KnowledgeResourceDto>>> {
    val result = mutableListOf<Pair<ChapterDto, List<com.knowledgehub.mobile.data.model.KnowledgeResourceDto>>>()
    fun walk(list: List<ChapterDto>) {
        for (c in list) {
            if (c.knowledgeResources.isNotEmpty()) result.add(c to c.knowledgeResources)
            walk(c.children)
        }
    }
    walk(chapters)
    return result
}

@Composable
private fun MetaText(label: String, value: String) {
    Column {
        Text(label, fontSize = 11.sp, color = TextSecondary)
        Text(value, fontSize = 13.sp, fontWeight = FontWeight.Medium, maxLines = 1)
    }
}
