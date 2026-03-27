import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable, of, BehaviorSubject } from 'rxjs';
import { catchError, tap } from 'rxjs/operators';

export interface EditionDto {
  edition: string;
  maxTenantCount: number;
  isAllianceEnabled: boolean;
  isTwoLevelApprovalEnabled: boolean;
}

@Injectable({
  providedIn: 'root'
})
export class EditionService {
  private http = inject(HttpClient);
  private editionCache: EditionDto | null = null;
  private editionSubject = new BehaviorSubject<EditionDto | null>(null);
  
  readonly edition$ = this.editionSubject.asObservable();

  getEdition(): Observable<EditionDto> {
    if (this.editionCache) {
      return of(this.editionCache);
    }
    
    return this.http.get<EditionDto>('/api/app/edition/current').pipe(
      tap(edition => {
        this.editionCache = edition;
        this.editionSubject.next(edition);
      }),
      catchError(() => {
        const defaultEdition: EditionDto = {
          edition: 'Basic',
          maxTenantCount: 1,
          isAllianceEnabled: false,
          isTwoLevelApprovalEnabled: false
        };
        this.editionCache = defaultEdition;
        this.editionSubject.next(defaultEdition);
        return of(defaultEdition);
      })
    );
  }

  isBasicEdition(): boolean {
    return this.editionCache?.edition === 'Basic';
  }

  isStandardEdition(): boolean {
    return this.editionCache?.edition === 'Standard';
  }

  isAllianceEnabled(): boolean {
    return this.editionCache?.isAllianceEnabled ?? false;
  }

  isTwoLevelApprovalEnabled(): boolean {
    return this.editionCache?.isTwoLevelApprovalEnabled ?? false;
  }

  getMaxTenantCount(): number {
    return this.editionCache?.maxTenantCount ?? 1;
  }

  canCreateTenant(): boolean {
    if (this.isStandardEdition()) {
      return true;
    }
    return this.getMaxTenantCount() > 0;
  }

  clearCache(): void {
    this.editionCache = null;
    this.editionSubject.next(null);
  }
}