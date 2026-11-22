package com.knowledgehub.mobile.ui.teacher

import androidx.compose.foundation.background
import androidx.compose.foundation.layout.*
import androidx.compose.foundation.text.KeyboardOptions
import androidx.compose.foundation.lazy.LazyColumn
import androidx.compose.foundation.lazy.items
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.material3.*
import androidx.compose.runtime.*
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.text.input.KeyboardType
import androidx.compose.ui.text.style.TextOverflow
import androidx.compose.ui.unit.dp
import androidx.compose.ui.unit.sp
import com.knowledgehub.mobile.data.model.ExerciseDto
import com.knowledgehub.mobile.theme.*

@Composable
fun TeacherAiTab(
    state: TeacherUiState,
    padding: PaddingValues,
    viewModel: TeacherViewModel
) {
    var tab by remember { mutableIntStateOf(0) }

    LaunchedEffect(state.courses) {
        if (state.aiCourseId == null && state.courses.isNotEmpty()) {
            viewModel.setAiCourseId(state.courses.first().id)
        }
    }

    Column(Modifier.fillMaxSize().background(SurfaceBg).padding(padding)) {
        TabRow(selectedTabIndex = tab) {
            Tab(selected = tab == 0, onClick = { tab = 0 }, text = { Text("AI 出题") })
            Tab(selected = tab == 1, onClick = { tab = 1 }, text = { Text("AI 备课") })
            Tab(selected = tab == 2, onClick = { tab = 2 }, text = { Text("教学助手") })
        }

        when (tab) {
            0 -> AiExercisePanel(state, viewModel)
            1 -> AiLessonPlanPanel(state, viewModel)
            2 -> AiChatPanel(state, viewModel)
        }
    }
}

@Composable
private fun AiExercisePanel(state: TeacherUiState, viewModel: TeacherViewModel) {
    var type by remember { mutableIntStateOf(0) }
    var count by remember { mutableStateOf("5") }
    var difficulty by remember { mutableStateOf("2") }
    var topic by remember { mutableStateOf("") }
    var prompt by remember { mutableStateOf("") }

    LazyColumn(
        Modifier.fillMaxSize(),
        contentPadding = PaddingValues(16.dp),
        verticalArrangement = Arrangement.spacedBy(10.dp)
    ) {
        item {
            Text("选择课程", fontWeight = FontWeight.Bold, fontSize = 15.sp)
            Spacer(Modifier.height(6.dp))
            Row(Modifier.fillMaxWidth(), horizontalArrangement = Arrangement.spacedBy(8.dp)) {
                state.courses.take(3).forEach { course ->
                    FilterChip(
                        selected = course.id == state.aiCourseId,
                        onClick = { viewModel.setAiCourseId(course.id) },
                        label = { Text(course.title, maxLines = 1, overflow = TextOverflow.Ellipsis) }
                    )
                }
            }
            if (state.courses.isEmpty()) Text("暂无课程，请先创建课程", fontSize = 12.sp, color = TextSecondary)
        }

        item {
            Text("题型", fontSize = 12.sp, color = TextSecondary)
            Row {
                listOf("单选", "多选", "判断", "填空", "简答").forEachIndexed { index, label ->
                    FilterChip(
                        selected = type == index,
                        onClick = { type = index },
                        label = { Text(label, fontSize = 11.sp) },
                        modifier = Modifier.padding(end = 4.dp)
                    )
                }
            }
        }

        item {
            Row(horizontalArrangement = Arrangement.spacedBy(8.dp)) {
                OutlinedTextField(
                    value = count, onValueChange = { count = it }, label = { Text("数量") }, singleLine = true,
                    keyboardOptions = KeyboardOptions(keyboardType = KeyboardType.Number),
                    modifier = Modifier.weight(1f)
                )
                OutlinedTextField(
                    value = difficulty, onValueChange = { difficulty = it }, label = { Text("难度 1-5") }, singleLine = true,
                    keyboardOptions = KeyboardOptions(keyboardType = KeyboardType.Number),
                    modifier = Modifier.weight(1f)
                )
            }
        }

        item {
            OutlinedTextField(value = topic, onValueChange = { topic = it }, label = { Text("主题提示（可空）") }, singleLine = true, modifier = Modifier.fillMaxWidth())
        }
        item {
            OutlinedTextField(value = prompt, onValueChange = { prompt = it }, label = { Text("自定义要求（可空）") }, maxLines = 3, modifier = Modifier.fillMaxWidth())
        }

        item {
            PrimaryAction(
                text = if (state.aiBusy) "生成中…" else "生成习题",
                enabled = !state.aiBusy && state.aiCourseId != null
            ) {
                viewModel.generateExercises(
                    count.toIntOrNull()?.coerceIn(1, 20) ?: 5,
                    type,
                    difficulty.toIntOrNull()?.coerceIn(1, 5) ?: 2,
                    topic,
                    prompt
                )
            }
        }

        if (state.aiGeneratedExercises.isNotEmpty()) {
            item { Text("生成结果（${state.aiGeneratedExercises.size} 道）", fontWeight = FontWeight.Bold, fontSize = 15.sp) }
            items(state.aiGeneratedExercises) { exercise -> GeneratedExerciseCard(exercise) }
        }
    }
}

