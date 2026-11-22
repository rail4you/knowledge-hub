plugins {
    id("com.android.application")
    id("org.jetbrains.kotlin.android")
    id("org.jetbrains.kotlin.plugin.serialization") version "1.9.24"
}

android {
    namespace = "com.knowledgehub.mobile"
    compileSdk = 34

    defaultConfig {
        applicationId = "com.knowledgehub.mobile"
        minSdk = 26
        targetSdk = 34
        versionCode = 1
        versionName = "0.1.0-mvp"

        buildConfigField("String", "CLIENT_ID", "\"KnowledgeHub_App\"")
        buildConfigField("String", "API_SCOPE", "\"offline_access KnowledgeHub\"")
    }

    // local  = 模拟器/真机联调本机开发后端（自签证书，按指纹固定）
    // iketong = 正式域名，使用系统 CA，不信任自签证书
    flavorDimensions += "environment"
    productFlavors {
        create("local") {
            dimension = "environment"
            versionNameSuffix = "-local"
            buildConfigField("String", "API_BASE_URL", "\"https://10.0.2.2:44305\"")
            buildConfigField("boolean", "USE_PINNED_DEV_CERT", "true")
            // 本机 ASP.NET Core 开发证书 SHA-256 指纹（非密钥，可入库）。
            // 查看方式：openssl s_client -connect 127.0.0.1:44305 -servername localhost </dev/null \
            //   | openssl x509 -noout -fingerprint -sha256
            // 重新生成 dotnet dev-certs 后需同步更新此值。
            buildConfigField(
                "String",
                "DEV_CERT_SHA256",
                "\"4b8e4c0c8791a164385043366fbdeec5a7f762333daac096b3c210f4928d68ee\""
            )
            buildConfigField(
                "String",
                "ALLOWED_DEV_HOSTS",
                "\"10.0.2.2,localhost,127.0.0.1\""
            )
        }
        create("iketong") {
            dimension = "environment"
            // 与本地版可同时安装，互不覆盖登录会话
            applicationIdSuffix = ".iketong"
            versionNameSuffix = "-iketong"
            buildConfigField("String", "API_BASE_URL", "\"https://www.iketong.com\"")
            buildConfigField("boolean", "USE_PINNED_DEV_CERT", "false")
            buildConfigField("String", "DEV_CERT_SHA256", "\"\"")
            buildConfigField("String", "ALLOWED_DEV_HOSTS", "\"\"")
        }
    }

    buildTypes {
        release {
            isMinifyEnabled = false
            proguardFiles(getDefaultProguardFile("proguard-android-optimize.txt"), "proguard-rules.pro")
        }
    }

    compileOptions {
        sourceCompatibility = JavaVersion.VERSION_17
        targetCompatibility = JavaVersion.VERSION_17
    }

    kotlinOptions {
        jvmTarget = "17"
    }

    buildFeatures {
        compose = true
        buildConfig = true
    }

    composeOptions {
        kotlinCompilerExtensionVersion = "1.5.14"
    }
}

dependencies {
    val composeBom = platform("androidx.compose:compose-bom:2024.06.00")
    implementation(composeBom)

    implementation("androidx.core:core-ktx:1.13.1")
    implementation("com.google.android.material:material:1.12.0")
    implementation("androidx.lifecycle:lifecycle-runtime-ktx:2.8.4")
    implementation("androidx.activity:activity-compose:1.9.2")

    implementation("androidx.compose.ui:ui")
    implementation("androidx.compose.ui:ui-tooling-preview")
    implementation("androidx.compose.material3:material3")
    implementation("androidx.compose.material:material-icons-extended")
    implementation("androidx.navigation:navigation-compose:2.7.7")

    implementation("org.jetbrains.kotlinx:kotlinx-coroutines-android:1.8.1")
    implementation("org.jetbrains.kotlinx:kotlinx-serialization-json:1.6.3")

    implementation("com.squareup.okhttp3:okhttp:4.12.0")
    implementation("com.squareup.okhttp3:logging-interceptor:4.12.0")
    implementation("io.coil-kt:coil-compose:2.6.0")

    implementation("androidx.datastore:datastore-preferences:1.1.1")

    debugImplementation("androidx.compose.ui:ui-tooling")
}
