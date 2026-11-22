package com.knowledgehub.mobile.ui.teacher

import androidx.compose.foundation.background
import androidx.compose.foundation.clickable
import androidx.compose.foundation.layout.*
import androidx.compose.foundation.lazy.LazyColumn
import androidx.compose.foundation.lazy.items
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.filled.MenuBook
import androidx.compose.material3.*
import androidx.compose.runtime.Composable
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.graphics.Brush
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.text.style.TextOverflow
import androidx.compose.ui.unit.dp
import androidx.compose.ui.unit.sp
import com.knowledgehub.mobile.data.model.WorkbenchStatsDto
import com.knowledgehub.mobile.theme.*
import com.knowledgehub.mobile.ui.common.AppModule
import com.knowledgehub.mobile.ui.common.AppModules

/** 教师工作台：教学概览 + 模块入口，结构与学生端一致。 */
@Composable
fun TeacherWorkbenchTab(
    state: TeacherUiState,
    padding: PaddingValues,
    displayName: String?,
    tenantName: String?,
    onOpenModule: (String) -> Unit,
    onOpenCourse: (String, String) -> Unit
) {
    LazyColumn(
        modifier = Modifier.fillMaxSize().background(SurfaceBg).padding(padding),
        contentPadding = PaddingValues(16.dp),
        verticalArrangement = Arrangement.spacedBy(12.dp)
    ) {
        item {
            TeacherHero(
                displayName = displayName,
                tenantName = tenantName,
                stats = state.dashboard
            )
        }

        item { SectionTitle("常用模块", "点击进入") }
        item {
            TeacherModuleGrid(
                modules = AppModules.teacher.filter { it.id != AppModules.WORKBENCH },
                onOpenModule = onOpenModule
            )
        }

        item { SectionTitle("我的课程", "${state.courses.size} 门") }
        if (state.isLoading && state.courses.isEmpty()) {
            item { Box(Modifier.fillMaxWidth().height(120.dp), Alignment.Center) { CircularProgressIndicator() } }
        }
        items(state.courses.take(2)) { course ->
            TeacherCourseRow(
                title = course.title,
                subtitle = "${course.majorName ?: "公共课"} · ${course.chapterCount} 章 · ${course.studentCount} 人",
                onClick = { onOpenCourse(course.id, course.title) }
            )
        }
        if (!state.isLoading && state.courses.isEmpty()) {
            item {
                Card(
                    modifier = Modifier.fillMaxWidth(),
                    shape = RoundedCornerShape(12.dp),
                    colors = CardDefaults.cardColors(containerColor = Color.White)
                ) {
                    Column(Modifier.padding(24.dp), horizontalAlignment = Alignment.CenterHorizontally) {
                        Icon(Icons.Filled.MenuBook, null, tint = TextSecondary, modifier = Modifier.size(36.dp))
                        Spacer(Modifier.height(10.dp))
                        Text("还没有课程", fontWeight = FontWeight.Medium, fontSize = 14.sp)
                        Spacer(Modifier.height(4.dp))
                        Text("到「课程」Tab 创建第一门课程吧", fontSize = 12.sp, color = TextSecondary)
                        TextButton(onClick = { onOpenModule(AppModules.TEACHER_COURSES) }) {
                            Text("去创建课程", color = BrandBlue, fontSize = 13.sp)
                        }
                    }
                }
            }
        }

        item { SectionTitle("更多功能", "即将开放") }
        item { TeacherModuleGrid(modules = AppModules.teacherUpcoming, onOpenModule = onOpenModule) }

        state.error?.let { item { Text(it, color = MaterialTheme.colorScheme.error, fontSize = 13.sp) } }
    }
}

@Composable
private fun TeacherHero(displayName: String?, tenantName: String?, stats: WorkbenchStatsDto?) {
    Box(
        modifier = Modifier
            .fillMaxWidth()
            .background(
                Brush.linearGradient(listOf(BrandBlue, BrandBlueDark)),
                RoundedCornerShape(16.dp)
            )
            .padding(20.dp)
    ) {
        Column {
            Text("欢迎回来，${displayName ?: "老师"}", color = Color.White, fontSize = 19.sp, fontWeight = FontWeight.Bold)
            Spacer(Modifier.height(4.dp))
            Text(
                listOfNotNull(tenantName, "易课通教学工作台").joinToString(" · "),
                color = Color.White.copy(alpha = 0.82f),
                fontSize = 12.sp
            )
            Spacer(Modifier.height(18.dp))
            Row(Modifier.fillMaxWidth(), horizontalArrangement = Arrangement.SpaceBetween) {
                HeroStat("${stats?.courses?.total ?: "--"}", "课程")
                HeroStat("${stats?.courses?.published ?: "--"}", "已发布")
                HeroStat("${stats?.courses?.exerciseCount ?: "--"}", "习题")
                HeroStat("${stats?.courses?.enrollmentCount ?: "--"}", "选课")
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
        Modifier.fillMaxWidth().padding(top = 4.dp),
        horizontalArrangement = Arrangement.SpaceBetween,
        verticalAlignment = Alignment.CenterVertically
    ) {
        Text(title, fontWeight = FontWeight.Bold, fontSize = 17.sp, color = TextPrimary)
        Text(hint, fontSize = 12.sp, color = TextSecondary)
    }
}

@Composable
private fun TeacherModuleGrid(modules: List<AppModule>, onOpenModule: (String) -> Unit) {
    Column(verticalArrangement = Arrangement.spacedBy(12.dp)) {
        modules.chunked(2).forEach { row ->
            Row(horizontalArrangement = Arrangement.spacedBy(12.dp)) {
                row.forEach { module ->
                    Card(
                        modifier = Modifier
                            .weight(1f)
                            .height(112.dp)
                            .clickable(onClick = { onOpenModule(module.id) }),
                        shape = RoundedCornerShape(12.dp),
                        colors = CardDefaults.cardColors(
                            containerColor = if (module.enabled) Color.White else Color(0xFFEEF0F5)
                        )
                    ) {
                        Column(Modifier.padding(14.dp)) {
                            Box(
                                Modifier.size(34.dp).background(module.tint.copy(alpha = 0.12f), RoundedCornerShape(10.dp)),
                                Alignment.Center
                            ) {
                                Icon(module.icon, null, tint = module.tint, modifier = Modifier.size(20.dp))
                            }
                            Spacer(Modifier.height(10.dp))
                            Text(module.label, fontSize = 14.sp, fontWeight = FontWeight.Bold, color = if (module.enabled) TextPrimary else TextSecondary)
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
                if (row.size == 1) Spacer(Modifier.weight(1f))
            }
        }
    }
}

@Composable
private fun TeacherCourseRow(title: String, subtitle: String, onClick: () -> Unit) {
    Card(
        modifier = Modifier.fillMaxWidth().clickable(onClick = onClick),
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
                Text(title, fontWeight = FontWeight.Bold, fontSize = 14.sp, maxLines = 1, overflow = TextOverflow.Ellipsis)
                Spacer(Modifier.height(3.dp))
                Text(subtitle, fontSize = 11.sp, color = TextSecondary, maxLines = 1, overflow = TextOverflow.Ellipsis)
            }
            Text("学情 ›", color = BrandBlue, fontSize = 12.sp)
        }
    }
}
