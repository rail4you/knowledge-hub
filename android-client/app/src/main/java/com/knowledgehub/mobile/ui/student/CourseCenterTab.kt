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

@OptIn(ExperimentalMaterial3Api::class)
@Composable
fun CourseCenterTab(
    state: StudentUiState,
    padding: PaddingValues,
    viewModel: StudentViewModel,
    onOpenDetail: (String) -> Unit
) {
    var majorMenuExpanded by remember { mutableStateOf(false) }
    val selectedMajorName = state.majors.firstOrNull { it.id == state.majorId }?.name ?: "全部专业"

    Column(
        Modifier
            .fillMaxSize()
            .background(SurfaceBg)
            .padding(padding)
    ) {
        Column(Modifier.padding(horizontal = 16.dp, vertical = 12.dp)) {
            Text("课程中心", fontWeight = FontWeight.Bold, fontSize = 18.sp)
            Spacer(Modifier.height(10.dp))
            OutlinedTextField(
                value = state.courseKeyword,
                onValueChange = { viewModel.setCourseKeyword(it) },
                modifier = Modifier.fillMaxWidth(),
                singleLine = true,
                placeholder = { Text("搜索课程名称或简介", fontSize = 13.sp) },
                leadingIcon = { Icon(Icons.Filled.Search, null, modifier = Modifier.size(18.dp)) },
                trailingIcon = {
                    if (state.courseKeyword.isNotBlank()) {
                        IconButton(onClick = { viewModel.setCourseKeyword("") }) {
                            Icon(Icons.Filled.Close, null, modifier = Modifier.size(18.dp))
                        }
                    }
                },
                shape = RoundedCornerShape(10.dp)
            )
            Spacer(Modifier.height(10.dp))
            Row(
                Modifier.fillMaxWidth(),
                horizontalArrangement = Arrangement.spacedBy(8.dp),
                verticalAlignment = Alignment.CenterVertically
            ) {
                CourseCenterScope.entries.forEach { scope ->
                    FilterChip(
                        selected = state.courseScope == scope,
                        onClick = { viewModel.setCourseScope(scope) },
                        label = { Text(scope.label, fontSize = 12.sp) }
                    )
                }
                Spacer(Modifier.weight(1f))
                Box {
                    AssistChip(
                        onClick = { majorMenuExpanded = true },
                        label = { Text(selectedMajorName, fontSize = 12.sp) },
                        trailingIcon = { Icon(Icons.Filled.ArrowDropDown, null, modifier = Modifier.size(16.dp)) }
                    )
                    DropdownMenu(
                        expanded = majorMenuExpanded,
                        onDismissRequest = { majorMenuExpanded = false }
                    ) {
                        DropdownMenuItem(
                            text = { Text("全部专业") },
                            onClick = { viewModel.setMajor(null); majorMenuExpanded = false }
                        )
                        state.majors.forEach { major ->
                            DropdownMenuItem(
                                text = { Text(major.name) },
                                onClick = { viewModel.setMajor(major.id); majorMenuExpanded = false }
                            )
                        }
                    }
                }
            }
        }

        LazyColumn(
            modifier = Modifier.weight(1f).fillMaxWidth(),
            contentPadding = PaddingValues(start = 16.dp, end = 16.dp, bottom = 16.dp),
            verticalArrangement = Arrangement.spacedBy(10.dp)
        ) {
            item {
                Text(
                    "共 ${state.courseCenterItems.size} 门课程 · 未选课也可查看详情并预览资源",
                    fontSize = 12.sp,
                    color = TextSecondary
                )
            }

            if (state.courseCenterLoading && state.courseCenterItems.isEmpty()) {
                item {
                    Box(Modifier.fillMaxWidth().height(160.dp), Alignment.Center) {
                        CircularProgressIndicator()
                    }
                }
            }

            items(state.courseCenterItems, key = { "${it.scope.name}-${it.courseId}" }) { course ->
                CenterCourseCard(course = course, onClick = { onOpenDetail(course.courseId) })
            }

            if (!state.courseCenterLoading && state.courseCenterItems.isEmpty() && state.courseCenterError == null) {
                item {
                    Card(
                        modifier = Modifier.fillMaxWidth(),
                        shape = RoundedCornerShape(12.dp),
                        colors = CardDefaults.cardColors(containerColor = Color.White)
                    ) {
                        Column(
                            Modifier.fillMaxWidth().padding(32.dp),
                            horizontalAlignment = Alignment.CenterHorizontally
                        ) {
                            Icon(Icons.Filled.School, null, tint = TextSecondary, modifier = Modifier.size(36.dp))
                            Spacer(Modifier.height(8.dp))
                            Text("暂无课程", fontSize = 14.sp, fontWeight = FontWeight.Medium)
                            Spacer(Modifier.height(4.dp))
                            Text("可切换课程范围或清除筛选条件", fontSize = 12.sp, color = TextSecondary)
                        }
                    }
                }
            }

            state.courseCenterError?.let { error ->
                item {
                    Card(
                        modifier = Modifier.fillMaxWidth(),
                        colors = CardDefaults.cardColors(containerColor = Color.White)
                    ) {
                        Column(Modifier.padding(16.dp)) {
                            Text(error, fontSize = 13.sp, color = MaterialTheme.colorScheme.error)
                            TextButton(onClick = { viewModel.loadExplore() }) { Text("重新加载") }
                        }
                    }
                }
            }
        }
    }
}

