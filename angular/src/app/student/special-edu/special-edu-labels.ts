import { SpecialEduCategory } from '../../proxy/special-education/special-edu-category.enum';

/**
 * 特教学生端标签映射。
 *
 * 原先散落在教师端 service（special-edu.service.ts）里，学生端直接 import
 * 过去，造成学生端依赖教师端模块。本文件把学生端需要的映射独立出来，
 * 避免跨模块耦合。
 */

/** 类别：培智 / 听障 / 视障 / 孤独症 */
export const SPECIAL_EDU_CATEGORY_LABELS: Record<number, string> = {
  [SpecialEduCategory.Peizhi]: '培智',
  [SpecialEduCategory.TingZhang]: '听障',
  [SpecialEduCategory.ShiZhang]: '视障',
  [SpecialEduCategory.Guzuzheng]: '孤独症',
};

/** 多模态资源形式 */
export const SPECIAL_RESOURCE_MODALITY_LABELS: Record<string, string> = {
  Text: '文本资源',
  ImageDesc: '图片描述',
  AudioScript: '音频脚本',
  VideoScript: '视频脚本',
  SocialStory: '社交故事',
};

export function categoryLabel(value?: number | null): string {
  if (value === null || value === undefined) return '-';
  return SPECIAL_EDU_CATEGORY_LABELS[value] ?? String(value);
}

export function modalityLabel(value?: string | null): string {
  if (!value) return '-';
  return SPECIAL_RESOURCE_MODALITY_LABELS[value] ?? value;
}
