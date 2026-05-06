import { provideAbpCore, withOptions } from '@abp/ng.core';
import { provideAbpOAuth } from '@abp/ng.oauth';
import { provideSettingManagementConfig } from '@abp/ng.setting-management/config';
import { provideFeatureManagementConfig } from '@abp/ng.feature-management';
import { provideAbpThemeShared,} from '@abp/ng.theme.shared';
import { provideIdentityConfig } from '@abp/ng.identity/config';
import { provideAccountConfig } from '@abp/ng.account/config';
import { ACCOUNT_CONFIG_OPTIONS } from '@abp/ng.account';
import { provideTenantManagementConfig } from '@abp/ng.tenant-management/config';
import { registerLocaleForEsBuild } from '@abp/ng.core/locale';
import { provideThemeLeptonX } from '@abp/ng.theme.lepton-x';
import { LPX_LAYOUT_PROVIDER, provideSideMenuLayout } from '@abp/ng.theme.lepton-x/layouts';
import { provideLogo, withEnvironmentOptions } from "@abp/ng.theme.shared";
import { ApplicationConfig, APP_INITIALIZER } from '@angular/core';
import { provideAnimations } from '@angular/platform-browser/animations';
import { provideRouter } from '@angular/router';
import { provideNzIcons } from 'ng-zorro-antd/icon';
import {
  PlusOutline,
  DownloadOutline,
  StarOutline,
  EditOutline,
  SendOutline,
  InboxOutline,
  DeleteOutline,
  LoadingOutline,
  UploadOutline,
  EyeOutline,
  EyeInvisibleOutline,
  SearchOutline,
  DatabaseOutline,
  CalendarOutline,
  FileWordOutline,
  FileExcelOutline,
  FilePdfOutline,
  FileTextOutline,
  FileImageOutline,
  FileOutline,
  FilePptOutline,
  AppstoreOutline,
  RightOutline,
  DownOutline,
} from '@ant-design/icons-angular/icons';
import { environment } from '../environments/environment';
import { APP_ROUTES } from './app.routes';
import { APP_ROUTE_PROVIDER } from './route.provider';
import { ALLIANCE_ROUTE_PROVIDER } from './alliance-route.provider';
import { accountEditFormPropContributors } from './account-form-prop-contributors';
import { FOOTER_PROVIDER } from './footer/footer.config';
import { IDENTITY_ROLES_PROVIDER } from './identity-roles.config';
import { LOGIN_PROVIDER } from './login/login.config';
import { IDENTITY_USERS_PROVIDER } from './identity-users.config';
import { IdentityUserService } from '@abp/ng.identity/proxy';
import { CustomIdentityUserService } from './custom-identity-user.service';
import { checkInstallStatus } from './install/install.initializer';

const icons = [
  PlusOutline,
  DownloadOutline,
  StarOutline,
  EditOutline,
  SendOutline,
  InboxOutline,
  DeleteOutline,
  LoadingOutline,
  UploadOutline,
  EyeOutline,
  EyeInvisibleOutline,
  SearchOutline,
  DatabaseOutline,
  CalendarOutline,
  FileWordOutline,
  FileExcelOutline,
  FilePdfOutline,
  FileTextOutline,
  FileImageOutline,
  FileOutline,
  FilePptOutline,
  AppstoreOutline,
  RightOutline,
  DownOutline,
];

export const appConfig: ApplicationConfig = {
  providers: [
    provideRouter(APP_ROUTES),
    APP_ROUTE_PROVIDER,
    ALLIANCE_ROUTE_PROVIDER,
    FOOTER_PROVIDER,
    IDENTITY_ROLES_PROVIDER,
    IDENTITY_USERS_PROVIDER,
    LOGIN_PROVIDER,
    provideAnimations(),
    provideNzIcons(icons),
    provideAbpCore(
      withOptions({
        environment,
        registerLocaleFn: registerLocaleForEsBuild(),
      }),
    ),
    provideAbpOAuth(),
    provideIdentityConfig(),
    provideSettingManagementConfig(),
    provideFeatureManagementConfig(),
    provideThemeLeptonX(),
    LPX_LAYOUT_PROVIDER,
    provideSideMenuLayout(),
    provideLogo(withEnvironmentOptions(environment)),
    provideAccountConfig(),
    {
      provide: ACCOUNT_CONFIG_OPTIONS,
      useValue: {
        editFormPropContributors: accountEditFormPropContributors,
      },
    },
    provideTenantManagementConfig(),
    provideAbpThemeShared(),
    { provide: IdentityUserService, useClass: CustomIdentityUserService },
    {
      provide: APP_INITIALIZER,
      useFactory: checkInstallStatus,
      multi: true,
    },
  ]
};
