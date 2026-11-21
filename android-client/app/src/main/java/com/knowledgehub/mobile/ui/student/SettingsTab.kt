package com.knowledgehub.mobile.ui.student

import androidx.compose.foundation.background
import androidx.compose.foundation.layout.*
import androidx.compose.foundation.lazy.LazyColumn
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.filled.*
import androidx.compose.material3.*
import androidx.compose.runtime.*
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.text.input.PasswordVisualTransformation
import androidx.compose.ui.unit.dp
import androidx.compose.ui.unit.sp
import com.knowledgehub.mobile.theme.*

@OptIn(ExperimentalMaterial3Api::class)
@Composable
fun SettingsTab(
    state: StudentUiState,
    padding: PaddingValues,
    viewModel: StudentViewModel,
    onLogout: () -> Unit
) {
    var name by remember { mutableStateOf("") }
    var email by remember { mutableStateOf("") }
    var phone by remember { mutableStateOf("") }
    var currentPassword by remember { mutableStateOf("") }
    var newPassword by remember { mutableStateOf("") }
    var confirmPassword by remember { mutableStateOf("") }

    LaunchedEffect(state.profile?.id) {
        name = state.profile?.name.orEmpty()
        email = state.profile?.email.orEmpty()
        phone = state.profile?.phoneNumber.orEmpty()
    }

    LazyColumn(
        modifier = Modifier
            .fillMaxSize()
            .background(SurfaceBg)
            .padding(padding),
        contentPadding = PaddingValues(16.dp),
        verticalArrangement = Arrangement.spacedBy(12.dp)
    ) {
        item {
            Text("设置", fontWeight = FontWeight.Bold, fontSize = 18.sp)
        }

        item {
            Card(
                modifier = Modifier.fillMaxWidth(),
                shape = RoundedCornerShape(14.dp),
                colors = CardDefaults.cardColors(containerColor = Color.White)
            ) {
                Column(Modifier.padding(16.dp)) {
                    Row(verticalAlignment = Alignment.CenterVertically) {
                        Box(
                            Modifier.size(48.dp).background(BrandBlueLight, RoundedCornerShape(24.dp)),
                            Alignment.Center
                        ) {
                            Icon(Icons.Filled.Person, null, tint = BrandBlue, modifier = Modifier.size(26.dp))
                        }
                        Spacer(Modifier.width(12.dp))
                        Column {
                            Text(
                                state.profile?.name?.takeIf { it.isNotBlank() }
                                    ?: viewModel.session.currentUser?.name
                                    ?: "学生用户",
                                fontWeight = FontWeight.Bold,
                                fontSize = 16.sp
                            )
                            Text(
                                state.profile?.userName
                                    ?: viewModel.session.currentUser?.userName
                                    ?: "—",
                                fontSize = 12.sp,
                                color = TextSecondary
                            )
                        }
                    }
                    Spacer(Modifier.height(14.dp))
                    HorizontalDivider(color = Divider)
                    Spacer(Modifier.height(12.dp))
                    if (state.profileLoading && state.profile == null) {
                        LinearProgressIndicator(Modifier.fillMaxWidth())
                    } else {
                        ProfileRow("登录账号", state.profile?.userName ?: "—")
                        ProfileRow("显示姓名", state.profile?.name?.takeIf { it.isNotBlank() } ?: "—")
                        ProfileRow("实名", state.profile?.realName?.takeIf { it.isNotBlank() } ?: "未认证")
                        ProfileRow("邮箱", state.profile?.email?.takeIf { it.isNotBlank() } ?: "未绑定")
                        ProfileRow("手机号", state.profile?.phoneNumber?.takeIf { it.isNotBlank() } ?: "未绑定")
                        ProfileRow("证件号", state.profile?.idCardMasked?.takeIf { it.isNotBlank() } ?: "—")
                        Spacer(Modifier.height(6.dp))
                        Row(horizontalArrangement = Arrangement.spacedBy(8.dp)) {
                            StatusChip("手机${if (state.profile?.isPhoneVerified == true) "已验证" else "未验证"}", state.profile?.isPhoneVerified == true)
                            StatusChip("实名${if (state.profile?.isRealNameVerified == true) "已认证" else "未认证"}", state.profile?.isRealNameVerified == true)
                        }
                    }
                }
            }
        }

        item {
            SectionCard(title = "编辑资料") {
                OutlinedTextField(
                    value = name,
                    onValueChange = { name = it },
                    label = { Text("姓名") },
                    modifier = Modifier.fillMaxWidth(),
                    singleLine = true
                )
                Spacer(Modifier.height(10.dp))
                OutlinedTextField(
                    value = email,
                    onValueChange = { email = it },
                    label = { Text("邮箱（留空表示不修改）") },
                    modifier = Modifier.fillMaxWidth(),
                    singleLine = true
                )
                Spacer(Modifier.height(10.dp))
                OutlinedTextField(
                    value = phone,
                    onValueChange = { phone = it },
                    label = { Text("手机号（留空表示解绑）") },
                    modifier = Modifier.fillMaxWidth(),
                    singleLine = true
                )
                Spacer(Modifier.height(12.dp))
                Button(
                    onClick = { viewModel.updateProfile(name, email, phone) },
                    modifier = Modifier.fillMaxWidth().height(44.dp),
                    enabled = !state.profileSaving,
                    colors = ButtonDefaults.buttonColors(containerColor = BrandBlue),
                    shape = RoundedCornerShape(8.dp)
                ) {
                    Text("保存资料")
                }
            }
        }

        item {
            SectionCard(title = "修改密码") {
                OutlinedTextField(
                    value = currentPassword,
                    onValueChange = { currentPassword = it },
                    label = { Text("当前密码") },
                    modifier = Modifier.fillMaxWidth(),
                    singleLine = true,
                    visualTransformation = PasswordVisualTransformation()
                )
                Spacer(Modifier.height(10.dp))
                OutlinedTextField(
                    value = newPassword,
                    onValueChange = { newPassword = it },
                    label = { Text("新密码（至少 6 位）") },
                    modifier = Modifier.fillMaxWidth(),
                    singleLine = true,
                    visualTransformation = PasswordVisualTransformation()
                )
                Spacer(Modifier.height(10.dp))
                OutlinedTextField(
                    value = confirmPassword,
                    onValueChange = { confirmPassword = it },
                    label = { Text("确认新密码") },
                    modifier = Modifier.fillMaxWidth(),
                    singleLine = true,
                    visualTransformation = PasswordVisualTransformation()
                )
                Spacer(Modifier.height(12.dp))
                Button(
                    onClick = {
                        viewModel.changePassword(currentPassword, newPassword, confirmPassword)
                        currentPassword = ""
                        newPassword = ""
                        confirmPassword = ""
                    },
                    modifier = Modifier.fillMaxWidth().height(44.dp),
                    enabled = !state.profileSaving,
                    colors = ButtonDefaults.buttonColors(containerColor = BrandBlue),
                    shape = RoundedCornerShape(8.dp)
                ) {
                    Text("修改密码")
                }
            }
        }

        item {
            OutlinedButton(
                onClick = onLogout,
                modifier = Modifier.fillMaxWidth().height(44.dp),
                shape = RoundedCornerShape(8.dp),
                colors = ButtonDefaults.outlinedButtonColors(contentColor = Color(0xFFD93A3A))
            ) {
                Icon(Icons.Filled.Logout, null, modifier = Modifier.size(18.dp))
                Spacer(Modifier.width(8.dp))
                Text("退出登录")
            }
        }
    }
}

