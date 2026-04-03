
export interface OpenDataLoaderPageDto {
  page?: number;
  width?: number;
  height?: number;
  text?: string | null;
}

export interface OpenDataLoaderResultDto {
  numberOfPages?: number;
  pages?: OpenDataLoaderPageDto[];
}
