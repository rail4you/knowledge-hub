
export interface LiteparsePageDto {
  page?: number;
  width?: number;
  height?: number;
  text?: string | null;
  textItems?: LiteparseTextItemDto[] | null;
}

export interface LiteparseResultDto {
  pages?: LiteparsePageDto[];
}

export interface LiteparseTextItemDto {
  text?: string;
  x?: number;
  y?: number;
  width?: number;
  height?: number;
  fontName?: string | null;
  fontSize?: number | null;
}