@Composable
private fun CenterCourseCard(course: CourseCenterItem, onClick: () -> Unit) {
    Card(
        modifier = Modifier.fillMaxWidth().clickable(onClick = onClick),
        shape = RoundedCornerShape(12.dp),
        colors = CardDefaults.cardColors(containerColor = Color.White),
        elevation = CardDefaults.cardElevation(defaultElevation = 1.dp)
    ) {
        Row(Modifier.padding(12.dp), verticalAlignment = Alignment.Top) {
            Box(
                Modifier.size(64.dp).background(BrandBlueLight, RoundedCornerShape(8.dp)),
                Alignment.Center
            ) {
                if (!course.coverImageUrl.isNullOrBlank()) {
                    AsyncImage(
                        model = course.coverImageUrl,
                        contentDescription = null,
                        contentScale = ContentScale.Crop,
                        modifier = Modifier.size(64.dp).background(BrandBlueLight, RoundedCornerShape(8.dp))
                    )
                } else {
                    Text("课", color = BrandBlue, fontWeight = FontWeight.Bold, fontSize = 22.sp)
                }
            }
            Spacer(Modifier.width(12.dp))
            Column(Modifier.weight(1f)) {
                Text(
                    course.title,
                    fontWeight = FontWeight.Bold,
                    fontSize = 15.sp,
                    maxLines = 2,
                    overflow = TextOverflow.Ellipsis
                )
                Spacer(Modifier.height(4.dp))
                val major = course.majorNames.joinToString("、").ifBlank { "公共课" }
                val teacher = course.teacherName?.takeIf { it.isNotBlank() }
                Text(
                    listOfNotNull(major, teacher?.let { "主讲 $it" }).joinToString(" · "),
                    fontSize = 12.sp,
                    color = TextSecondary,
                    maxLines = 1,
                    overflow = TextOverflow.Ellipsis
                )
                Spacer(Modifier.height(6.dp))
                Row(horizontalArrangement = Arrangement.spacedBy(6.dp)) {
                    SourceBadge(course.scope.label)
                    val source = course.sourceTenantName ?: course.ownerTenantName
                    if (!source.isNullOrBlank()) SourceBadge(source)
                    if (course.isEnrolled) SourceBadge("已选课", AccentTeal)
                }
                Spacer(Modifier.height(6.dp))
                val meta = buildList {
                    if (course.chapterCount > 0) add("${course.chapterCount} 章")
                    if (course.semesterHours != null) add("${course.semesterHours} 学时")
                    if (course.credits != null) add("${course.credits} 学分")
                    if (course.studentCount > 0) add("${course.studentCount} 人学习")
                }.joinToString(" · ")
                if (meta.isNotBlank()) {
                    Text(meta, fontSize = 11.sp, color = TextSecondary, maxLines = 1, overflow = TextOverflow.Ellipsis)
                }
                if (course.isEnrolled) {
                    Spacer(Modifier.height(6.dp))
                    LinearProgressIndicator(
                        progress = { (course.progress / 100.0).coerceIn(0.0, 1.0).toFloat() },
                        modifier = Modifier.fillMaxWidth().height(4.dp),
                        color = AccentTeal,
                        trackColor = BrandBlueLight
                    )
                }
            }
            Icon(Icons.Filled.ChevronRight, null, tint = TextSecondary, modifier = Modifier.size(18.dp))
        }
    }
}

@Composable
private fun SourceBadge(text: String, color: Color = BrandBlue) {
    Surface(
        color = color.copy(alpha = 0.1f),
        shape = RoundedCornerShape(4.dp)
    ) {
        Text(
            text,
            color = color,
            fontSize = 10.sp,
            modifier = Modifier.padding(horizontal = 6.dp, vertical = 2.dp),
            maxLines = 1
        )
    }
}
