import { eIdentityComponents, IdentityCreateFormPropContributors, IdentityEntityPropContributors } from '@abp/ng.identity';
import { IdentityUserDto } from '@abp/ng.identity/proxy';
import { ePropType, FormProp, FormPropList, EntityPropList } from '@abp/ng.components/extensible';
import { TenantService } from '@abp/ng.tenant-management/proxy';
import { map } from 'rxjs/operators';
import { of } from 'rxjs';

let cachedTenants: { id?: string; name?: string }[] = [];

const tenantProp = new FormProp<IdentityUserDto>({
  type: ePropType.String,
  name: 'tenantId',
  displayName: '所属租户',
  isExtra: false,
  options: (data) => {
    const tenantService = data.getInjected(TenantService);
    if (cachedTenants.length > 0) {
      return of([
        { key: '全局', value: null },
        ...cachedTenants.map((tenant) => ({
          key: tenant.name || '',
          value: tenant.id,
        }))
      ]);
    }
    return tenantService.getList({ maxResultCount: 100 }).pipe(
      map((result) => {
        cachedTenants = result.items;
        return [
          { key: '全局', value: null },
          ...result.items.map((tenant) => ({
            key: tenant.name || '',
            value: tenant.id,
          }))
        ];
      })
    );
  },
  defaultValue: null,
});

export function tenantPropContributor(propList: FormPropList<IdentityUserDto>) {
  propList.addByIndex(tenantProp, 0);
}

export function hideRoleTypeAndSchoolIdContributor(propList: EntityPropList<IdentityUserDto>) {
  const props = propList.toArray ? propList.toArray() : (propList as any).items || [];
  const roleTypeProp = props.find((x: any) => x.name === 'RoleType');
  if (roleTypeProp) {
    roleTypeProp.columnVisible = () => false;
  }
  const schoolIdProp = props.find((x: any) => x.name === 'SchoolId');
  if (schoolIdProp) {
    schoolIdProp.columnVisible = () => false;
  }
}

export const identityUserCreateFormPropContributors: IdentityCreateFormPropContributors = {
  [eIdentityComponents.Users]: [tenantPropContributor],
};

export const identityUserEntityPropContributors: IdentityEntityPropContributors = {
  [eIdentityComponents.Users]: [hideRoleTypeAndSchoolIdContributor],
};