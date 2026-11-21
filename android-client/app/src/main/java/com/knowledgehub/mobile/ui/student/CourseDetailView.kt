package com.knowledgehub.mobile.ui.student

import androidx.compose.foundation.ExperimentalFoundationApi
import androidx.compose.foundation.background
import androidx.compose.foundation.border
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
import androidx.compose.ui.draw.clip
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.layout.ContentScale
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.text.style.TextOverflow
import androidx.compose.ui.unit.dp
import androidx.compose.ui.unit.sp
import coil.compose.AsyncImage
import com.knowledgehub.mobile.data.model.ChapterDto
import com.knowledgehub.mobile.data.model.CourseDetailDto
import com.knowledgehub.mobile.data.model.CourseExerciseProgress
import com.knowledgehub.mobile.data.model.KnowledgeResourceDto
import com.knowledgehub.mobile.theme.*
import com.knowledgehub.mobile.ui.common.BrandTopBarWithBack

private data class DetailFlatChapter(
    val chapter: ChapterDto,
    val depth: Int,
    val hasChildren: Boolean
)

private data class ResourceChapter(
    val chapter: ChapterDto,
    val depth: Int,
    val resources: List<KnowledgeResourceDto>
)

@OptIn(ExperimentalMaterial3Api::class, ExperimentalFoundationApi::class)
@Composable
fun CourseDetailView(
    courseId: String,
    state: StudentUiState,
    onBack: () -> Unit,
    onLoad: (String) -> Unit,
    onStartLearn: (String, String?) -> Unit,
    onEnroll: (String) -> Unit
) {
    LaunchedEffect(courseId) { onLoad(courseId) }

    var selectedTab by remember(courseId) { mutableIntStateOf(0) }
    var expanded by remember(courseId) { mutableStateOf<Set<String>>(emptySet()) }

    LaunchedEffect(state.detail?.id) {
        expanded = setOfNotNull(state.detail?.chapters?.firstOrNull()?.id)
    }

    Scaffold(
        topBar = {
            BrandTopBarWithBack(
                title = state.detail?.title ?: "课程详情",
                onBack = onBack
            )
        }
    ) { padding ->
        if (state.detailLoading) {
            Box(Modifier.fillMaxSize().padding(padding), Alignment.Center) {
                CircularProgressIndicator()
            }
            return@Scaffold
        }

        val detail = state.detail
        if (detail == null) {
            Box(Modifier.fillMaxSize().padding(padding), Alignment.Center) {
                Text(state.error ?: "课程加载失败", color = TextSecondary)
            }
            return@Scaffold
        }

        Column(
            Modifier
                .fillMaxSize()
                .background(SurfaceBg)
                .padding(padding)
        ) {
            LazyColumn(
                modifier = Modifier.weight(1f),
                contentPadding = PaddingValues(16.dp),
                verticalArrangement = Arrangement.spacedBy(12.dp)
            ) {
                item { CourseHero(detail) }
                item { CourseInfoCard(detail) }
                item { CourseProgressCard(state.exerciseProgress) }

                stickyHeader {
                    TabRow(
                        selectedTabIndex = selectedTab,
                        containerColor = SurfaceBg,
                        contentColor = BrandBlue
                    ) {
                        Tab(
                            selected = selectedTab == 0,
                            onClick = { selectedTab = 0 },
                            text = { Text("课程资源", fontSize = 13.sp) },
                            selectedContentColor = BrandBlue,
                            unselectedContentColor = TextSecondary
                        )
                        Tab(
                            selected = selectedTab == 1,
                            onClick = { selectedTab = 1 },
                            text = { Text("章节目录", fontSize = 13.sp) },
                            selectedContentColor = BrandBlue,
                            unselectedContentColor = TextSecondary
                        )
                    }
                }

                if (selectedTab == 0) {
                    val resourceChapters = collectResourceChapters(detail.chapters, 0)
                    if (resourceChapters.isEmpty()) {
                        item { EmptyHintCard("本课程暂无资源", "教师上传课程资源后会显示在这里") }
                    }
                    resourceChapters.forEach { entry ->
                        item {
                            Text(
                                "${"　".repeat(entry.depth)}${entry.chapter.title}",
                                fontWeight = FontWeight.Bold,
                                fontSize = 13.sp,
                                color = TextPrimary,
                                modifier = Modifier.padding(top = 4.dp)
                            )
                        }
                        items(entry.resources) { res -> ResourceRow(res) }
                    }
                } else {
                    item {
                        Text(
                            "共 ${detail.chapterCount} 章 · 点击章节直接进入学习",
                            fontSize = 12.sp,
                            color = TextSecondary
                        )
                    }
                    val flat = flattenDetailChapters(detail.chapters, expanded)
                    items(flat, key = { it.chapter.id }) { entry ->
                        DetailChapterRow(
                            entry = entry,
                            expanded = expanded.contains(entry.chapter.id),
                            onToggle = {
                                expanded = if (expanded.contains(entry.chapter.id)) {
                                    expanded - entry.chapter.id
                                } else {
                                    expanded + entry.chapter.id
                                }
                            },
                            onOpen = { onStartLearn(courseId, entry.chapter.id) }
                        )
                    }
                }
            }

            Surface(shadowElevation = 8.dp, color = Color.White) {
                Button(
                    onClick = {
                        if (detail.isEnrolled) onStartLearn(courseId, null) else onEnroll(courseId)
                    },
                    modifier = Modifier
                        .fillMaxWidth()
                        .padding(16.dp)
                        .height(48.dp),
                    shape = RoundedCornerShape(8.dp),
                    colors = ButtonDefaults.buttonColors(containerColor = BrandBlue)
                ) {
                    Icon(if (detail.isEnrolled) Icons.Filled.PlayArrow else Icons.Filled.HowToReg, null)
                    Spacer(Modifier.width(8.dp))
                    Text(
                        if (detail.isEnrolled) "开始学习" else "报名学习",
                        fontSize = 15.sp,
                        fontWeight = FontWeight.Medium
                    )
                }
                if (!detail.isEnrolled) {
                    Text(
                        "未选课也可浏览课程简介、章节和资源；报名后即可提交习题并记录进度",
                        fontSize = 11.sp,
                        color = TextSecondary,
                        modifier = Modifier.padding(start = 16.dp, end = 16.dp, bottom = 12.dp)
                    )
                }
            }
        }
    }
}

