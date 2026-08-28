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
import { provideHttpClient, withInterceptors } from '@angular/common/http';
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
  FolderOutline,
  FolderOpenOutline,
  MenuFoldOutline,
  MenuUnfoldOutline,
  AuditOutline,
  IdcardOutline,
  IdcardFill,
  // P0：employment-application 页用到的图标（之前未注册，导致 nz-icon 报 "does not exist"）
  BankOutline,
  CheckOutline,
  CloseOutline,
  CloseCircleOutline,
  FormOutline,
  MessageOutline,
  TrophyOutline,
  ClockCircleOutline,
  QuestionOutline,
  RollbackOutline,
  // 学生端 tab 图标
  HomeOutline,
  ReadOutline,
  LineChartOutline,
  RobotOutline,
  ExperimentOutline,
  CompassOutline,
  VideoCameraOutline,
  LogoutOutline,
  CaretDownOutline,
  CaretRightOutline,
  BulbOutline,
  HistoryOutline,
  AimOutline,
  PlayCircleOutline,
  PictureOutline,
  SoundOutline,
  RiseOutline,
  ZoomInOutline,
  ZoomOutOutline,
  DragOutline,
  SelectOutline,
  FullscreenOutline,
  LeftOutline,
  BookOutline,
  TeamOutline,
  CloudOutline,
  ApartmentOutline,
  ThunderboltOutline,
  RedoOutline,
  SaveOutline,
  CheckCircleOutline,
  // 招聘直播 & 其他页面
  AudioOutline,
  AudioMutedOutline,
  PhoneOutline,
  StopOutline,
  SwapOutline,
  CopyOutline,
  KeyOutline,
  UserOutline,
  // 微专业证书 & 学生端导航
  SafetyCertificateOutline,
  SafetyCertificateFill,
  UnorderedListOutline,
  // 租户主页
  SolutionOutline,
  ArrowRightOutline,
  // WASM 仿真实训中心 & 播放页
  ArrowLeftOutline,
  ExportOutline,
  ProfileOutline,
  HddOutline,
  ExclamationCircleOutline,
  PlayCircleFill,
  LinkOutline,
  UserAddOutline,
  // 课程详情 难度 / 统计 图标
  ThunderboltFill,
  FireOutline,
  BarChartOutline,
  // 就业服务大厅 & 就业管理页
  EnvironmentOutline,
  RedEnvelopeOutline,
  WalletOutline,
} from '@ant-design/icons-angular/icons';
import { environment } from '../environments/environment';
import { APP_ROUTES } from './app.routes';
import { APP_ROUTE_PROVIDER } from './route.provider';
import { ALLIANCE_ROUTE_PROVIDER } from './alliance-route.provider';
import { accountEditFormPropContributors } from './account-form-prop-contributors';
import { FOOTER_PROVIDER } from './footer/footer.config';
import { IDENTITY_ROLES_PROVIDER } from './identity-roles.config';
import { IDENTITY_USERS_PROVIDER } from './identity-users.config';
import { IdentityUserService } from '@abp/ng.identity/proxy';
import { CustomIdentityUserService } from './custom-identity-user.service';
import { checkInstallStatus } from './install/install.initializer';
import { authErrorInterceptor } from './core/auth/auth-error.interceptor';

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
  FilePptOutline,
  AppstoreOutline,
  RightOutline,
  DownOutline,
  FolderOutline,
  FolderOpenOutline,
  MenuFoldOutline,
  MenuUnfoldOutline,
  AuditOutline,
  IdcardOutline,
  IdcardFill,
  // P0：employment-application 页用到的图标
  BankOutline,
  CheckOutline,
  CloseOutline,
  CloseCircleOutline,
  FormOutline,
  MessageOutline,
  TrophyOutline,
  ClockCircleOutline,
  QuestionOutline,
  RollbackOutline,
  // 学生端 tab 图标
  HomeOutline,
  ReadOutline,
  LineChartOutline,
  RobotOutline,
  ExperimentOutline,
  CompassOutline,
  VideoCameraOutline,
  LogoutOutline,
  CaretDownOutline,
  CaretRightOutline,
  BulbOutline,
  HistoryOutline,
  AimOutline,
  PlayCircleOutline,
  PictureOutline,
  SoundOutline,
  RiseOutline,
  ZoomInOutline,
  ZoomOutOutline,
  DragOutline,
  SelectOutline,
  FullscreenOutline,
  LeftOutline,
  BookOutline,
  TeamOutline,
  CloudOutline,
  ApartmentOutline,
  FileOutline,
  ThunderboltOutline,
  RedoOutline,
  SaveOutline,
  CheckCircleOutline,
  // 招聘直播 & 其他页面
  AudioOutline,
  AudioMutedOutline,
  PhoneOutline,
  StopOutline,
  SwapOutline,
  CopyOutline,
  KeyOutline,
  UserOutline,
  // 微专业证书 & 学生端导航
  SafetyCertificateOutline,
  SafetyCertificateFill,
  UnorderedListOutline,
  // 租户主页
  SolutionOutline,
  ArrowRightOutline,
  // WASM 仿真实训中心 & 播放页
  ArrowLeftOutline,
  ExportOutline,
  ProfileOutline,
  HddOutline,
  ExclamationCircleOutline,
  PlayCircleFill,
  LinkOutline,
  UserAddOutline,
  // 课程详情 难度 / 统计 图标
  ThunderboltFill,
  FireOutline,
  BarChartOutline,
  // 就业服务大厅 & 就业管理页
  EnvironmentOutline,
  RedEnvelopeOutline,
  WalletOutline,
];

export const appConfig: ApplicationConfig = {
  providers: [
    provideRouter(APP_ROUTES),
    provideHttpClient(
      withInterceptors([authErrorInterceptor])
    ),
    APP_ROUTE_PROVIDER,
    ALLIANCE_ROUTE_PROVIDER,
    FOOTER_PROVIDER,
    IDENTITY_ROLES_PROVIDER,
    IDENTITY_USERS_PROVIDER,
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