@Composable
private fun AiLessonPlanPanel(state: TeacherUiState, viewModel: TeacherViewModel) {
    var topic by remember { mutableStateOf("") }
    var subject by remember { mutableStateOf("") }
    var grade by remember { mutableStateOf("") }
    var duration by remember { mutableStateOf("45") }
    var prompt by remember { mutableStateOf("") }

    LazyColumn(
        Modifier.fillMaxSize(),
        contentPadding = PaddingValues(16.dp),
        verticalArrangement = Arrangement.spacedBy(10.dp)
    ) {
        item { OutlinedTextField(value = topic, onValueChange = { topic = it }, label = { Text("备课主题 *") }, singleLine = true, modifier = Modifier.fillMaxWidth()) }
        item {
            Row(horizontalArrangement = Arrangement.spacedBy(8.dp)) {
                OutlinedTextField(value = subject, onValueChange = { subject = it }, label = { Text("学科") }, singleLine = true, modifier = Modifier.weight(1f))
                OutlinedTextField(value = grade, onValueChange = { grade = it }, label = { Text("年级") }, singleLine = true, modifier = Modifier.weight(1f))
                OutlinedTextField(
                    value = duration, onValueChange = { duration = it }, label = { Text("分钟") }, singleLine = true,
                    keyboardOptions = KeyboardOptions(keyboardType = KeyboardType.Number),
                    modifier = Modifier.weight(0.8f)
                )
            }
        }
        item { OutlinedTextField(value = prompt, onValueChange = { prompt = it }, label = { Text("教学要求（可空）") }, maxLines = 3, modifier = Modifier.fillMaxWidth()) }
        item {
            PrimaryAction(text = if (state.aiBusy) "生成中…" else "生成教案", enabled = !state.aiBusy) {
                viewModel.generateLessonPlan(topic, subject, grade, duration.toIntOrNull()?.coerceIn(10, 180) ?: 45, prompt)
            }
        }
        if (state.aiText.isNotBlank()) {
            item {
                Card(
                    modifier = Modifier.fillMaxWidth(),
                    colors = CardDefaults.cardColors(containerColor = Color.White),
                    shape = RoundedCornerShape(12.dp)
                ) {
                    Text(state.aiText, fontSize = 13.sp, color = TextPrimary, modifier = Modifier.padding(14.dp))
                }
            }
        }
    }
}

@Composable
private fun AiChatPanel(state: TeacherUiState, viewModel: TeacherViewModel) {
    var message by remember { mutableStateOf("") }
    LazyColumn(
        Modifier.fillMaxSize(),
        contentPadding = PaddingValues(16.dp),
        verticalArrangement = Arrangement.spacedBy(10.dp)
    ) {
        item {
            OutlinedTextField(value = message, onValueChange = { message = it }, label = { Text("向 AI 助手提问") }, maxLines = 4, modifier = Modifier.fillMaxWidth())
        }
        item {
            PrimaryAction(text = if (state.aiBusy) "思考中…" else "发送", enabled = !state.aiBusy && message.isNotBlank()) {
                viewModel.aiChat(message)
            }
        }
        if (state.aiText.isNotBlank()) {
            item {
                Card(
                    modifier = Modifier.fillMaxWidth(),
                    colors = CardDefaults.cardColors(containerColor = Color.White),
                    shape = RoundedCornerShape(12.dp)
                ) {
                    Text(state.aiText, fontSize = 13.sp, color = TextPrimary, modifier = Modifier.padding(14.dp))
                }
            }
        }
    }
}

@Composable
private fun PrimaryAction(text: String, enabled: Boolean, onClick: () -> Unit) {
    Button(
        onClick = onClick,
        enabled = enabled,
        modifier = Modifier.fillMaxWidth().height(46.dp),
        shape = RoundedCornerShape(10.dp),
        colors = ButtonDefaults.buttonColors(containerColor = BrandBlue)
    ) {
        if (text.contains("中…")) {
            CircularProgressIndicator(modifier = Modifier.size(18.dp), strokeWidth = 2.dp, color = Color.White)
            Spacer(Modifier.width(8.dp))
        }
        Text(text)
    }
}

@Composable
private fun GeneratedExerciseCard(exercise: ExerciseDto) {
    Card(
        modifier = Modifier.fillMaxWidth(),
        colors = CardDefaults.cardColors(containerColor = Color.White),
        shape = RoundedCornerShape(10.dp)
    ) {
        Column(Modifier.padding(12.dp)) {
            Text(exercise.title.ifBlank { "AI 生成习题" }, fontWeight = FontWeight.Medium, fontSize = 14.sp)
            Spacer(Modifier.height(4.dp))
            Text(exercise.questionContent, fontSize = 13.sp)
            Spacer(Modifier.height(6.dp))
            Text("答案：${exercise.answer ?: "-"} · 难度 ${exercise.difficulty}", fontSize = 11.sp, color = AccentTeal)
        }
    }
}
