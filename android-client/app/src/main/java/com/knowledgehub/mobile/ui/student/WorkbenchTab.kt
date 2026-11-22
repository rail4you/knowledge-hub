package com.knowledgehub.mobile.ui.student

import androidx.compose.foundation.background
import androidx.compose.foundation.clickable
import androidx.compose.foundation.layout.*
import androidx.compose.foundation.lazy.LazyColumn
import androidx.compose.foundation.lazy.items
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.filled.School
import androidx.compose.material3.*
import androidx.compose.runtime.Composable
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.graphics.Brush
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.layout.ContentScale
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.text.style.TextOverflow
import androidx.compose.ui.unit.dp
import androidx.compose.ui.unit.sp
import coil.compose.AsyncImage
import com.knowledgehub.mobile.data.model.LearningDashboard
import com.knowledgehub.mobile.theme.*
import com.knowledgehub.mobile.ui.common.AppModule
import com.knowledgehub.mobile.ui.common.AppModules

/**
 * 学生端工作台：仿常见安卓首页的信息架构。
 * 顶部是欢迎与学习概览，中间是常用模块快捷入口，下面是继续学习和即将开放模块。
 * 所有入口都来自 AppModules 注册表，新增模块无需改这里。
 */
@Composable
fun WorkbenchTab(
    state: StudentUiState,
    padding: PaddingValues,
    displayName: String?,
    tenantName: String?,
    onOpenModule: (String) -> Unit,
    onOpenDetail: (String) -> Unit,
    onContinueLearn: (String) -> Unit
) {
    LazyColumn(
        modifier = Modifier
            .fillMaxSize()
            .background(SurfaceBg)
            .padding(padding),
        contentPadding = PaddingValues(16.dp),
        verticalArrangement = Arrangement.spacedBy(12.dp)
    ) {
        item {
            WorkbenchHero(
                displayName = displayName,
                tenantName = tenantName,
                dashboard = state.dashboard
            )
        }

        item {
            SectionTitle(title = "常用模块", hint = "点击进入")
        }
        item {
            ModuleGrid(
                modules = AppModules.student.filter { it.id != AppModules.WORKBENCH },
                onOpenModule = onOpenModule
            )
        }

        item {
            SectionTitle(title = "继续学习", hint = "${state.myCourses.size} 门课程")
        }
        if (state.isLoading && state.myCourses.isEmpty()) {
            item {
                Box(Modifier.fillMaxWidth().height(120.dp), Alignment.Center) {
                    CircularProgressIndicator()
                }
            }
        }
        items(state.myCourses.take(2)) { course ->
            WorkbenchCourseCard(
                title = course.courseTitle,
                subtitle = "${course.majorName ?: "公共课"} · ${course.chapterCount} 章",
                progress = course.progress,
                exercises = "${course.completedExerciseCount}/${course.totalExerciseCount} 题",
                coverUrl = course.courseCoverImageUrl,
                onClick = { onOpenDetail(course.courseId) },
                onContinue = { onContinueLearn(course.courseId) }
            )
        }
        if (!state.isLoading && state.myCourses.isEmpty()) {
            item {
                EmptyLearningCard(onOpenCourseCenter = { onOpenModule(AppModules.COURSE_CENTER) })
            }
        }
        item {
            Box(Modifier.fillMaxWidth(), Alignment.CenterEnd) {
                TextButton(onClick = { onOpenModule(AppModules.MY_LEARNING) }) {
                    Text("查看全部课程 ›", fontSize = 13.sp, color = BrandBlue)
                }
            }
        }

        item {
            SectionTitle(title = "更多功能", hint = "即将开放")
        }
        item {
            ModuleGrid(modules = AppModules.studentUpcoming, onOpenModule = onOpenModule)
        }

        state.error?.let { error ->
            item { Text(error, color = MaterialTheme.colorScheme.error, fontSize = 13.sp) }
        }
    }
}

