package com.knowledgehub.mobile.ui.common

import androidx.compose.foundation.background
import androidx.compose.foundation.layout.*
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.filled.ArrowBack
import androidx.compose.material.icons.filled.Logout
import androidx.compose.material3.*
import androidx.compose.runtime.Composable
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.unit.Dp
import androidx.compose.ui.unit.TextUnit
import androidx.compose.ui.unit.dp
import androidx.compose.ui.unit.sp
import com.knowledgehub.mobile.theme.BrandBlue

/** 应用 Logo：蓝底圆角方块 + “易”字。size 可自适应不同场景。 */
@Composable
fun AppLogo(size: Dp = 34.dp, corner: Dp = 9.dp, fontSize: TextUnit = 19.sp) {
    Box(
        modifier = Modifier
            .size(size)
            .background(Color.White, RoundedCornerShape(corner)),
        contentAlignment = Alignment.Center
    ) {
        Text("易", color = BrandBlue, fontSize = fontSize, fontWeight = FontWeight.Bold)
    }
}

/**
 * 品牌顶栏：Logo + 应用名“易课通” + 页面/角色标题。
 * 主界面用（学生端/教师端），带退出按钮。
 */
@OptIn(ExperimentalMaterial3Api::class)
@Composable
fun BrandTopBar(
    sectionTitle: String,
    onLogout: () -> Unit
) {
    TopAppBar(
        title = {
            Row(verticalAlignment = Alignment.CenterVertically) {
                AppLogo()
                Spacer(Modifier.width(10.dp))
                Column {
                    Text(
                        "易课通",
                        color = Color.White,
                        fontSize = 16.sp,
                        fontWeight = FontWeight.Bold,
                        lineHeight = 18.sp
                    )
                    Text(sectionTitle, color = Color.White.copy(alpha = 0.85f), fontSize = 11.sp)
                }
            }
        },
        actions = {
            IconButton(onClick = onLogout) {
                Icon(Icons.Filled.Logout, contentDescription = "退出登录", tint = Color.White)
            }
        },
        colors = TopAppBarDefaults.topAppBarColors(
            containerColor = BrandBlue,
            titleContentColor = Color.White,
            actionIconContentColor = Color.White
        )
    )
}

/**
 * 品牌顶栏（带返回）：用于课程详情等二级页面。
 * 同时展示应用名与当前页面标题，保持全 App 品牌一致。
 */
@OptIn(ExperimentalMaterial3Api::class)
@Composable
fun BrandTopBarWithBack(
    title: String,
    onBack: () -> Unit
) {
    TopAppBar(
        title = {
            Row(verticalAlignment = Alignment.CenterVertically) {
                AppLogo(size = 30.dp, corner = 8.dp, fontSize = 17.sp)
                Spacer(Modifier.width(10.dp))
                Column {
                    Text(
                        "易课通",
                        color = Color.White,
                        fontSize = 13.sp,
                        fontWeight = FontWeight.Bold,
                        lineHeight = 15.sp
                    )
                    Text(
                        title,
                        color = Color.White.copy(alpha = 0.9f),
                        fontSize = 12.sp,
                        maxLines = 1
                    )
                }
            }
        },
        navigationIcon = {
            IconButton(onClick = onBack) {
                Icon(Icons.Filled.ArrowBack, contentDescription = "返回", tint = Color.White)
            }
        },
        colors = TopAppBarDefaults.topAppBarColors(
            containerColor = BrandBlue,
            titleContentColor = Color.White,
            navigationIconContentColor = Color.White
        )
    )
}
