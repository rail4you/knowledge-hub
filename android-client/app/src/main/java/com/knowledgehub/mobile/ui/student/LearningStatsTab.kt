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
import androidx.compose.runtime.Composable
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.text.style.TextOverflow
import androidx.compose.ui.unit.dp
import androidx.compose.ui.unit.sp
import com.knowledgehub.mobile.data.model.ExerciseRecordDto
import com.knowledgehub.mobile.data.model.LearningDashboard
import com.knowledgehub.mobile.theme.*

@Composable
fun LearningStatsTab(
    state: StudentUiState,
    padding: PaddingValues,
    viewModel: StudentViewModel,
    onOpenDetail: (String) -> Unit,
    onContinueLearn: (String) -> Unit
) {
    if (state.statsLoading && state.dashboard == null) {
        Box(
            Modifier.fillMaxSize().padding(padding),
            Alignment.Center
        ) { CircularProgressIndicator() }
        return
    }

    LazyColumn(
        modifier = Modifier
            .fillMaxSize()
            .background(SurfaceBg)
            .padding(padding),
        contentPadding = PaddingValues(16.dp),
        verticalArrangement = Arrangement.spacedBy(12.dp)
    ) {
        state.statsError?.let { error ->
            item {
                Card(
                    modifier = Modifier.fillMaxWidth(),
                    colors = CardDefaults.cardColors(containerColor = Color.White)
                ) {
                    Text(error, color = MaterialTheme.colorScheme.error, fontSize = 13.sp, modifier = Modifier.padding(16.dp))
                    TextButton(onClick = { viewModel.loadStats() }) { Text("重试") }
                }
            }
        }

        state.dashboard?.let { dash ->
            item { StatsSummary(dash) }
            item { ActivityCard(dash) }
            item { MasteryCard(dash) }
            item {
                Text("最近学习", fontWeight = FontWeight.Bold, fontSize = 16.sp)
            }
            if (dash.recentLearning.isEmpty()) {
                item { EmptyStatsCard("暂无最近学习记录") }
            } else {
                items(dash.recentLearning, key = { it.courseId }) { recent ->
                    Card(
                        modifier = Modifier.fillMaxWidth().clickable { onOpenDetail(recent.courseId) },
                        shape = RoundedCornerShape(10.dp),
                        colors = CardDefaults.cardColors(containerColor = Color.White)
                    ) {
                        Row(Modifier.padding(12.dp), verticalAlignment = Alignment.CenterVertically) {
                            Icon(Icons.Filled.History, null, tint = BrandBlue, modifier = Modifier.size(20.dp))
                            Spacer(Modifier.width(10.dp))
                            Column(Modifier.weight(1f)) {
                                Text(recent.courseName, fontSize = 13.sp, fontWeight = FontWeight.Medium, maxLines = 1, overflow = TextOverflow.Ellipsis)
                                Text(
                                    "进度 ${recent.progress.toInt()}% · ${formatTime(recent.lastAccessAt)}",
                                    fontSize = 11.sp,
                                    color = TextSecondary
                                )
                            }
                            Text("${recent.progress.toInt()}%", color = BrandBlue, fontSize = 13.sp, fontWeight = FontWeight.Bold)
                        }
                    }
                }
            }
        }

        item {
            Row(
                Modifier.fillMaxWidth(),
                horizontalArrangement = Arrangement.SpaceBetween,
                verticalAlignment = Alignment.CenterVertically
            ) {
                Text("进行中的课程", fontWeight = FontWeight.Bold, fontSize = 16.sp)
                Text("${state.myCourses.size} 门在学", fontSize = 12.sp, color = TextSecondary)
            }
        }
        val inProgress = state.myCourses.filter { it.progress < 100 }
        if (inProgress.isEmpty()) {
            item { EmptyStatsCard("暂无进行中的课程") }
        } else {
            items(inProgress, key = { it.courseId.ifBlank { it.id } }) { course ->
                Card(
                    modifier = Modifier.fillMaxWidth().clickable { onContinueLearn(course.courseId) },
                    shape = RoundedCornerShape(10.dp),
                    colors = CardDefaults.cardColors(containerColor = Color.White)
                ) {
                    Column(Modifier.padding(12.dp)) {
                        Text(course.courseTitle, fontSize = 13.sp, fontWeight = FontWeight.Medium, maxLines = 1, overflow = TextOverflow.Ellipsis)
                        Spacer(Modifier.height(6.dp))
                        LinearProgressIndicator(
                            progress = { (course.progress / 100.0).coerceIn(0.0, 1.0).toFloat() },
                            modifier = Modifier.fillMaxWidth().height(5.dp),
                            color = AccentTeal,
                            trackColor = BrandBlueLight
                        )
                        Spacer(Modifier.height(4.dp))
                        Text(
                            "${course.progress.toInt()}% · 习题 ${course.completedExerciseCount}/${course.totalExerciseCount}",
                            fontSize = 11.sp,
                            color = TextSecondary
                        )
                    }
                }
            }
        }

        item {
            Row(
                Modifier.fillMaxWidth(),
                horizontalArrangement = Arrangement.SpaceBetween,
                verticalAlignment = Alignment.CenterVertically
            ) {
                Text("最近习题记录", fontWeight = FontWeight.Bold, fontSize = 16.sp)
                Text("${state.recentRecords.size} 条", fontSize = 12.sp, color = TextSecondary)
            }
        }
        item {
            Row(horizontalArrangement = Arrangement.spacedBy(8.dp)) {
                RecordFilter.entries.forEach { filter ->
                    FilterChip(
                        selected = state.recordFilter == filter,
                        onClick = { viewModel.setRecordFilter(filter) },
                        label = { Text(filter.label, fontSize = 12.sp) }
                    )
                }
            }
        }
        if (state.recentRecordsLoading) {
            item { Box(Modifier.fillMaxWidth().height(80.dp), Alignment.Center) { CircularProgressIndicator() } }
        } else if (state.recentRecords.isEmpty()) {
            item { EmptyStatsCard("暂无习题提交记录") }
        } else {
            items(state.recentRecords, key = { it.id }) { record ->
                RecentRecordRow(record)
            }
        }
    }
}