@Composable
private fun WorkbenchHero(
    displayName: String?,
    tenantName: String?,
    dashboard: LearningDashboard?
) {
    Box(
        modifier = Modifier
            .fillMaxWidth()
            .background(
                brush = Brush.linearGradient(listOf(BrandBlue, BrandBlueDark)),
                shape = RoundedCornerShape(16.dp)
            )
            .padding(20.dp)
    ) {
        Column {
            Text(
                text = "欢迎回来，${displayName ?: "同学"}",
                color = Color.White,
                fontSize = 19.sp,
                fontWeight = FontWeight.Bold
            )
            Spacer(Modifier.height(4.dp))
            Text(
                text = listOfNotNull(tenantName, "易课通学习工作台").joinToString(" · "),
                color = Color.White.copy(alpha = 0.82f),
                fontSize = 12.sp
            )
            Spacer(Modifier.height(18.dp))
            Row(Modifier.fillMaxWidth(), horizontalArrangement = Arrangement.SpaceBetween) {
                HeroStat("${dashboard?.inProgressCourses ?: "--"}", "学习中")
                HeroStat("${dashboard?.completedCourses ?: "--"}", "已完成")
                HeroStat("${dashboard?.totalExerciseRecords ?: "--"}", "习题记录")
                HeroStat(
                    dashboard?.totalLearningTime?.let { "${it.toInt()}分" } ?: "--",
                    "累计时长"
                )
            }
        }
    }
}

@Composable
private fun HeroStat(value: String, label: String) {
    Column(horizontalAlignment = Alignment.CenterHorizontally) {
        Text(value, color = Color.White, fontSize = 17.sp, fontWeight = FontWeight.Bold)
        Spacer(Modifier.height(2.dp))
        Text(label, color = Color.White.copy(alpha = 0.78f), fontSize = 11.sp)
    }
}

@Composable
private fun SectionTitle(title: String, hint: String) {
    Row(
        modifier = Modifier.fillMaxWidth().padding(top = 4.dp),
        horizontalArrangement = Arrangement.SpaceBetween,
        verticalAlignment = Alignment.CenterVertically
    ) {
        Text(title, fontWeight = FontWeight.Bold, fontSize = 17.sp, color = TextPrimary)
        Text(hint, fontSize = 12.sp, color = TextSecondary)
    }
}

@Composable
private fun ModuleGrid(
    modules: List<AppModule>,
    onOpenModule: (String) -> Unit
) {
    Column(verticalArrangement = Arrangement.spacedBy(12.dp)) {
        modules.chunked(2).forEach { row ->
            Row(horizontalArrangement = Arrangement.spacedBy(12.dp)) {
                row.forEach { module ->
                    ModuleCard(
                        module = module,
                        modifier = Modifier.weight(1f),
                        onClick = { onOpenModule(module.id) }
                    )
                }
                if (row.size == 1) Spacer(Modifier.weight(1f))
            }
        }
    }
}

@Composable
private fun ModuleCard(
    module: AppModule,
    modifier: Modifier = Modifier,
    onClick: () -> Unit
) {
    Card(
        modifier = modifier
            .height(112.dp)
            .clickable(onClick = onClick),
        shape = RoundedCornerShape(12.dp),
        colors = CardDefaults.cardColors(
            containerColor = if (module.enabled) Color.White else Color(0xFFEEF0F5)
        ),
        elevation = CardDefaults.cardElevation(defaultElevation = if (module.enabled) 1.dp else 0.dp)
    ) {
        Column(Modifier.padding(14.dp)) {
            Box(
                modifier = Modifier
                    .size(34.dp)
                    .background(module.tint.copy(alpha = 0.12f), RoundedCornerShape(10.dp)),
                contentAlignment = Alignment.Center
            ) {
                Icon(module.icon, contentDescription = null, tint = module.tint, modifier = Modifier.size(20.dp))
            }
            Spacer(Modifier.height(10.dp))
            Text(
                module.label,
                fontSize = 14.sp,
                fontWeight = FontWeight.Bold,
                color = if (module.enabled) TextPrimary else TextSecondary,
                maxLines = 1
            )
            Text(
                if (module.enabled) module.description else "即将开放",
                fontSize = 11.sp,
                color = TextSecondary,
                maxLines = 1,
                overflow = TextOverflow.Ellipsis
            )
        }
    }
}

