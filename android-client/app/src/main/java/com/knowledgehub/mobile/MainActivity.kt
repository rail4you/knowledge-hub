package com.knowledgehub.mobile

import android.os.Bundle
import androidx.activity.ComponentActivity
import androidx.activity.compose.setContent
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.Surface
import androidx.compose.runtime.*
import androidx.compose.ui.Modifier
import androidx.lifecycle.lifecycleScope
import com.knowledgehub.mobile.data.ApiInitializer
import com.knowledgehub.mobile.data.SessionManager
import com.knowledgehub.mobile.data.repository.AuthRepository
import com.knowledgehub.mobile.data.repository.CourseRepository
import com.knowledgehub.mobile.theme.KnowledgeHubTheme
import com.knowledgehub.mobile.ui.login.LoginScreen
import com.knowledgehub.mobile.ui.login.LoginViewModel
import com.knowledgehub.mobile.ui.student.StudentScreen
import com.knowledgehub.mobile.ui.student.StudentViewModel
import com.knowledgehub.mobile.ui.teacher.TeacherScreen
import com.knowledgehub.mobile.ui.teacher.TeacherViewModel
import kotlinx.coroutines.launch

class MainActivity : ComponentActivity() {

    private lateinit var session: SessionManager

    override fun onCreate(savedInstanceState: Bundle?) {
        super.onCreate(savedInstanceState)

        session = SessionManager(applicationContext)
        ApiInitializer.init(session)

        // 先在后台把会话从磁盘恢复到内存，再渲染 UI，避免竞态。
        lifecycleScope.launch {
            session.restore()
            setContent {
                KnowledgeHubTheme {
                    Surface(
                        modifier = Modifier.fillMaxSize(),
                        color = MaterialTheme.colorScheme.background
                    ) {
                        AppNav(session = session, onLogout = {
                            lifecycleScope.launch { session.clear() }
                        })
                    }
                }
            }
        }
    }
}

@Composable
private fun AppNav(session: SessionManager, onLogout: () -> Unit) {
    var isLoggedIn by remember { mutableStateOf(!session.accessToken.isNullOrBlank()) }
    var roleVersion by remember { mutableIntStateOf(0) }

    // token 失效（如后端重启）时自动回到登录页
    LaunchedEffect(Unit) {
        com.knowledgehub.mobile.data.AuthEvents.unauthorized.collect {
            onLogout()
            isLoggedIn = false
        }
    }

    if (!isLoggedIn) {
        val loginVm = remember { LoginViewModel(AuthRepository(), session) }
        val state by loginVm.uiState.collectAsState()
        LaunchedEffect(state.loginSuccess) {
            if (state.loginSuccess) isLoggedIn = true
        }
        LoginScreen(loginVm)
        return
    }

    // 角色分流：与 Web 端 admin-roles.ts 口径一致
    if (session.isTeacher()) {
        val teacherVm = remember(roleVersion) { TeacherViewModel(CourseRepository(), session) }
        TeacherScreen(teacherVm, onLogout = {
            isLoggedIn = false
            onLogout()
        })
    } else {
        val studentVm = remember(roleVersion) { StudentViewModel(CourseRepository(), session) }
        StudentScreen(studentVm, onLogout = {
            isLoggedIn = false
            onLogout()
        })
    }
}
