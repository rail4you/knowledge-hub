import { ChangeDetectionStrategy, Component, HostListener, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzIconModule } from 'ng-zorro-antd/icon';
import { VoiceAssistantService } from './voice-assistant.service';

/**
 * 学生端语音助手：悬浮按钮 + 面板。
 * 全键盘可达：按钮有 aria-label/aria-pressed，字幕区 aria-live，Esc 停止播报，Alt+V 开关。
 */
@Component({
  selector: 'app-voice-panel',
  standalone: true,
  imports: [CommonModule, FormsModule, NzButtonModule, NzIconModule],
  templateUrl: './voice-panel.component.html',
  styleUrls: ['./voice-panel.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class VoicePanelComponent {
  protected readonly assistant = inject(VoiceAssistantService);

  @HostListener('window:keydown', ['$event'])
  onKeydown(event: KeyboardEvent): void {
    const e = event as KeyboardEvent;
    // Ctrl+Enter（Mac 上 Cmd+Enter）：开始语音输入；播报中按则打断并开始听
    if ((e.ctrlKey || e.metaKey) && e.key === 'Enter') {
      e.preventDefault();
      if (!this.assistant.isOpen()) this.assistant.setOpen(true);
      this.assistant.startVoiceInput();
      return;
    }
    // Alt+V 开关面板（避开输入框内触发）
    if (e.altKey && (e.key === 'v' || e.key === 'V')) {
      const target = e.target as HTMLElement | null;
      if (target && (target.tagName === 'INPUT' || target.tagName === 'TEXTAREA')) return;
      e.preventDefault();
      this.assistant.toggleOpen();
    }
    if (e.key === 'Escape' && this.assistant.isOpen()) {
      this.assistant.stopSpeaking();
    }
  }

  protected toggle(): void {
    this.assistant.toggleOpen();
  }

  protected voice(): void {
    this.assistant.startVoiceInput();
  }

  protected stop(): void {
    this.assistant.stopSpeaking();
  }

  protected send(): void {
    this.assistant.sendDraft();
  }

  protected onRateChange(event: Event): void {
    const value = Number((event.target as HTMLSelectElement).value);
    if (Number.isFinite(value)) this.assistant.setRate(value);
  }
}
