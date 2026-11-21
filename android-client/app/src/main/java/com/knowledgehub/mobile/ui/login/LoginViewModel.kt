package com.knowledgehub.mobile.ui.login

import androidx.lifecycle.ViewModel
import androidx.lifecycle.viewModelScope
import com.knowledgehub.mobile.data.SessionManager
import com.knowledgehub.mobile.data.api.ApiException
import com.knowledgehub.mobile.data.model.TenantItem
import com.knowledgehub.mobile.data.repository.AuthRepository
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.StateFlow
import kotlinx.coroutines.flow.asStateFlow
import kotlinx.coroutines.launch

data class LoginUiState(
    val tenants: List<TenantItem> = emptyList(),
    val selectedTenant: String = "",       // tenantName，空字符串表示宿主
    val userName: String = "",
    val password: String = "",
    val isLoading: Boolean = false,
    val error: String? = null,
    val loginSuccess: Boolean = false
)

class LoginViewModel(
    private val authRepo: AuthRepository,
    private val session: SessionManager
) : ViewModel() {

    private val _uiState = MutableStateFlow(LoginUiState())
    val uiState: StateFlow<LoginUiState> = _uiState.asStateFlow()

    init {
        loadTenants()
    }

    private fun loadTenants() {
        viewModelScope.launch {
            runCatching { authRepo.getTenants() }
                .onSuccess { list ->
                    // "全局"(id==null) 对应宿主，tenantName 为空；其余用 tenantName
                    val tenantNames = list.mapNotNull { it.tenantName }
                    _uiState.value = _uiState.value.copy(
                        tenants = list,
                        selectedTenant = tenantNames.firstOrNull { it == "qidi" } ?: tenantNames.firstOrNull() ?: ""
                    )
                }
                .onFailure {
                    // 租户列表拉取失败不阻塞登录，允许手工输入
                    _uiState.value = _uiState.value.copy(tenants = emptyList())
                }
        }
    }

    fun onTenantChange(value: String) {
        _uiState.value = _uiState.value.copy(selectedTenant = value, error = null)
    }

    fun onUserNameChange(value: String) {
        _uiState.value = _uiState.value.copy(userName = value, error = null)
    }

    fun onPasswordChange(value: String) {
        _uiState.value = _uiState.value.copy(password = value, error = null)
    }

    fun login() {
        val state = _uiState.value
        if (state.userName.isBlank() || state.password.isBlank()) {
            _uiState.value = state.copy(error = "请输入账号和密码")
            return
        }
        viewModelScope.launch {
            _uiState.value = state.copy(isLoading = true, error = null)
            runCatching {
                val tenant = state.selectedTenant.ifBlank { null }
                val token = authRepo.login(state.userName, state.password, tenant)
                if (token.accessToken.isBlank()) {
                    throw ApiException(token.errorDescription ?: token.error ?: "登录失败")
                }
                session.saveTokens(token.accessToken, token.refreshToken, tenant)
                val config = authRepo.getCurrentUser()
                session.saveUser(config.currentUser.copy(isAuthenticated = true))
            }.onSuccess {
                _uiState.value = _uiState.value.copy(isLoading = false, loginSuccess = true)
            }.onFailure { e ->
                _uiState.value = _uiState.value.copy(
                    isLoading = false,
                    error = when (e) {
                        is ApiException -> e.message
                        else -> e.message ?: "网络错误，请检查后端服务"
                    }
                )
            }
        }
    }
}