@Composable
private fun CourseHero(detail: CourseDetailDto) {
    Card(
        modifier = Modifier.fillMaxWidth(),
        shape = RoundedCornerShape(16.dp)
    ) {
        Box(Modifier.fillMaxWidth().height(176.dp)) {
            if (!detail.coverImageUrl.isNullOrBlank()) {
                AsyncImage(
                    model = detail.coverImageUrl,
                    contentDescription = null,
                    contentScale = ContentScale.Crop,
                    modifier = Modifier.fillMaxSize()
                )
            } else {
                Box(
                    Modifier.fillMaxSize().background(BrandBlueLight),
                    Alignment.Center
                ) {
                    Icon(
                        Icons.Filled.School,
                        null,
                        tint = BrandBlue,
                        modifier = Modifier.size(56.dp)
                    )
                }
            }
            Box(Modifier.fillMaxSize().background(Color(0x8A0A1633)))
            Column(
                Modifier
                    .align(Alignment.BottomStart)
                    .padding(16.dp)
            ) {
                Surface(
                    color = BrandBlue,
                    shape = RoundedCornerShape(6.dp)
                ) {
                    Text(
                        "课程",
                        color = Color.White,
                        fontSize = 11.sp,
                        fontWeight = FontWeight.Bold,
                        modifier = Modifier.padding(horizontal = 8.dp, vertical = 3.dp)
                    )
                }
                Spacer(Modifier.height(8.dp))
                Text(
                    detail.title,
                    color = Color.White,
                    fontSize = 20.sp,
                    fontWeight = FontWeight.Bold,
                    maxLines = 2,
                    overflow = TextOverflow.Ellipsis
                )
            }
        }
    }
}

