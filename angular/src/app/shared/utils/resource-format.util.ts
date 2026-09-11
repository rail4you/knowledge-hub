import { ResourceType } from '../../proxy/resources/enums/resource-type.enum';

const RESOURCE_TYPE_NAMES: Record<number, string> = {
  [ResourceType.Document]: '文档',
  [ResourceType.Video]: '视频',
  [ResourceType.Audio]: '音频',
  [ResourceType.Image]: '图片',
  [ResourceType.PPT]: '演示文稿',
};

/** 资源类型名称（模块级常量表，避免模板每次变更检测重新创建对象） */
export function resourceTypeName(type?: number | null): string {
  return RESOURCE_TYPE_NAMES[type ?? 0] || '资料';
}

/** 资源文件大小展示文本 */
export function fileSizeText(size?: number | null): string {
  if (!size) return '未知大小';
  if (size >= 1024 * 1024) return `${(size / 1024 / 1024).toFixed(1)} MB`;
  if (size >= 1024) return `${(size / 1024).toFixed(0)} KB`;
  return `${size} B`;
}
