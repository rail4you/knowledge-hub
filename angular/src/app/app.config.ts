import { provideAbpCore, withOptions, AuthService } from '@abp/ng.core';
import { provideAbpOAuth } from '@abp/ng.oauth';
import { CustomAuthService } from './core/auth/custom-auth.service';
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
import { authRecoveryInterceptor } from './core/auth/auth-recovery.interceptor';
import { cacheInvalidationInterceptor } from './shared/cache/cache-invalidation.interceptor';
import { provideAnimations } from '@angular/platform-browser/animations';
import { provideServiceWorker } from '@angular/service-worker';
import { provideRouter, withInMemoryScrolling } from '@angular/router';
import { IconDefinition } from '@ant-design/icons-angular';
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
  DeploymentUnitOutline,
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
  ScheduleOutline,
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
  CaretRightFill,
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
  ArrowUpOutline,
  ArrowDownOutline,
  ReloadOutline,
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
  MutedOutline,
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
  MailOutline,
  // 课程详情 难度 / 统计 图标
  ThunderboltFill,
  FireOutline,
  BarChartOutline,
  // 就业服务大厅 & 就业管理页
  EnvironmentOutline,
  RedEnvelopeOutline,
  WalletOutline,
  // 资讯点赞 & 收藏实心态（未注册会导致 nz-icon 空渲染）
  LikeOutline,
  LikeFill,
  HeartOutline,
  HeartFill,
  // 搜索详情页“匹配高亮”面板标题图标：原 nzType="highlight" 未注册到图标表，
  // 运行时只会报 "icon highlight-o does not exist" 并渲染成空白
  HighlightOutline,
  StarFill,
  BellOutline,
  // 学生端实训详情·沟通 tab 图标
  CommentOutline,
  // 学生端资源库 / 资讯页筛选与分享图标
  MenuOutline,
  ShareAltOutline,
  // 学生端页面缺失图标补齐（消除运行时 SVG 404）
  FolderFill,
  FolderOpenFill,
  ClearOutline,
  CloudUploadOutline,
  CopyrightOutline,
  FileAddOutline,
  FlagOutline,
  HourglassOutline,
  InfoCircleOutline,
  LockOutline,
  PaperClipOutline,
  PauseCircleOutline,
  PieChartOutline,
  PushpinOutline,
  QuestionCircleOutline,
  WarningOutline,
  // 资讯管理·下架/上架按钮（未注册会抛 IconNotFoundError，连带按钮上的
  // popconfirm 点击失效）
  DownCircleOutline,
  UpCircleOutline,
  // 学生端空状态/错误态：服务不可用提示
  WifiOutline,
} from '@ant-design/icons-angular/icons';
import { environment } from '../environments/environment';
import { APP_ROUTES } from './app.routes';
import { APP_ROUTE_PROVIDER } from './route.provider';
import { ALLIANCE_ROUTE_PROVIDER } from './alliance-route.provider';
import { SPECIAL_EDU_ROUTE_PROVIDER } from './special-edu-route.provider';
import { VOICE_ASSISTANT_ROUTE_PROVIDER } from './voice-assistant-route.provider';
import { accountEditFormPropContributors } from './account-form-prop-contributors';
import { FOOTER_PROVIDER } from './footer/footer.config';
import { SCHOOL_DOMAIN_PROVIDER } from './core/tenant/school-domain.config';
import { AI_NOTIFICATION_PROVIDER } from './ai/notification/ai-notification.provider';
import { IDENTITY_ROLES_PROVIDER } from './identity-roles.config';
import { IDENTITY_USERS_PROVIDER } from './identity-users.config';
import { LOGIN_PROVIDER } from './login/login.config';
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
  DeploymentUnitOutline,
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
  ScheduleOutline,
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
  CaretRightFill,
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
  ArrowUpOutline,
  ArrowDownOutline,
  ReloadOutline,
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
  MutedOutline,
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
  MailOutline,
  // 课程详情 难度 / 统计 图标
  ThunderboltFill,
  FireOutline,
  BarChartOutline,
  // 就业服务大厅 & 就业管理页
  EnvironmentOutline,
  RedEnvelopeOutline,
  WalletOutline,
  // 资讯点赞 & 收藏实心态
  LikeOutline,
  LikeFill,
  HeartOutline,
  HeartFill,
  // 搜索详情页“匹配高亮”面板标题图标：原 nzType="highlight" 未注册到图标表，
  // 运行时只会报 "icon highlight-o does not exist" 并渲染成空白
  HighlightOutline,
  StarFill,
  // AI 任务通知铃铛
  BellOutline,
  // 学生端实训详情·沟通 tab 图标
  CommentOutline,
  // 学生端资源库 / 资讯页筛选与分享图标
  MenuOutline,
  ShareAltOutline,
  // 学生端页面缺失图标补齐（消除运行时 SVG 404）
  FolderFill,
  FolderOpenFill,
  ClearOutline,
  CloudUploadOutline,
  CopyrightOutline,
  FileAddOutline,
  FlagOutline,
  HourglassOutline,
  InfoCircleOutline,
  LockOutline,
  PaperClipOutline,
  PauseCircleOutline,
  PieChartOutline,
  PushpinOutline,
  QuestionCircleOutline,
  WarningOutline,
  DownCircleOutline,
  UpCircleOutline,
  WifiOutline,
];

