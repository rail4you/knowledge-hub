package com.knowledgehub.mobile.theme

import androidx.compose.foundation.isSystemInDarkTheme
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.darkColorScheme
import androidx.compose.material3.lightColorScheme
import androidx.compose.runtime.Composable
import androidx.compose.ui.graphics.Color

// 品牌蓝（PRODUCT.md: #1a5fe0 - #1e6ce8）
val BrandBlue = Color(0xFF1A5FE0)
val BrandBlueDark = Color(0xFF1549B0)
val BrandBlueLight = Color(0xFFE8F0FE)
val AccentTeal = Color(0xFF0E9F8A)
val TextPrimary = Color(0xFF1A2233)
val TextSecondary = Color(0xFF5A6478)
val SurfaceBg = Color(0xFFF5F7FB)
val Divider = Color(0xFFE4E8F0)
val SuccessGreen = Color(0xFF0E9F6E)
val WarnOrange = Color(0xFFE8833A)

private val LightColors = lightColorScheme(
    primary = BrandBlue,
    onPrimary = Color.White,
    primaryContainer = BrandBlueLight,
    onPrimaryContainer = BrandBlueDark,
    secondary = AccentTeal,
    onSecondary = Color.White,
    background = SurfaceBg,
    onBackground = TextPrimary,
    surface = Color.White,
    onSurface = TextPrimary,
    surfaceVariant = Color(0xFFEEF1F7),
    onSurfaceVariant = TextSecondary,
    outline = Divider,
    error = Color(0xFFD93A3A)
)

private val DarkColors = darkColorScheme(
    primary = Color(0xFF6E9BFF),
    onPrimary = Color(0xFF0A1633),
    primaryContainer = Color(0xFF1E3A7A),
    onPrimaryContainer = Color.White,
    secondary = Color(0xFF4FD8C2),
    background = Color(0xFF0F1524),
    onBackground = Color(0xFFE8ECF5),
    surface = Color(0xFF1A2233),
    onSurface = Color(0xFFE8ECF5),
    surfaceVariant = Color(0xFF232D42),
    onSurfaceVariant = Color(0xFFAAB4C8),
    outline = Color(0xFF33405A),
    error = Color(0xFFFF6B6B)
)

@Composable
fun KnowledgeHubTheme(
    darkTheme: Boolean = isSystemInDarkTheme(),
    content: @Composable () -> Unit
) {
    MaterialTheme(
        colorScheme = if (darkTheme) DarkColors else LightColors,
        content = content
    )
}
