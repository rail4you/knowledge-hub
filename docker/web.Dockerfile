# KnowledgeHub Angular Frontend Dockerfile
FROM node:20-alpine AS build

# Install dependencies
WORKDIR /app

# Copy package files
COPY angular/package.json angular/yarn.lock ./

# Install dependencies using yarn
RUN yarn install --frozen-lockfile

# Copy source files
COPY angular ./

# Set environment variables for production
ENV NG_CLI_ANALYTICS=false
ENV NODE_OPTIONS="--max-old-space-size=4096"

# Build the Angular application
RUN npm run build -- --configuration=production

# Production image with nginx
FROM nginx:alpine AS production

# Copy custom nginx config
COPY docker/nginx.conf /etc/nginx/conf.d/default.conf

# Copy built Angular files
COPY --from=build /app/dist/KnowledgeHub/browser /usr/share/nginx/html

# Expose port 80
EXPOSE 80

# Health check
HEALTHCHECK --interval=30s --timeout=10s --start-period=10s --retries=3 \
    CMD wget --no-verbose --tries=1 --spider http://localhost/ || exit 1

CMD ["nginx", "-g", "daemon off;"]
