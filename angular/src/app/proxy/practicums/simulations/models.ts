import type { EntityDto } from '@abp/ng.core';
import type { PracticumSimulationStatus } from '../enums/practicum-simulation-status.enum';

export interface CreatePracticumSimulationDto {
  projectId?: string;
  name?: string;
  description?: string | null;
  coverUrl?: string | null;
  entryPath?: string | null;
  uploadedFilePath?: string;
}

export interface PracticumSimulationDto extends EntityDto<string> {
  projectId?: string;
  name?: string;
  slug?: string;
  entryPath?: string;
  description?: string | null;
  coverUrl?: string | null;
  status?: PracticumSimulationStatus;
  fileCount?: number;
  totalBytes?: number;
  coopCoepRequired?: boolean;
  sortOrder?: number;
  publicUrl?: string;
  creationTime?: string;
}

export interface UpdatePracticumSimulationDto {
  name?: string;
  description?: string | null;
  coverUrl?: string | null;
  sortOrder?: number;
}
