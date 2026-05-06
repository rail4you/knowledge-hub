import { eAccountComponents, type AccountEditFormPropContributors } from '@abp/ng.account';
import type { UpdateProfileDto } from '@abp/ng.account.core/proxy';
import type { FormProp, FormPropList } from '@abp/ng.components/extensible';

function removeFormProp(propList: FormPropList<UpdateProfileDto>, propName: string) {
  const prop = propList
    .toArray()
    .find(item => item.name.toLowerCase() === propName.toLowerCase());

  if (!prop) {
    return;
  }

  propList.dropByValue(prop, (value: FormProp<UpdateProfileDto>, candidate: FormProp<UpdateProfileDto>) => {
    return value.name === candidate.name;
  });
}

function profileFormContributor(propList: FormPropList<UpdateProfileDto>) {
  removeFormProp(propList, 'RoleType');
  removeFormProp(propList, 'SchoolId');
}

export const accountEditFormPropContributors: AccountEditFormPropContributors = {
  [eAccountComponents.PersonalSettings]: [profileFormContributor],
};
