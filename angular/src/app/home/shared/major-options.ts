import type { PublicBrowseFilterOption } from '../../proxy/portal/models';

/**
 * 专业下拉选项。
 *
 * <para>
 * 门户是跨校聚合视图，同一个专业名可能由多所学校各自建立（专业是租户级数据）。
 * 若只用专业名作下拉值，同名项会互相覆盖，用户选了 A 校的专业却只能筛出其中一条，
 * 另一条同名专业永远查不到。这里统一改成「id 承载 + 名称展示」，同名跨校时追加学校后缀。
 * </para>
 */
export interface MajorOptionVm {
  /** 专业 Id（下拉值与筛选参数都用它） */
  id: string;
  /** 专业原名 */
  name: string;
  /** 下拉展示文案：同名跨校时形如「计算机科学与技术（启迪职业学院）」 */
  label: string;
  /** 所属租户 Id；null 表示平台级专业 */
  tenantId: string | null;
  /** 所属学校名称 */
  tenantName: string | null;
  /** 是否因为跨校同名而追加了学校后缀 */
  suffixed: boolean;
}

/** 平台级（host）专业的兜底归属名 */
const PLATFORM_OWNER = '平台';

/**
 * 把后端返回的专业筛选项转换成带展示文案的下拉选项。
 *
 * <para>规则：同一个专业名出现在多个不同租户时，给该名下的每一项都追加「（学校名）」后缀；
 * 只有一所学校拥有该专业名时保持原样，避免下拉被无意义的括号淹没。</para>
 */
export function buildMajorOptions(
  majors: PublicBrowseFilterOption[] | null | undefined,
): MajorOptionVm[] {
  const list = (majors ?? []).filter(
    (m): m is PublicBrowseFilterOption & { id: string; name: string } =>
      !!m?.id && !!m?.name,
  );

  // 统计每个专业名分布在多少个不同租户上（host 单独算一个归属）
  const nameOwners = new Map<string, Set<string>>();
  for (const m of list) {
    const key = m.name.trim();
    const owner = m.tenantId ?? `__host__`;
    if (!nameOwners.has(key)) {
      nameOwners.set(key, new Set<string>());
    }
    nameOwners.get(key)!.add(owner);
  }

  return list.map(m => {
    const name = m.name.trim();
    const duplicated = (nameOwners.get(name)?.size ?? 0) > 1;
    const tenantName = (m.tenantName ?? '').trim();
    const owner = tenantName || (m.tenantId ? '' : PLATFORM_OWNER);
    return {
      id: m.id,
      name,
      label: duplicated && owner ? `${name}（${owner}）` : name,
      tenantId: m.tenantId ?? null,
      tenantName: tenantName || null,
      suffixed: duplicated && !!owner,
    };
  });
}
