import { api } from './api';
import { getAccessTokenFromStorage } from './auth';

/**
 * Download a resource file with auth token and trigger browser download
 */
export async function downloadResourceFile(resourceId: string, fileName: string) {
  const token = await getAccessTokenFromStorage();
  const response = await fetch(`/api/resource-file/${resourceId}/download`, {
    headers: {
      ...(token ? { Authorization: `Bearer ${token}` } : {}),
    },
  });

  if (!response.ok) {
    throw new Error(`Download failed: ${response.status}`);
  }

  const blob = await response.blob();
  const url = URL.createObjectURL(blob);
  const a = document.createElement('a');
  a.href = url;
  a.download = fileName;
  document.body.appendChild(a);
  a.click();
  document.body.removeChild(a);
  URL.revokeObjectURL(url);
}

/**
 * Fetch resource file as ArrayBuffer with auth token for preview
 */
export async function fetchResourcePreview(resourceId: string): Promise<ArrayBuffer> {
  const token = await getAccessTokenFromStorage();
  const response = await fetch(`/api/resource-file/${resourceId}/preview`, {
    headers: {
      ...(token ? { Authorization: `Bearer ${token}` } : {}),
    },
  });

  if (!response.ok) {
    throw new Error(`Preview failed: ${response.status}`);
  }

  return response.arrayBuffer();
}

export type FileType =
  | 'pdf'
  | 'word'
  | 'excel'
  | 'pptx'
  | 'image'
  | 'video'
  | 'audio'
  | 'text'
  | 'unsupported';

const EXTENSION_MAP: Record<string, FileType> = {
  pdf: 'pdf',
  docx: 'word',
  dotx: 'word',
  xls: 'excel',
  xlsx: 'excel',
  csv: 'excel',
  pptx: 'pptx',
  potx: 'pptx',
  jpg: 'image',
  jpeg: 'image',
  png: 'image',
  gif: 'image',
  bmp: 'image',
  webp: 'image',
  svg: 'image',
  mp4: 'video',
  webm: 'video',
  avi: 'video',
  mov: 'video',
  mp3: 'audio',
  wav: 'audio',
  ogg: 'audio',
  flac: 'audio',
  txt: 'text',
  json: 'text',
  xml: 'text',
  md: 'text',
  log: 'text',
  js: 'text',
  ts: 'text',
  css: 'text',
  html: 'text',
  htm: 'text',
  yml: 'text',
  yaml: 'text',
  sql: 'text',
};

export function getFileType(extension: string): FileType {
  const ext = extension.toLowerCase().replace('.', '');
  return EXTENSION_MAP[ext] || 'unsupported';
}

export function formatFileSize(bytes: number): string {
  if (bytes === 0) return '0 B';
  const k = 1024;
  const sizes = ['B', 'KB', 'MB', 'GB'];
  const i = Math.floor(Math.log(bytes) / Math.log(k));
  return parseFloat((bytes / Math.pow(k, i)).toFixed(2)) + ' ' + sizes[i];
}
