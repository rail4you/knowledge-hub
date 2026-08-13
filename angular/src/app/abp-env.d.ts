import '@abp/ng.core';

declare module '@abp/ng.core' {
  interface ApplicationInfo {
    /** 上传文件大小上限（字节），来自 dynamic-env.json / 后端 App:MaxFileSizeBytes。 */
    maxFileSizeBytes?: number;
  }
}
