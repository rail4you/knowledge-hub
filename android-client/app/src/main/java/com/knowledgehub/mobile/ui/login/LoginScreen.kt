package com.knowledgehub.mobile.ui.login

import androidx.compose.foundation.background
import androidx.compose.foundation.layout.*
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.foundation.text.KeyboardOptions
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.filled.Visibility
import androidx.compose.material.icons.filled.VisibilityOff
import androidx.compose.material3.*
import androidx.compose.runtime.*
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.text.input.KeyboardType
import androidx.compose.ui.text.input.PasswordVisualTransformation
import androidx.compose.ui.text.input.VisualTransformation
import androidx.compose.ui.unit.dp
import androidx.compose.ui.unit.sp
import com.knowledgehub.mobile.theme.BrandBlue

@OptIn(ExperimentalMaterial3Api::class)
@Composable
fun LoginScreen(viewModel: LoginViewModel) {
    val state by viewModel.uiState.collectAsState()
    var passwordVisible by remember { mutableStateOf(false) }
    var tenantExpanded by remember { mutableStateOf(false) }

    Column(
        modifier = Modifier
            .fillMaxSize()
            .background(MaterialTheme.colorScheme.background)
            .statusBarsPadding()
            .padding(horizontal = 28.dp),
        horizontalAlignment = Alignment.CenterHorizontally
    ) {
        Spacer(Modifier.height(72.dp))

        // Logo 块
        Box(
            modifier = Modifier
                .size(72.dp)
                .background(BrandBlue, RoundedCornerShape(16.dp)),
            contentAlignment = Alignment.Center
        ) {
            Text("易", color = Color.White, fontSize = 34.sp, fontWeight = FontWeight.Bold)
        }

        Spacer(Modifier.height(20.dp))
        Text("易课通", fontSize = 28.sp, fontWeight = FontWeight.Bold, color = MaterialTheme.colorScheme.onBackground)
        Spacer(Modifier.height(6.dp))
        Text("课程学习 · 教师与学生统一入口", fontSize = 14.sp, color = MaterialTheme.colorScheme.onSurfaceVariant)

        Spacer(Modifier.height(40.dp))

        // 租户选择（下拉）
        ExposedDropdownMenuBox(
            expanded = tenantExpanded,
            onExpandedChange = { tenantExpanded = !tenantExpanded }
        ) {
            OutlinedTextField(
                value = state.selectedTenant.ifBlank { "全局（默认）" },
                onValueChange = {},
                readOnly = true,
                label = { Text("学校 / 租户") },
                trailingIcon = { ExposedDropdownMenuDefaults.TrailingIcon(expanded = tenantExpanded) },
                modifier = Modifier
                    .fillMaxWidth()
                    .menuAnchor(),
                shape = RoundedCornerShape(8.dp),
                singleLine = true
            )
            ExposedDropdownMenu(
                expanded = tenantExpanded,
                onDismissRequest = { tenantExpanded = false }
            ) {
                DropdownMenuItem(
                    text = { Text("全局（默认）") },
                    onClick = { viewModel.onTenantChange(""); tenantExpanded = false }
                )
                state.tenants.filter { !it.tenantName.isNullOrBlank() }.forEach { t ->
                    DropdownMenuItem(
                        text = { Text("${t.schoolName ?: t.name}（${t.tenantName}）") },
                        onClick = { viewModel.onTenantChange(t.tenantName!!); tenantExpanded = false }
                    )
                }
            }
        }

        Spacer(Modifier.height(16.dp))

        OutlinedTextField(
            value = state.userName,
            onValueChange = viewModel::onUserNameChange,
            label = { Text("账号") },
            modifier = Modifier.fillMaxWidth(),
            shape = RoundedCornerShape(8.dp),
            singleLine = true,
            keyboardOptions = KeyboardOptions(keyboardType = KeyboardType.Text)
        )

        Spacer(Modifier.height(16.dp))

        OutlinedTextField(
            value = state.password,
            onValueChange = viewModel::onPasswordChange,
            label = { Text("密码") },
            modifier = Modifier.fillMaxWidth(),
            shape = RoundedCornerShape(8.dp),
            singleLine = true,
            visualTransformation = if (passwordVisible) VisualTransformation.None else PasswordVisualTransformation(),
            keyboardOptions = KeyboardOptions(keyboardType = KeyboardType.Password),
            trailingIcon = {
                IconButton(onClick = { passwordVisible = !passwordVisible }) {
                    Icon(
                        if (passwordVisible) Icons.Filled.VisibilityOff else Icons.Filled.Visibility,
                        contentDescription = "显示/隐藏密码"
                    )
                }
            }
        )

        Spacer(Modifier.height(12.dp))

        state.error?.let { err ->
            Text(
                text = err,
                color = MaterialTheme.colorScheme.error,
                fontSize = 13.sp,
                modifier = Modifier.fillMaxWidth()
            )
        }

        Spacer(Modifier.height(20.dp))

        Button(
            onClick = viewModel::login,
            modifier = Modifier
                .fillMaxWidth()
                .height(50.dp),
            shape = RoundedCornerShape(8.dp),
            colors = ButtonDefaults.buttonColors(containerColor = BrandBlue),
            enabled = !state.isLoading
        ) {
            if (state.isLoading) {
                CircularProgressIndicator(modifier = Modifier.size(20.dp), color = Color.White, strokeWidth = 2.dp)
                Spacer(Modifier.width(8.dp))
                Text("登录中…")
            } else {
                Text("登录", fontSize = 16.sp, fontWeight = FontWeight.Medium)
            }
        }

        Spacer(Modifier.height(24.dp))
        Text(
            "登录后根据账号角色自动进入教师端或学生端",
            fontSize = 12.sp,
            color = MaterialTheme.colorScheme.onSurfaceVariant
        )
    }
}