@Composable
private fun StatsSummary(dash: LearningDashboard) {
    Card(
        modifier = Modifier.fillMaxWidth(),
        shape = RoundedCornerShape(14.dp),
        colors = CardDefaults.cardColors(containerColor = Color.White)
    ) {
        Column(Modifier.padding(16.dp)) {
            Text("学习概览", fontWeight = FontWeight.Bold, fontSize = 15.sp)
            Spacer(Modifier.height(14.dp))
            Row(Modifier.fillMaxWidth(), horizontalArrangement = Arrangement.SpaceEvenly) {
                StatCell("${dash.totalCourses}", "在学课程")
                StatCell("${dash.completedCourses}", "已完成")
                StatCell("${dash.totalExerciseRecords}", "习题练习")
                StatCell("${dash.totalResourceActivities}", "资源学习")
            }
            Spacer(Modifier.height(14.dp))
            HorizontalDivider(color = Divider)
            Spacer(Modifier.height(12.dp))
            Row(Modifier.fillMaxWidth(), horizontalArrangement = Arrangement.SpaceBetween) {
                Text("平均进度", fontSize = 12.sp, color = TextSecondary)
                Text("${dash.averageProgress.toInt()}%", fontSize = 14.sp, fontWeight = FontWeight.Bold, color = BrandBlue)
            }
            Spacer(Modifier.height(6.dp))
            LinearProgressIndicator(
                progress = { (dash.averageProgress / 100.0).coerceIn(0.0, 1.0).toFloat() },
                modifier = Modifier.fillMaxWidth().height(7.dp),
                color = BrandBlue,
                trackColor = BrandBlueLight
            )
            Spacer(Modifier.height(8.dp))
            Text(
                "累计学习时长 ${String.format("%.0f", dash.totalLearningTime)} 分钟 · 已学 ${dash.inProgressCourses} 门 · 未开始 ${dash.notStartedCourses} 门",
                fontSize = 11.sp,
                color = TextSecondary
            )
        }
    }
}

@Composable
private fun StatCell(value: String, label: String) {
    Column(horizontalAlignment = Alignment.CenterHorizontally) {
        Text(value, fontWeight = FontWeight.Bold, fontSize = 20.sp, color = BrandBlue)
        Text(label, fontSize = 11.sp, color = TextSecondary)
    }
}

