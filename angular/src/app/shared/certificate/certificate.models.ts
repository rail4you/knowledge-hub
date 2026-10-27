/**
 * 证书画布通用模型（教学资源库结业证书 / 微专业证书共用）。
 *
 * <para>PRD 第 9 节要求证书模板含：校徽位、学员姓名、课程名称、结业时间、
 * 证书编号、防伪二维码等字段。这里把可插入的占位符字段统一定义出来，
 * 模板编辑器负责摆放位置，发证时把真实数据合成到图上。</para>
 */

/** 占位符图层配置（相对坐标 0-100，字体大小以 1000px 宽为基准）。 */
export interface CertificateLayer {
  id: string;
  fieldType: string;
  label: string;
  customFieldName?: string;
  x: number;
  y: number;
  fontSize: number;
  color: string;
  fontWeight: number;
  center: boolean;
  fontFamily?: string;
}

/** 发证时填充到占位图层的真实数据。 */
export interface CertificateRenderData {
  studentName?: string;
  studentNo?: string;
  advisor?: string;
  issueDate?: string;
  certificateNo?: string;
  validUntil?: string;
  /** 课程名称（教学资源库结业证书）。 */
  courseTitle?: string;
  /** 主建学校名称。 */
  schoolName?: string;
  /** 资源库名称。 */
  libraryName?: string;
  /** 防伪校验码（二维码内容）。 */
  verifyCode?: string;
  /** 微专业名称（微专业证书）。 */
  microMajorTitle?: string;
  custom?: Record<string, string>;
}
