import type { DocumentType } from './document-type.enum';
import type { AuditedEntityDto, EntityDto } from '@abp/ng.core';

export interface CreateUpdateDocumentDto {
  name: string;
  type: DocumentType;
  publishDate: string;
  price: number;
  userId?: string;
}

export interface DocumentDto extends AuditedEntityDto<string> {
  userId?: string;
  userName?: string;
  name?: string;
  type?: DocumentType;
  publishDate?: string;
  price?: number;
}

export interface UserLookupDto extends EntityDto<string> {
  name?: string;
}
