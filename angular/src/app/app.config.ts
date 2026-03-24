import { provideAbpCore, withOptions } from '@abp/ng.core';
import { provideAbpOAuth } from '@abp/ng.oauth';
import { provideSettingManagementConfig } from '@abp/ng.setting-management/config';
import { provideFeatureManagementConfig } from '@abp/ng.feature-management';
import { provideAbpThemeShared,} from '@abp/ng.theme.shared';
import { provideIdentityConfig } from '@abp/ng.identity/config';
import { provideAccountConfig } from '@abp/ng.account/config';
import { provideTenantManagementConfig } from '@abp/ng.tenant-management/config';
import { registerLocaleForEsBuild } from '@abp/ng.core/locale';
import { provideThemeLeptonX } from '@abp/ng.theme.lepton-x';
import { LPX_LAYOUT_PROVIDER, provideSideMenuLayout } from '@abp/ng.theme.lepton-x/layouts';
import { provideLogo, withEnvironmentOptions } from "@abp/ng.theme.shared";
import { ApplicationConfig } from '@angular/core';
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
  UploadOutline
} from '@ant-design/icons-angular/icons';
import { environment } from '../environments/environment';
import { APP_ROUTES } from './app.routes';
import { APP_ROUTE_PROVIDER } from './route.provider';
import { FOOTER_PROVIDER } from './footer/footer.config';

const icons = [
  PlusOutline, 
  DownloadOutline, 
  StarOutline, 
  EditOutline, 
  SendOutline, 
  InboxOutline, 
  DeleteOutline, 
  LoadingOutline,
  UploadOutline
];

export const appConfig: ApplicationConfig = {
  providers: [
    provideRouter(APP_ROUTES),
    APP_ROUTE_PROVIDER,
    FOOTER_PROVIDER,
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
    provideTenantManagementConfig(),
    provideAbpThemeShared(),
  ]
};
