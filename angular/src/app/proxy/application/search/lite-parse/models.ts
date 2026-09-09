import type { PageContentDto } from '../../contracts/search/models';

export interface LiteParseExtractionResult {
  pages?: PageContentDto[];
  pageWidths?: number[];
  pageHeights?: number[];
  textItemsJson?: string[];
}
