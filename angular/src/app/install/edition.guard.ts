import { inject } from '@angular/core';
import { EditionService } from './edition.service';

export function editionGuard() {
  const editionService = inject(EditionService);
  
  return () => editionService.isStandardEdition();
}