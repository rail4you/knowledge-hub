export interface IFormFile {
  contentType?: string;
  contentDisposition?: string;
  headers?: Record<string, string | string[]>;
  length?: number;
  name?: string;
  fileName?: string;
}

export interface StringValues {
  count: number;
  item: (index: number) => string;
}