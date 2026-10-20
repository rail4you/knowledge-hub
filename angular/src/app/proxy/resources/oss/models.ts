
export interface OssConfigDto {
  endpoint?: string;
  accessKeyId?: string;
  hasAccessKeySecret?: boolean;
  bucketName?: string;
  uploadPath?: string;
  urlPrefix?: string;
  isConfigured?: boolean;
  source?: string;
  publicHost?: string;
}

export interface OssTestResultDto {
  success?: boolean;
  error?: string | null;
  probeUrl?: string | null;
}

export interface UpdateOssConfigDto {
  endpoint?: string;
  accessKeyId?: string;
  accessKeySecret?: string | null;
  clearAccessKeySecret?: boolean;
  bucketName?: string;
  uploadPath?: string;
  urlPrefix?: string;
}