export const appConfig: ApplicationConfig = {
  providers: [
    // 每次导航回到顶部，并禁用浏览器滚动位置恢复：
    // 异步渲染长页面（如课程详情章节树默认展开）时，浏览器可能保持旧的滚动锚点，
    // 导致用户从列表进入详情后落在页面底部。
    provideRouter(APP_ROUTES, withInMemoryScrolling({
      scrollPositionRestoration: 'top',
      anchorScrolling: 'disabled',
    })),
    // 注：core/auth/auth-error.interceptor 之前是无操作的透传拦截器（仅 inject + next，
    // 无任何错误处理逻辑），已移除注册以减少每个请求的开销；401/403 统一处理见
    // AuthErrorService + app-auth-error-modal，相关页面直接调用 handleAuthError。
    // 全局认证恢复：401 静默刷新+重放、重启窗口期 GET 瞬态重试，
    // 失败则弹重新登录（见 core/auth/auth-recovery.interceptor）。
    provideHttpClient(withInterceptors([authRecoveryInterceptor, cacheInvalidationInterceptor])),
    APP_ROUTE_PROVIDER,
    ALLIANCE_ROUTE_PROVIDER,
    SPECIAL_EDU_ROUTE_PROVIDER,
    VOICE_ASSISTANT_ROUTE_PROVIDER,
    FOOTER_PROVIDER,
    // 域名绑定多租户：启动时探测当前域名归属的学校，锁定会话租户与登录页机构
    SCHOOL_DOMAIN_PROVIDER,
    AI_NOTIFICATION_PROVIDER,
    IDENTITY_ROLES_PROVIDER,
    IDENTITY_USERS_PROVIDER,
    provideIdentityConfig(),
    provideSettingManagementConfig(),
    provideFeatureManagementConfig(),
    provideThemeLeptonX(),
    LPX_LAYOUT_PROVIDER,
    provideSideMenuLayout(),
    provideLogo(withEnvironmentOptions(environment)),
    provideAccountConfig(),
    provideAnimations(),
    provideNzIcons(icons),
    provideAbpCore(
      withOptions({
        environment,
        registerLocaleFn: registerLocaleForEsBuild(),
      }),
    ),
    provideAbpOAuth(),
    // 关键修复：覆盖默认 AuthService，登出时清除 __host_login cookie，
    // 避免用户在 /admin-login 流程后登出再点击首页「登录」时仍走宿主模式。
    // 必须在 provideAbpOAuth() 之后注册以覆盖其提供的 AbpOAuthService。
    { provide: AuthService, useClass: CustomAuthService },
    // 注意顺序：LOGIN_PROVIDER 必须在 provideAccountConfig() 之后。
    // 它的初始化器会 patch ABP account 路由的 layout（account→empty，修复
    // /account/login 白屏），而 configureRoutes() 会重写这些节点，patch 必须
    // 在 configureRoutes() 注册的初始化器之后执行（providers 顺序 = 初始化器顺序）。
    LOGIN_PROVIDER,
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
    // PWA：生产环境启用 Angular Service Worker 缓存应用外壳与静态资源
    provideServiceWorker('ngsw-worker.js', {
      enabled: environment.production,
      registrationStrategy: 'registerWhenStable:30000',
    }),
  ]
};
