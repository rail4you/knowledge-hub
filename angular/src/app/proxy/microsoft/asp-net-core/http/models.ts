/** C# Microsoft.Extensions.Primitives.StringValues 在前端没有对应类型，用联合类型兜底。 */
type StringValues = string | string[];

export interface IFormFile {
  contentType?: string;
  contentDisposition?: string;
  headers?: Record<string, StringValues>;
  length?: number;
  name?: string;
  fileName?: string;
}
