import { Pipe, PipeTransform } from '@angular/core';
import type { MajorLookupDto } from '../proxy/majors/dtos/models';

@Pipe({
  name: 'findMajorName',
  standalone: true,
})
export class FindMajorNamePipe implements PipeTransform {
  transform(majors: MajorLookupDto[] | null | undefined, id: string | null | undefined): string {
    if (!majors || !id) {
      return '';
    }
    const found = majors.find((m) => m.id === id);
    return found?.name || '';
  }
}
