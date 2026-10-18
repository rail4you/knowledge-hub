/**
 * 习题答案的展示格式化。
 *
 * 判断题（ExerciseType.TrueFalse）的 StudentAnswer 在库里存的是 "true"/"false"，
 * 直接展示会让老师/学生在学习统计里看到英文布尔值，需要渲染成“正确/错误”。
 */

/**
 * 把判断题答案归一化为 true / false / null（无法识别）。
 *
 * 取值范围与后端 `StudentExerciseRecordAppService.NormalizeTrueFalse` 严格保持一致 ——
 * 两边规则不一致会导致“统计里显示正确、批改时判错”这类自相矛盾的结果。
 */
export function normalizeTrueFalse(raw: string | null | undefined): boolean | null {
  if (!raw) return null;
  let t = raw.trim().toLowerCase();
  if (!t) return null;
  // 去掉首尾引号/句号等噪音（对应后端的 Trim('\'', '"', '。', '.', '!', '！')）
  t = t.replace(/^['"”。.！!]+/, '').replace(/['"、。.！!]+$/, '');
  if (['true', 't', '1', 'yes', 'y', '√', '✓', '对', '正确', '是', '真', 'right'].includes(t)) return true;
  if (['false', 'f', '0', 'no', 'n', '×', 'x', '错', '错误', '否', '假', 'wrong'].includes(t)) return false;
  return null;
}

/**
 * 判断题答案的展示文本：识别得出就渲染成“正确/错误”，否则原样返回。
 *
 * 必须传入题型来判断是否只对判断题生效 —— 否则简答题答案里恰好写了
 * "true"/"1" 之类也会被误改写。
 */
export function formatTrueFalseAnswer(
  answer: string | null | undefined,
  isTrueFalseQuestion: boolean
): string {
  const raw = (answer ?? '').trim();
  if (!raw || !isTrueFalseQuestion) return raw;
  const v = normalizeTrueFalse(raw);
  return v === null ? raw : v ? '正确' : '错误';
}
