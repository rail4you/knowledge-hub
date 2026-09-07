import { inject, provideAppInitializer } from '@angular/core';
import { RoutesService, eLayoutType } from '@abp/ng.core';

/**
 * 语音助手管理入口菜单（应用布局左侧导航）：
 * 始终注册，可见性完全由 KnowledgeHub.VoiceAssistant.Manage 控制
 * （仅 host 全局管理员持有，种子见 RolePermissionSeeder）。
 */
export const VOICE_ASSISTANT_ROUTE_PROVIDER = [
  provideAppInitializer(() => {
    configureVoiceAssistantRoutes();
  }),
];

function configureVoiceAssistantRoutes() {
  const routes = inject(RoutesService);

  routes.add([
    {
      path: '/admin/voice-assistant',
      name: '::Menu:VoiceAssistantAdmin',
      iconClass: 'fas fa-microphone',
      parentName: 'AbpUiNavigation::Menu:Administration',
      layout: eLayoutType.application,
      requiredPolicy: 'KnowledgeHub.VoiceAssistant.Manage',
    },
  ]);
}
