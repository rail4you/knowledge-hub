import { Component, ChangeDetectionStrategy } from '@angular/core';
import { CommonModule } from '@angular/common';
import { NzIconModule } from 'ng-zorro-antd/icon';
import { ChatComponent } from '../../../ai/chat/chat.component';

/**
 * 学生端 AI 助手包装组件
 *
 * 设计：复用 ChatComponent 的所有对话能力（资源选择、问答流式输出），
 * 在顶部加一层"学习助手"标题栏，明确告诉学生这是学习场景的 AI。
 *
 * ChatComponent 本身没有角色判断逻辑（前后端权限已通过 KnowledgeHub.AI.Chat 限制），
 * 这里只做"包装"和"场景化"标识。
 */
@Component({
  selector: 'app-student-chat',
  standalone: true,
  imports: [CommonModule, NzIconModule, ChatComponent],
  templateUrl: './student-chat.component.html',
  styleUrls: ['./student-chat.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class StudentChatComponent {}
