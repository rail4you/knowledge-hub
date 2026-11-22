package com.knowledgehub.mobile.ui.common

import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.automirrored.filled.Assignment
import androidx.compose.material.icons.filled.AutoAwesome
import androidx.compose.material.icons.filled.Dashboard
import androidx.compose.material.icons.filled.Explore
import androidx.compose.material.icons.filled.Folder
import androidx.compose.material.icons.filled.InsertChart
import androidx.compose.material.icons.filled.Notifications
import androidx.compose.material.icons.filled.School
import androidx.compose.material.icons.filled.Settings
import androidx.compose.material3.Icon
import androidx.compose.material3.NavigationBar
import androidx.compose.material3.NavigationBarItem
import androidx.compose.material3.Text
import androidx.compose.runtime.Composable
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.graphics.vector.ImageVector
import com.knowledgehub.mobile.theme.AccentTeal
import com.knowledgehub.mobile.theme.BrandBlue
import com.knowledgehub.mobile.theme.TextSecondary
import com.knowledgehub.mobile.theme.WarnOrange

/**
 * 应用模块注册表。
 *
 * 新增一个模块时，只需要在这里注册：
 * - 工作台快捷入口从 student 列表自动读取；
 * - 需要出现在底部导航的模块设置 showInBottomBar = true；
 * - 尚未上线的模块设置 enabled = false，工作台会显示“即将开放”且不可跳转。
 */
data class AppModule(
    val id: String,
    val label: String,
    val description: String,
    val icon: ImageVector,
    val tint: Color = BrandBlue,
    val showInBottomBar: Boolean = false,
    val enabled: Boolean = true
)

object AppModules {
    const val WORKBENCH = "workbench"
    const val MY_LEARNING = "my_learning"
    const val COURSE_CENTER = "course_center"
    const val LEARNING_STATS = "learning_stats"
    const val SETTINGS = "settings"

    val student: List<AppModule> = listOf(
        AppModule(
            id = WORKBENCH,
            label = "工作台",
            description = "总览与快捷入口",
            icon = Icons.Filled.Dashboard,
            tint = BrandBlue,
            showInBottomBar = true
        ),
        AppModule(
            id = MY_LEARNING,
            label = "我的学习",
            description = "继续课程与章节学习",
            icon = Icons.Filled.School,
            tint = AccentTeal,
            showInBottomBar = true
        ),
        AppModule(
            id = COURSE_CENTER,
            label = "课程中心",
            description = "本校、共享与公共课程",
            icon = Icons.Filled.Explore,
            tint = BrandBlue,
            showInBottomBar = true
        ),
        AppModule(
            id = LEARNING_STATS,
            label = "学习统计",
            description = "进度、时长与习题记录",
            icon = Icons.Filled.InsertChart,
            tint = AccentTeal,
            showInBottomBar = true
        ),
        AppModule(
            id = SETTINGS,
            label = "设置",
            description = "账户资料与安全",
            icon = Icons.Filled.Settings,
            tint = BrandBlue,
            showInBottomBar = true
        )
    )

    /** 已规划但尚未接入的模块，先在工作台展示占位，后续改为 enabled = true 即可跳转。 */
    val studentUpcoming: List<AppModule> = listOf(
        AppModule(
            id = "assignments",
            label = "作业",
            description = "作业提交与批改",
            icon = Icons.AutoMirrored.Filled.Assignment,
            tint = TextSecondary,
            enabled = false
        ),
        AppModule(
            id = "resource_library",
            label = "资源库",
            description = "资源检索与收藏",
            icon = Icons.Filled.Folder,
            tint = TextSecondary,
            enabled = false
        ),
        AppModule(
            id = "notifications",
            label = "消息通知",
            description = "课程与学习提醒",
            icon = Icons.Filled.Notifications,
            tint = WarnOrange,
            enabled = false
        )
    )

    val studentBottomBar: List<AppModule> = student.filter { it.showInBottomBar && it.enabled }

    // 教师端模块：与学生端共用 WORKBENCH / SETTINGS 语义，业务模块独立注册。
    const val TEACHER_COURSES = "teacher_courses"
    const val TEACHER_CONTENT = "teacher_content"
    const val TEACHER_AI = "teacher_ai"
    const val TEACHER_INSIGHTS = "teacher_insights"

    val teacher: List<AppModule> = listOf(
        AppModule(
            id = WORKBENCH,
            label = "工作台",
            description = "教学总览与快捷入口",
            icon = Icons.Filled.Dashboard,
            tint = BrandBlue,
            showInBottomBar = true
        ),
        AppModule(
            id = TEACHER_COURSES,
            label = "课程",
            description = "课程列表、创建与发布",
            icon = Icons.Filled.School,
            tint = AccentTeal,
            showInBottomBar = true
        ),
        AppModule(
            id = TEACHER_CONTENT,
            label = "内容",
            description = "章节、习题与资源",
            icon = Icons.Filled.Folder,
            tint = BrandBlue,
            showInBottomBar = true
        ),
        AppModule(
            id = TEACHER_AI,
            label = "AI",
            description = "出题、备课与教学助手",
            icon = Icons.Filled.AutoAwesome,
            tint = WarnOrange,
            showInBottomBar = true
        ),
        AppModule(
            id = TEACHER_INSIGHTS,
            label = "学情",
            description = "学生学习情况与完成率",
            icon = Icons.Filled.InsertChart,
            tint = AccentTeal,
            showInBottomBar = true
        )
    )

    /** 教师端后续模块占位，注册后即可在工作台显示。 */
    val teacherUpcoming: List<AppModule> = listOf(
        AppModule(
            id = "teacher_settings",
            label = "账户设置",
            description = "资料与安全设置",
            icon = Icons.Filled.Settings,
            tint = TextSecondary,
            enabled = false
        ),
        AppModule(
            id = "teacher_assignments",
            label = "作业批改",
            description = "作业发布与批改",
            icon = Icons.AutoMirrored.Filled.Assignment,
            tint = TextSecondary,
            enabled = false
        )
    )

    val teacherBottomBar: List<AppModule> = teacher.filter { it.showInBottomBar && it.enabled }

    fun find(id: String): AppModule? =
        (student + studentUpcoming + teacher + teacherUpcoming).firstOrNull { it.id == id }
}

/** 常见安卓底部导航：固定一级模块，切换后保留各自页面状态。 */
@Composable
fun AppBottomNavigation(
    modules: List<AppModule>,
    selectedId: String,
    onSelect: (String) -> Unit
) {
    NavigationBar {
        modules.forEach { module ->
            NavigationBarItem(
                selected = module.id == selectedId,
                onClick = { onSelect(module.id) },
                icon = { module.icon.let { Icon(it, contentDescription = null) } },
                label = { Text(module.label) },
                alwaysShowLabel = true
            )
        }
    }
}
