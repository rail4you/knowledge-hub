import { ResourceType } from '../../proxy/resources/enums/resource-type.enum';
import type { ResourceDto } from '../../proxy/resources/models';

/**
 * 学生端「资源」相关页面的枚举文案与展示辅助。
 *
 * 规范要求：枚举标签集中放在模块的 *-labels.ts 里，不要散落到各组件模板中。
 */

/** 资源类型（后端枚举 ResourceType）→ 展示文案 */
export const RESOURCE_TYPE_LABELS: Record<number, string> = {
  [ResourceType.Document]: '文档',
  [ResourceType.Video]: '视频',
  [ResourceType.Audio]: '音频',
  [ResourceType.Image]: '图片',
  [ResourceType.PPT]: '演示文稿',
};

/** 「资源格式」筛选项，对应后端 ResourceListQueryDto.resourceType */
export interface ResourceTypeFilterOption {
  value: ResourceType;
  label: string;
}

export const RESOURCE_TYPE_FILTER_OPTIONS: ResourceTypeFilterOption[] = [
  { value: ResourceType.Document, label: '文档' },
  { value: ResourceType.Video, label: '视频' },
  { value: ResourceType.Audio, label: '音频' },
  { value: ResourceType.Image, label: '图片' },
  { value: ResourceType.PPT, label: '演示文稿' },
];

/** 资源类型文案（未知类型回退为「资料」） */
export function resourceTypeLabel(type?: number | null): string {
  return RESOURCE_TYPE_LABELS[type ?? 0] ?? '资料';
}

/**
 * 行首的格式角标：优先真实文件扩展名（真实 API 字段），
 * 没有扩展名时回退到资源类型文案。
 */
export function resourceFormatTag(resource: ResourceDto | null | undefined): string {
  const ext = (resource?.fileExtension || '').replace(/^\./, '').trim();
  if (ext) return ext.toUpperCase();

  const fileName = resource?.originalFileName || resource?.name || '';
  const dot = fileName.lastIndexOf('.');
  if (dot >= 0 && dot < fileName.length - 1) {
    const fromName = fileName.substring(dot + 1).trim();
    if (fromName) return fromName.toUpperCase();
  }

  return resourceTypeLabel(resource?.resourceType);
}

/**
 * 资源来源：共享来源院校 → 所属机构 → 上传人 → 当前租户名。
 * 全部来自真实 API 字段，没有来源信息时返回空串（不编造）。
 */
export function resourceSourceName(
  resource: ResourceDto | null | undefined,
  tenantName?: string | null,
): string {
  return (
    resource?.sourceTenantName ||
    resource?.organizationName ||
    resource?.creatorName ||
    tenantName ||
    ''
  );
}

/** 资源更新时间：优先最后修改时间，回退创建时间 */
export function resourceUpdatedAt(resource: ResourceDto | null | undefined): string {
  const value = resource?.lastModificationTime || resource?.creationTime;
  if (!value) return '';
  return typeof value === 'string' ? value : value.toISOString();
}
