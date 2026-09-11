import { createBytesCache } from '../cache/bytes-cache';

/**
 * Office 文档（PPTX/DOCX/XLSX）转换后 PDF 的持久化预览缓存。
 *
 * 后端已把转换结果缓存到磁盘，`/preview-pdf` 每次返回同一份文件。这里用浏览器
 * Cache Storage 把整份 PDF 持久化：跨页面刷新、新开标签、下次访问都以缓存为主，
 * 直接交给 pdfjs 渲染，免去重复下载（实测整份 PDF 可达 10MB+，下载 10s+）。
 */
const pdfCache = createBytesCache('kh-pdf-preview-v1');

/** 仅读取本地缓存（不发起网络请求）；无缓存或已过期返回 null */
export const readCachedPdf = pdfCache.read;

/** 把已加载的整份 PDF 字节写入持久化缓存（来源可为 pdfjs 的 getData()） */
export const storePdfBytes = pdfCache.store;

/** 清空持久化预览缓存（资源更新等场景可调用） */
export const clearPdfPreviewCache = pdfCache.clear;