@Composable
private fun WorkbenchCourseCard(
    title: String,
    subtitle: String,
    progress: Double,
    exercises: String,
    coverUrl: String?,
    onClick: () -> Unit,
    onContinue: () -> Unit
) {
    Card(
        modifier = Modifier.fillMaxWidth(),
        shape = RoundedCornerShape(12.dp),
        colors = CardDefaults.cardColors(containerColor = Color.White),
        elevation = CardDefaults.cardElevation(defaultElevation = 1.dp)
    ) {
        Row(Modifier.padding(14.dp), verticalAlignment = Alignment.CenterVertically) {
            Box(
                Modifier
                    .size(48.dp)
                    .background(BrandBlueLight, RoundedCornerShape(8.dp))
                    .clickable(onClick = onClick),
                Alignment.Center
            ) {
                if (!coverUrl.isNullOrBlank()) {
                    AsyncImage(
                        model = coverUrl,
                        contentDescription = null,
                        contentScale = ContentScale.Crop,
                        modifier = Modifier.size(48.dp).background(BrandBlueLight, RoundedCornerShape(8.dp))
                    )
                } else {
                    Text("课", color = BrandBlue, fontWeight = FontWeight.Bold, fontSize = 18.sp)
                }
            }
            Spacer(Modifier.width(12.dp))
            Column(
                Modifier
                    .weight(1f)
                    .clickable(onClick = onClick)
            ) {
                Text(title, fontWeight = FontWeight.Bold, fontSize = 14.sp, maxLines = 1, overflow = TextOverflow.Ellipsis)
                Spacer(Modifier.height(3.dp))
                Text(subtitle, fontSize = 11.sp, color = TextSecondary, maxLines = 1, overflow = TextOverflow.Ellipsis)
                Spacer(Modifier.height(7.dp))
                LinearProgressIndicator(
                    progress = { (progress / 100.0).coerceIn(0.0, 1.0).toFloat() },
                    modifier = Modifier.fillMaxWidth().height(5.dp),
                    color = AccentTeal,
                    trackColor = BrandBlueLight
                )
                Spacer(Modifier.height(3.dp))
                Text("进度 ${progress.toInt()}% · $exercises", fontSize = 10.sp, color = TextSecondary)
            }
            Spacer(Modifier.width(8.dp))
            FilledTonalButton(
                onClick = onContinue,
                modifier = Modifier.height(34.dp),
                contentPadding = PaddingValues(horizontal = 12.dp)
            ) {
                Text("继续", fontSize = 12.sp)
            }
        }
    }
}

@Composable
private fun EmptyLearningCard(onOpenCourseCenter: () -> Unit) {
    Card(
        modifier = Modifier.fillMaxWidth(),
        shape = RoundedCornerShape(12.dp),
        colors = CardDefaults.cardColors(containerColor = Color.White)
    ) {
        Column(Modifier.padding(24.dp), horizontalAlignment = Alignment.CenterHorizontally) {
            Icon(Icons.Filled.School, null, tint = TextSecondary, modifier = Modifier.size(36.dp))
            Spacer(Modifier.height(10.dp))
            Text("还没有学习记录", fontWeight = FontWeight.Medium, fontSize = 14.sp)
            Spacer(Modifier.height(4.dp))
            Text("去课程中心挑一门感兴趣的课程吧", fontSize = 12.sp, color = TextSecondary)
            TextButton(onClick = onOpenCourseCenter) {
                Text("去课程中心", color = BrandBlue, fontSize = 13.sp)
            }
        }
    }
}
