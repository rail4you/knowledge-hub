/**
 * 学期（Course.Semester）文案格式化。
 *
 * 后端 Semester 是教师自由输入的文本，历史数据里混着好几种写法：
 *  - 裸序号："5"（教师只填了第几学期）
 *  - 学年+学期："2026春"、"2025秋季"、"2026秋季"
 *  - 纯年份："2026"
 *  - 明显误填："1123"、"123"
 *
 * 学生端展示时若直接原样输出，裸数字夹在「中级」「14 个章节」这类信息旁边
 * 会被当成数量，反而更看不懂。这里统一转成自解释文案：
 *  - 裸序号（1~20）→ "第5学期"
 *  - 纯年份（2000~2099）→ "2026学年"
 *  - 已是学期写法（含「学期」或含春夏秋冬）→ 原样保留
 *  - 其余（空值、乱码）→ ''，调用方据此不显示这个 chip
 */
export function formatSemesterLabel(semester?: string | null): string {
  const value = (semester ?? '').trim();
  if (!value) return '';

  // 裸序号：1~20 视为学期序号
  if (/^\d{1,2}$/.test(value)) {
    const n = Number(value);
    return n >= 1 && n <= 20 ? `第${n}学期` : '';
  }

  // 纯年份
  if (/^(19|20)\d{2}$/.test(value)) {
    return `${value}学年`;
  }

  // 已经是学期写法
  if (value.includes('学期') || /[春夏秋冬]/.test(value)) {
    return value;
  }

  // 其余（"1123"、"123" 之类）无意义，不展示
  return '';
}
