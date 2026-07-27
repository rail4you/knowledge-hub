# ============================================================
# Dockerfile - Angular 管理端 + 首页 生产镜像
# ============================================================
FROM node:20-alpine AS angular-build

WORKDIR /src/angular

# Use npmmirror.com for faster downloads in China
RUN npm config set registry https://registry.npmmirror.com

# Pass proxy from docker build --build-arg (optional, for npm install)
ARG HTTP_PROXY
ARG HTTPS_PROXY
ARG NO_PROXY
ENV HTTP_PROXY=${HTTP_PROXY} \
    HTTPS_PROXY=${HTTPS_PROXY} \
    NO_PROXY=${NO_PROXY}

COPY . /src
RUN npm ci --legacy-peer-deps
RUN npm run build -- --configuration production

# ----------------------------------------------------------
# 生产镜像
# ----------------------------------------------------------
FROM nginx:alpine

WORKDIR /app

# Angular 作为站点根首页 + 管理端
COPY --from=angular-build /src/angular/dist/KnowledgeHub/browser /usr/share/nginx/html

# 复制 nginx 配置
COPY angular/nginx.conf /etc/nginx/conf.d/default.conf

# 复制动态环境配置（运行时可覆盖）
COPY angular/dynamic-env.json /usr/share/nginx/html/dynamic-env.json

EXPOSE 80

CMD ["nginx", "-g", "daemon off;"]