@Composable
private fun CourseInfoCard(detail: CourseDetailDto) {
    Card(
        modifier = Modifier.fillMaxWidth(),
        shape = RoundedCornerShape(14.dp),
        colors = CardDefaults.cardColors(containerColor = Color.White)
    ) {
        Column(Modifier.padding(16.dp)) {
            Text("课程信息", fontWeight = FontWeight.Bold, fontSize = 15.sp)
            Spacer(Modifier.height(12.dp))

            Surface(
                modifier = Modifier.fillMaxWidth(),
                shape = RoundedCornerShape(12.dp),
                color = BrandBlueLight
            ) {
                Row(Modifier.padding(12.dp), verticalAlignment = Alignment.CenterVertically) {
                    Box(
                        Modifier.size(44.dp).background(BrandBlue, RoundedCornerShape(22.dp)),
                        Alignment.Center
                    ) {
                        Text(
                            (detail.teacherName ?: "师").take(1),
                            color = Color.White,
                            fontSize = 19.sp,
                            fontWeight = FontWeight.Bold
                        )
                    }
                    Spacer(Modifier.width(12.dp))
                    Column(Modifier.weight(1f)) {
                        Text("主讲教师", fontSize = 11.sp, color = TextSecondary)
                        Text(
                            detail.teacherName ?: "未指定",
                            fontSize = 16.sp,
                            fontWeight = FontWeight.Bold,
                            color = TextPrimary
                        )
                    }
                    Icon(Icons.Filled.Person, null, tint = BrandBlue, modifier = Modifier.size(20.dp))
                }
            }

            Spacer(Modifier.height(12.dp))
            Row(Modifier.fillMaxWidth(), horizontalArrangement = Arrangement.spacedBy(10.dp)) {
                InfoTile("专业", detail.majorName?.takeIf { it.isNotBlank() } ?: "公共课", Modifier.weight(1f))
                InfoTile("学期", detail.semester?.takeIf { it.isNotBlank() } ?: "未设置", Modifier.weight(1f))
            }
            Spacer(Modifier.height(10.dp))
            Row(Modifier.fillMaxWidth(), horizontalArrangement = Arrangement.spacedBy(10.dp)) {
                InfoTile("章节", "${detail.chapterCount} 章", Modifier.weight(1f))
                InfoTile("学生", "${detail.studentCount} 人", Modifier.weight(1f))
            }
            if (detail.credits != null || detail.semesterHours != null) {
                Spacer(Modifier.height(10.dp))
                Row(Modifier.fillMaxWidth(), horizontalArrangement = Arrangement.spacedBy(10.dp)) {
                    InfoTile("学分", detail.credits?.toString() ?: "—", Modifier.weight(1f))
                    InfoTile("学时", detail.semesterHours?.let { "$it 学时" } ?: "—", Modifier.weight(1f))
                }
            }

            Spacer(Modifier.height(14.dp))
            HorizontalDivider(color = Divider)
            Spacer(Modifier.height(12.dp))
            Text("课程简介", fontWeight = FontWeight.Bold, fontSize = 13.sp)
            Spacer(Modifier.height(6.dp))
            Text(
                detail.description?.takeIf { it.isNotBlank() } ?: "暂无课程简介",
                fontSize = 13.sp,
                color = TextSecondary,
                lineHeight = 20.sp
            )
        }
    }
}

@Composable
private fun InfoTile(label: String, value: String, modifier: Modifier = Modifier) {
    Column(
        modifier
            .clip(RoundedCornerShape(10.dp))
            .background(Color(0xFFF8F9FC))
            .border(1.dp, Divider, RoundedCornerShape(10.dp))
            .padding(12.dp)
    ) {
        Text(label, fontSize = 11.sp, color = TextSecondary)
        Spacer(Modifier.height(4.dp))
        Text(
            value,
            fontSize = 14.sp,
            fontWeight = FontWeight.Medium,
            color = TextPrimary,
            maxLines = 2,
            overflow = TextOverflow.Ellipsis
        )
    }
}

@Composable
private fun CourseProgressCard(progress: CourseExerciseProgress?) {
    Card(
        modifier = Modifier.fillMaxWidth(),
        shape = RoundedCornerShape(14.dp),
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
                    "${exercisePercent(progress)}%",
                    color = BrandBlue,
                    fontWeight = FontWeight.Bold,
                    fontSize = 18.sp
                )
            }
            Spacer(Modifier.height(10.dp))
            LinearProgressIndicator(
                progress = { exercisePercent(progress) / 100f },
                modifier = Modifier.fillMaxWidth().height(8.dp),
                color = AccentTeal,
                trackColor = BrandBlueLight
            )
            Spacer(Modifier.height(8.dp))
            Text(exerciseProgressText(progress), fontSize = 11.sp, color = TextSecondary)
        }
    }
}