@Composable
private fun SectionCard(title: String, content: @Composable ColumnScope.() -> Unit) {
    Card(
        modifier = Modifier.fillMaxWidth(),
        shape = RoundedCornerShape(14.dp),
        colors = CardDefaults.cardColors(containerColor = Color.White)
    ) {
        Column(Modifier.padding(16.dp)) {
            Text(title, fontWeight = FontWeight.Bold, fontSize = 15.sp)
            Spacer(Modifier.height(14.dp))
            content()
        }
    }
}

@Composable
private fun ProfileRow(label: String, value: String) {
    Row(
        Modifier.fillMaxWidth().padding(vertical = 5.dp),
        horizontalArrangement = Arrangement.SpaceBetween
    ) {
        Text(label, fontSize = 12.sp, color = TextSecondary)
        Text(value, fontSize = 12.sp, color = TextPrimary, maxLines = 1)
    }
}

@Composable
private fun StatusChip(text: String, positive: Boolean) {
    Surface(
        color = if (positive) SuccessGreen.copy(alpha = 0.12f) else WarnOrange.copy(alpha = 0.12f),
        shape = RoundedCornerShape(6.dp)
    ) {
        Text(
            text,
            fontSize = 10.sp,
            color = if (positive) SuccessGreen else WarnOrange,
            modifier = Modifier.padding(horizontal = 8.dp, vertical = 4.dp)
        )
    }
}