@Composable
private fun ActivityCard(dash: LearningDashboard) {
    Card(
        modifier = Modifier.fillMaxWidth(),
        shape = RoundedCornerShape(14.dp),
        colors = CardDefaults.cardColors(containerColor = Color.White)
    ) {
        Column(Modifier.padding(16.dp)) {
            Text("最近 7 天学习活动", fontWeight = FontWeight.Bold, fontSize = 15.sp)
            Spacer(Modifier.height(4.dp))
            Text("习题提交与资源预览次数", fontSize = 11.sp, color = TextSecondary)
            Spacer(Modifier.height(16.dp))
            val values = dash.dailyTimeValues
            val labels = dash.dailyTimeLabels
            val max = values.maxOrNull()?.takeIf { it > 0 } ?: 1.0
            Row(
                Modifier.fillMaxWidth().height(130.dp),
                horizontalArrangement = Arrangement.SpaceEvenly,
                verticalAlignment = Alignment.Bottom
            ) {
                values.forEachIndexed { index, value ->
                    val label = labels.getOrNull(index).orEmpty()
                    Column(
                        horizontalAlignment = Alignment.CenterHorizontally,
                        verticalArrangement = Arrangement.Bottom,
                        modifier = Modifier.height(130.dp)
                    ) {
                        Text(value.toInt().toString(), fontSize = 10.sp, color = TextSecondary)
                        Box(
                            Modifier
                                .height(((value / max) * 80).coerceAtLeast(2.0).dp)
                                .width(18.dp)
                                .background(BrandBlue, RoundedCornerShape(topStart = 4.dp, topEnd = 4.dp))
                        )
                        Spacer(Modifier.height(4.dp))
                        Text(label, fontSize = 10.sp, color = TextSecondary)
                    }
                }
            }
        }
    }
}

@Composable
private fun MasteryCard(dash: LearningDashboard) {
    if (dash.knowledgeDimensions.isEmpty()) return
    Card(
        modifier = Modifier.fillMaxWidth(),
        shape = RoundedCornerShape(14.dp),
        colors = CardDefaults.cardColors(containerColor = Color.White)
    ) {
        Column(Modifier.padding(16.dp)) {
            Text("知识点掌握度", fontWeight = FontWeight.Bold, fontSize = 15.sp)
            Spacer(Modifier.height(12.dp))
            dash.knowledgeDimensions.forEachIndexed { index, dimension ->
                val value = dash.masteryValues.getOrNull(index) ?: 0.0
                val max = dimension.maxValue.takeIf { it > 0 }?.toDouble() ?: 100.0
                Text(dimension.name, fontSize = 12.sp, color = TextPrimary)
                Spacer(Modifier.height(4.dp))
                LinearProgressIndicator(
                    progress = { (value / max).coerceIn(0.0, 1.0).toFloat() },
                    modifier = Modifier.fillMaxWidth().height(7.dp),
                    color = if (value >= max * 0.6) AccentTeal else BrandBlue,
                    trackColor = BrandBlueLight
                )
                Spacer(Modifier.height(10.dp))
            }
        }
    }
}

@Composable
private fun RecentRecordRow(record: ExerciseRecordDto) {
    Card(
        modifier = Modifier.fillMaxWidth(),
        shape = RoundedCornerShape(10.dp),
        colors = CardDefaults.cardColors(containerColor = Color.White)
    ) {
        Row(Modifier.padding(12.dp), verticalAlignment = Alignment.CenterVertically) {
            Icon(
                imageVector = if (record.isCorrect == true) Icons.Filled.CheckCircle else Icons.Filled.Cancel,
                contentDescription = null,
                tint = if (record.isCorrect == true) SuccessGreen else Color(0xFFD93A3A),
                modifier = Modifier.size(20.dp)
            )
            Spacer(Modifier.width(10.dp))
            Column(Modifier.weight(1f)) {
                Text(
                    record.exerciseTitle?.takeIf { it.isNotBlank() } ?: "习题练习",
                    fontSize = 13.sp,
                    fontWeight = FontWeight.Medium,
                    maxLines = 1,
                    overflow = TextOverflow.Ellipsis
                )
                Text(
                    listOfNotNull(
                        record.courseName,
                        record.chapterName,
                        formatTime(record.completedAt)
                    ).joinToString(" · "),
                    fontSize = 11.sp,
                    color = TextSecondary,
                    maxLines = 1,
                    overflow = TextOverflow.Ellipsis
                )
            }
            Text(
                if (record.isCorrect == true) "正确" else if (record.isCorrect == false) "错误" else "未批阅",
                fontSize = 11.sp,
                color = if (record.isCorrect == true) SuccessGreen else TextSecondary
            )
        }
    }
}

@Composable
private fun EmptyStatsCard(message: String) {
    Card(
        modifier = Modifier.fillMaxWidth(),
        shape = RoundedCornerShape(10.dp),
        colors = CardDefaults.cardColors(containerColor = Color.White)
    ) {
        Column(
            Modifier.fillMaxWidth().padding(24.dp),
            horizontalAlignment = Alignment.CenterHorizontally
        ) {
            Icon(Icons.Filled.Inbox, null, tint = TextSecondary, modifier = Modifier.size(28.dp))
            Spacer(Modifier.height(8.dp))
            Text(message, fontSize = 12.sp, color = TextSecondary)
        }
    }
}

private fun formatTime(value: String?): String {
    if (value.isNullOrBlank()) return ""
    return value.replace("T", " ").take(16)
}