@Composable
private fun ResourceRow(res: KnowledgeResourceDto) {
    Card(
        modifier = Modifier.fillMaxWidth(),
        shape = RoundedCornerShape(10.dp),
        colors = CardDefaults.cardColors(containerColor = Color.White)
    ) {
        Row(Modifier.padding(12.dp), verticalAlignment = Alignment.CenterVertically) {
            Icon(Icons.Filled.Description, null, tint = BrandBlue, modifier = Modifier.size(20.dp))
            Spacer(Modifier.width(10.dp))
            Column(Modifier.weight(1f)) {
                Text(
                    res.originalFileName?.takeIf { it.isNotBlank() } ?: res.name,
                    fontWeight = FontWeight.Medium,
                    fontSize = 13.sp,
                    maxLines = 1,
                    overflow = TextOverflow.Ellipsis
                )
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

@Composable
private fun DetailChapterRow(
    entry: DetailFlatChapter,
    expanded: Boolean,
    onToggle: () -> Unit,
    onOpen: () -> Unit
) {
    Card(
        modifier = Modifier.fillMaxWidth().clickable(onClick = onOpen),
        shape = RoundedCornerShape(10.dp),
        colors = CardDefaults.cardColors(containerColor = Color.White)
    ) {
        Row(
            Modifier
                .fillMaxWidth()
                .padding(start = (10 + entry.depth * 18).dp, end = 12.dp, top = 6.dp, bottom = 6.dp),
            verticalAlignment = Alignment.CenterVertically
        ) {
            if (entry.hasChildren) {
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
            Icon(Icons.Filled.PlayCircle, null, tint = BrandBlue, modifier = Modifier.size(20.dp))
            Spacer(Modifier.width(10.dp))
            Column(Modifier.weight(1f)) {
                Text(
                    entry.chapter.title,
                    fontSize = 14.sp,
                    fontWeight = if (entry.depth == 0) FontWeight.Bold else FontWeight.Medium,
                    maxLines = 2,
                    overflow = TextOverflow.Ellipsis
                )
                val meta = buildList {
                    add("${entry.chapter.knowledgeResources.size} 个资源")
                    if (entry.hasChildren) add("${entry.chapter.children.size} 个子章节")
                }.joinToString(" · ")
                Text(meta, fontSize = 11.sp, color = TextSecondary)
            }
            Icon(Icons.Filled.ChevronRight, null, tint = TextSecondary, modifier = Modifier.size(20.dp))
        }
    }
}

@Composable
private fun EmptyHintCard(title: String, desc: String) {
    Card(
        modifier = Modifier.fillMaxWidth(),
        shape = RoundedCornerShape(12.dp),
        colors = CardDefaults.cardColors(containerColor = Color.White)
    ) {
        Column(
            Modifier.fillMaxWidth().padding(32.dp),
            horizontalAlignment = Alignment.CenterHorizontally
        ) {
            Icon(Icons.Filled.Inbox, null, tint = TextSecondary, modifier = Modifier.size(34.dp))
            Spacer(Modifier.height(10.dp))
            Text(title, fontSize = 14.sp, fontWeight = FontWeight.Medium)
            Spacer(Modifier.height(4.dp))
            Text(desc, fontSize = 12.sp, color = TextSecondary)
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

private fun flattenDetailChapters(
    chapters: List<ChapterDto>,
    expanded: Set<String>
): List<DetailFlatChapter> {
    val result = mutableListOf<DetailFlatChapter>()
    fun walk(list: List<ChapterDto>, depth: Int) {
        for (chapter in list) {
            result.add(
                DetailFlatChapter(
                    chapter = chapter,
                    depth = depth,
                    hasChildren = chapter.children.isNotEmpty()
                )
            )
            if (expanded.contains(chapter.id)) walk(chapter.children, depth + 1)
        }
    }
    walk(chapters, 0)
    return result
}

private fun collectResourceChapters(
    chapters: List<ChapterDto>,
    depth: Int
): List<ResourceChapter> {
    val result = mutableListOf<ResourceChapter>()
    fun walk(list: List<ChapterDto>, currentDepth: Int) {
        for (chapter in list) {
            if (chapter.knowledgeResources.isNotEmpty()) {
                result.add(ResourceChapter(chapter, currentDepth, chapter.knowledgeResources))
            }
            walk(chapter.children, currentDepth + 1)
        }
    }
    walk(chapters, depth)
    return result
}
