#!/bin/bash
# 构建 document-parser.jar

# 检查 Maven
if ! command -v mvn &> /dev/null; then
    echo "错误: 需要安装 Maven"
    exit 1
fi

# 检查 Java
if ! command -v java &> /dev/null; then
    echo "错误: 需要安装 Java"
    exit 1
fi

SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
cd "$SCRIPT_DIR"

# 从 META-INF 复制 pom.xml
if [ ! -f "pom.xml" ]; then
    cp META-INF/maven/com.example/document-parser/pom.xml .
    echo "已复制 pom.xml"
fi

# 构建
echo "开始构建 document-parser.jar..."
mvn clean package -DskipTests

if [ -f "target/document-parser-1.0-SNAPSHOT.jar" ]; then
    cp target/document-parser-1.0-SNAPSHOT.jar ./document-parser.jar
    echo "构建成功: $(ls -lh document-parser.jar | awk '{print $5}')"
else
    echo "构建失败"
    exit 1
fi