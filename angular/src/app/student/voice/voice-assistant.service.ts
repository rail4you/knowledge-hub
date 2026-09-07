import { Injectable, NgZone, inject, signal } from '@angular/core';
import { Router } from '@angular/router';
import { ChatService } from '../../ai/services/chat.service';
import { VoiceContextService } from './voice-context.service';

/**
 * 语音助手 MVP（只读）。
 *
 * 能力：
 * - STT：浏览器原生 SpeechRecognition（Chrome/Edge 可用，zh-CN），单次识别；
 *   不可用时自动降级为文本输入。
 * - TTS：浏览器原生 speechSynthesis，中文音色，分段队列，可打断。
 * - 意图：本地规则（导航/总结/朗读/帮助）→ 兜底调现有 AI Chat SSE（总结/本页问答）。
 * - 只读白名单：仅 /student/courses、/student/courses/:id、
 *   /student/courses/:id/learn(/:chapterId) 三类路由之间跳转，不做任何写操作。
 */
export interface VoiceSubtitle {
  role: 'user' | 'assistant' | 'system';
  text: string;
}

type AssistantState = 'idle' | 'listening' | 'thinking' | 'speaking';

const CN_NUM: Record<string, number> = {
  一: 1, 二: 2, 三: 3, 四: 4, 五: 5, 六: 6, 七: 7, 八: 8, 九: 9, 十: 10,
  两: 2, 首: 1, 第一: 1,
};

@Injectable({ providedIn: 'root' })
export class VoiceAssistantService {
  private readonly router = inject(Router);
  private readonly ngZone = inject(NgZone);
  private readonly chatService = inject(ChatService);
  private readonly contextService = inject(VoiceContextService);

  /** 面板是否打开 */
  readonly isOpen = signal(false);
  /** 当前状态机 */
  readonly state = signal<AssistantState>('idle');
  /** 浏览器是否支持语音识别 */
  readonly sttSupported = signal(this.detectSttSupport());
  /** 浏览器是否支持语音合成 */
  readonly ttsSupported = signal(this.detectTtsSupport());
  /** 识别中的中间文本 */
  readonly interimText = signal('');
  /** 字幕历史（同步显示 + aria-live 朗读区） */
  readonly subtitles = signal<VoiceSubtitle[]>([]);
  /** 错误提示（麦克风被拒等） */
  readonly error = signal<string | null>(null);
  /** 语速 */
  readonly rate = signal(1);

  /** 最近一次 AI 回复全文（用于重播） */
  private lastReply = '';
  private recognition: any = null;
  private readonly subtitleCap = 50;

  // ── 面板 ──────────────────────────────

  setOpen(open: boolean): void {
    this.isOpen.set(open);
    if (open && this.subtitles().length === 0) {
      this.pushSubtitle('system', '语音助手已开启。点“语音输入”按钮或按 Ctrl+Enter 开始说话，识别结果会填入输入框，按回车确认发送。');
    }
    // 受控模式：打开面板不自动开麦（点按钮或按 Ctrl+Enter 才开始听）；
    // 关闭面板 = 麦克风与播报全停
    if (!open) {
      this.pendingExit = false;
      this.stopVoiceInput();
      this.stopSpeaking();
    }
  }

  toggleOpen(): void {
    this.setOpen(!this.isOpen());
  }

  setRate(rate: number): void {
    this.rate.set(rate);
  }

  // ── STT ───────────────────────────────

  private detectSttSupport(): boolean {
    if (typeof window === 'undefined') return false;
    const w = window as any;
    return !!(w.SpeechRecognition || w.webkitSpeechRecognition);
  }

  private detectTtsSupport(): boolean {
    if (typeof window === 'undefined') return false;
    return 'speechSynthesis' in window;
  }

  /** 受控语音输入循环已移除，改为单次识别（见 startVoiceInput）。 */
  /** 输入框草稿：语音识别结果先落到这里，回车确认后才发送 */
  readonly draft = signal('');
  /** 对话轮次：打断时递增，让旧 AI 回复失效 */
  private turn = 0;
  /** 用户提前按了回车：识别结果一到就自动发送，不用再按第二次 */
  private sendOnResult = false;
  /** 退出确认播报结束后自动关面板 */
  private pendingExit = false;
  /** TTS 代际令牌：打断旧播报 */
  private speechEpoch = 0;
  /** 最近播报的文本（归一化），用于过滤麦克风收到的自身回声 */
  private recentSpoken: string[] = [];

  /**
   * 受控语音输入：点按钮或按 Ctrl+Enter 触发，单次识别。
   * 若正在播报/等待 AI，先打断（作废旧播报与旧回复）再开始听。
   */
  startVoiceInput(): void {
    this.error.set(null);
    if (!this.sttSupported()) {
      this.error.set('当前浏览器不支持语音识别，请用 Chrome 或 Edge，或直接打字后按回车发送。');
      return;
    }
    // 打断：旧播报链与等待中的 AI 回复一律作废
    this.speechEpoch++;
    this.turn++;
    this.sendOnResult = false;
    try {
      if (this.ttsSupported()) speechSynthesis.cancel();
    } catch {
      // 忽略取消异常
    }
    this.startRec();
    // 焦点先落到输入框：用户随后按的回车一定能进表单（不再被吞）
    this.focusDraftInput();
  }

  stopVoiceInput(): void {
    this.sendOnResult = false;
    try {
      this.recognition?.abort?.();
    } catch {
      // 忽略停止异常
    }
    this.recognition = null;
    this.interimText.set('');
    if (this.state() === 'listening') this.state.set('idle');
  }

  private startRec(): void {
    if (!this.isOpen() || !this.sttSupported()) return;
    try {
      this.recognition?.abort?.();
    } catch {
      // 忽略旧实例清理异常
    }
    this.recognition = null;
    const w = window as any;
    const Ctor = w.SpeechRecognition || w.webkitSpeechRecognition;
    if (!Ctor) return;
    const rec = new Ctor();
    this.recognition = rec;
    rec.lang = 'zh-CN';
    rec.interimResults = true;
    rec.continuous = false; // 单次识别：说完即停，结果进草稿，回车确认才发送
    rec.maxAlternatives = 1;

    rec.onresult = (event: any) => {
      let interim = '';
      const finals: string[] = [];
      for (let i = event.resultIndex; i < event.results.length; i++) {
        const res = event.results[i];
        const t = (res[0]?.transcript || '').trim();
        if (!t) continue;
        if (res.isFinal) finals.push(t);
        else interim += t;
      }
      this.ngZone.run(() => {
        this.interimText.set(interim);
        for (const f of finals) {
          // 回声过滤：播报声被麦克风收回时直接忽略
          if (this.isEcho(f)) continue;
          this.interimText.set('');
          if (this.sendOnResult) {
            // 用户提前按过回车：不等二次确认，直接发送
            this.sendOnResult = false;
            this.draft.set('');
            this.pushSubtitle('system', '识别完成，已自动发送。');
            this.handleText(f);
          } else {
            // 受控模式：识别结果先入草稿，回车确认后才发送
            this.draft.set(f);
            this.pushSubtitle('system', `识别到：“${f}”，按回车发送，或重新说话覆盖。`);
            this.focusDraftInput();
          }
        }
      });
    };
    rec.onerror = (event: any) => {
      this.ngZone.run(() => {
        const code = event?.error;
        if (code === 'aborted') return; // 主动中止，不算错
        if (this.state() === 'listening') this.state.set('idle');
        this.interimText.set('');
        this.sendOnResult = false; // 识别失败，预存的回车作废
        if (code === 'no-speech') {
          this.error.set('没听清，请再按一次语音输入说一次，或直接打字后按回车。');
        } else {
          this.error.set(this.mapRecError(code));
        }
      });
    };
    rec.onend = () => {
      this.ngZone.run(() => {
        this.recognition = null;
        if (this.state() === 'listening') this.state.set('idle');
      });
    };
    try {
      rec.start();
      this.state.set('listening');
    } catch {
      this.recognition = null;
      this.state.set('idle');
      this.error.set('麦克风启动失败，请检查浏览器麦克风权限，或直接打字后按回车。');
    }
  }

  /** 是否是自己播报声被麦克风收回的回声（仅播报中生效）。 */
  private isEcho(text: string): boolean {
    if (this.state() !== 'speaking') return false;
    const n = this.normSpeech(text);
    if (!n) return true;
    return this.recentSpoken.slice(-6).some(sp => sp.includes(n) || (n.length >= 6 && sp.includes(n.slice(0, 8))));
  }

  private normSpeech(s: string): string {
    return (s || '').replace(/[\s，。！？；：、,.!?;:\-—…·'"“”‘’（）()]/g, '');
  }

  private mapRecError(code: string): string {
    switch (code) {
      case 'not-allowed':
      case 'service-not-allowed':
        return '麦克风权限被拒绝。请点击浏览器地址栏的麦克风图标允许访问，或直接打字。';
      case 'no-speech':
        return '没听清，请靠近麦克风再说一次，或直接打字。';
      case 'audio-capture':
        return '找不到麦克风设备，请检查设备连接，或直接打字。';
      case 'network':
        return '语音识别需要联网（走浏览器厂商公网服务），请检查网络后重试，或直接打字。';
      default:
        return '语音识别遇到问题，请再试一次，或直接打字。';
    }
  }

  // ── TTS ───────────────────────────────

  speak(text: string): void {
    const plain = this.toSpeechText(text);
    if (!plain) return;
    if (!this.ttsSupported()) {
      this.error.set('当前浏览器不支持语音播报，已显示文字版。');
      this.finishSpeakTurn();
      return;
    }
    // 打断上一轮播报（新 epoch 让旧 utterance 回调失效），识别保持运行以支持随时打断
    const epoch = ++this.speechEpoch;
    try {
      speechSynthesis.cancel();
    } catch {
      // 忽略取消异常
    }
    const chunks = this.splitForSpeech(plain);
    if (chunks.length === 0) {
      this.finishSpeakTurn();
      return;
    }
    this.state.set('speaking');
    this.recentSpoken.push(...chunks.map(c => this.normSpeech(c)).filter(Boolean));
    this.recentSpoken = this.recentSpoken.slice(-10);
    let index = 0;
    const voices = speechSynthesis.getVoices();
    const zhVoice = voices.find(v => (v.lang || '').toLowerCase().startsWith('zh')) || null;
    const playNext = () => {
      if (epoch !== this.speechEpoch) return; // 已被新一轮打断
      if (index >= chunks.length) {
        this.ngZone.run(() => {
          if (this.state() === 'speaking') this.state.set('listening');
          this.finishSpeakTurn();
        });
        return;
      }
      const utter = new SpeechSynthesisUtterance(chunks[index]);
      utter.lang = 'zh-CN';
      utter.rate = this.rate();
      if (zhVoice) utter.voice = zhVoice;
      utter.onend = () => {
        if (epoch !== this.speechEpoch) return;
        index++;
        playNext();
      };
      utter.onerror = () => {
        if (epoch !== this.speechEpoch) return;
        index++;
        playNext();
      };
      speechSynthesis.speak(utter);
    };
    playNext();
  }

  /** 一轮播报真正结束后的收尾：退出确认则关面板。 */
  private finishSpeakTurn(): void {
    if (this.pendingExit) {
      this.pendingExit = false;
      this.setOpen(false);
    }
  }

  stopSpeaking(): void {
    this.speechEpoch++; // 作废进行中的播报链
    try {
      if (this.ttsSupported()) speechSynthesis.cancel();
    } catch {
      // 忽略取消异常
    }
    if (this.state() === 'speaking') this.state.set('listening');
  }

  /** “停止/退出”：播报一句确认后自动退出助手（关面板、停麦克风）。 */
  exitAssistant(): void {
    this.pushSubtitle('assistant', '好的，已退出语音助手。');
    if (!this.ttsSupported()) {
      this.setOpen(false);
      return;
    }
    this.pendingExit = true;
    this.speak('好的，已退出语音助手。');
  }

  replay(): void {
    if (this.lastReply) this.speak(this.lastReply);
  }

  /** 去掉 Markdown/HTML，截断到适合播报的长度。 */
  private toSpeechText(text: string): string {
    let t = (text || '').replace(/```[\s\S]*?```/g, ' ');
    t = t.replace(/<[^>]+>/g, ' ');
    t = t.replace(/[#*>`_~|[\]()]/g, '');
    t = t.replace(/\s+/g, ' ').trim();
    if (t.length > 1200) t = t.slice(0, 1200) + '。以下省略。';
    return t;
  }

  private splitForSpeech(text: string): string[] {
    const parts = text.split(/([。！？；\n]+)/).filter(s => s.trim().length > 0);
    const chunks: string[] = [];
    let current = '';
    for (const part of parts) {
      if ((current + part).length > 220) {
        if (current.trim()) chunks.push(current.trim());
        current = part;
      } else {
        current += part;
      }
    }
    if (current.trim()) chunks.push(current.trim());
    return chunks;
  }

  // ── 入口：统一处理识别文本与打字文本 ────

  handleText(raw: string): void {
    const text = (raw || '').trim();
    if (!text) return;
    this.error.set(null);
    // 打断：新指令到达时先停掉正在播报的声音
    if (this.state() === 'speaking') this.stopSpeaking();
    this.pushSubtitle('user', text);
    const myTurn = ++this.turn;
    const handled = this.routeLocalCommand(text);
    if (!handled) this.askAi(text, myTurn);
  }

  /** 回车确认：有草稿就发送；草稿为空但正在聆听，说明用户提前按了回车，预存一次 */
  sendDraft(): void {
    const text = this.draft().trim();
    if (text) {
      if (this.state() === 'thinking') return;
      this.draft.set('');
      this.handleText(text);
      return;
    }
    if (this.state() === 'listening' && this.recognition) {
      this.sendOnResult = true;
      this.pushSubtitle('system', '好的，识别完成后会自动发送。');
    }
    // 草稿为空且没在听：静默忽略（和原来禁用按钮效果一致）
  }

  /** 识别完成后把焦点放回输入框，方便直接回车确认（读屏器用户可用方向键复核内容）。 */
  private focusDraftInput(): void {
    setTimeout(() => {
      if (!this.isOpen()) return;
      try {
        document.getElementById('voice-text-input')?.focus({ preventScroll: true });
      } catch {
        // 忽略聚焦异常
      }
    }, 50);
  }

  private pushSubtitle(role: VoiceSubtitle['role'], text: string): void {
    this.subtitles.update(list => [...list.slice(-this.subtitleCap + 1), { role, text }]);
  }

  // ── 本地意图（零延迟、离线可用） ────────

  /** 返回 true 表示已本地处理，不再调 AI。 */
  private routeLocalCommand(text: string): boolean {
    // 退出指令（最高优先）：说“停止”即退出整个助手
    if (/^(停|停止|退出|关闭|结束)(吧|了)?$/.test(text)
      || /^(关闭|退出)(语音)?助手$/.test(text)
      || text === '别说了' || text === '不要说了') {
      this.exitAssistant();
      return true;
    }
    if (/(帮助|你能做什|怎么用|如何使用)/.test(text)) {
      const help = '麦克风一直开着，直接说话就行，不用等我说完，随时可以打断我。试试说“总结本页”。说“停止”可以退出助手。';
      this.pushSubtitle('assistant', help);
      this.speak(help);
      return true;
    }
    // 仅停播报（不出助手）：别说 / 闭嘴 / 安静 / 停止播报
    if (/(别说|闭嘴|安静|暂停播报|停止播报)/.test(text)) {
      this.stopSpeaking();
      this.pushSubtitle('assistant', '已停止播报，还在听，你继续说。');
      return true;
    }
    if (/(重播|再说一遍|再念一遍)/.test(text)) {
      if (this.lastReply) this.speak(this.lastReply);
      else this.speak('还没有可重播的内容。');
      return true;
    }
    // 朗读（本地直接播，不调 AI，省 token）
    if (/(朗读|读一下|念一下|读出来|全文朗读)/.test(text)) {
      this.readAloud();
      return true;
    }
    // 总结（调 AI）
    if (/(总结|概括|讲了什|介绍一下|本页是什|这页是什)/.test(text)) {
      this.summarizePage();
      return true;
    }
    // 导航意图一律本地处理：能跳则跳，不能跳就如实说明，绝不交给 AI
    // （否则 AI 会编造“已跳转成功”，但页面实际没动）。
    if (this.looksLikeNavigation(text)) {
      const navTarget = this.parseNavigation(text);
      if (navTarget && navTarget.kind !== 'unsupported') {
        this.navigateVoice(navTarget);
      } else if (navTarget == null) {
        const tip = '没有找到这个页面。首版语音助手只支持课程中心、课程详情和章节学习三个页面之间跳转。';
        this.pushSubtitle('assistant', tip);
        this.speak(tip);
      }
      // unsupported 分支已在 parseNavigation 内提示过，不再重复
      return true;
    }
    return false;
  }

  /** 宽泛的导航意图判断：宁可误判为导航（如实拒绝），也不让 AI 编造“已跳转”。 */
  private looksLikeNavigation(text: string): boolean {
    if (/(跳转|跳到|打开|进入|前往|带我|个人中心|首页|主页|返回|回去|上一页)/.test(text)) return true;
    if (/去.{0,6}(课程|学习|页面|详情|中心|首页|那里)/.test(text)) return true;
    return false;
  }

  private parseNavigation(text: string): { kind: 'courses' | 'detail' | 'learn' | 'back' | 'unsupported'; index?: number } | null {
    const snap = this.contextService.snapshot(this.router.url);
    // 返回 / 上一页
    if (/(返回|回去|上一页)/.test(text)) {
      if (this.router.url.includes('/learn')) return { kind: 'detail' };
      if (/\/courses\/[^/]+/.test(this.router.url)) return { kind: 'courses' };
      return { kind: 'courses' };
    }
    // 学习页
    if (/(开始学习|继续学习|进入学习|去学习|章节学习)/.test(text)) {
      return snap.courseId ? { kind: 'learn' } : { kind: 'courses' };
    }
    // 详情页（在课程中心说“打开第X门”）
    const index = this.parseIndex(text);
    if ((/(打开|进入|查看|详情)/.test(text)) && index != null) {
      return { kind: 'detail', index };
    }
    if (/(课程详情|本课详情)/.test(text)) {
      return snap.courseId ? { kind: 'detail' } : null;
    }
    // 课程中心
    if (/(课程中心|课程列表|全部课程|我的课程|跳转.*课程|打开.*课程|去.*课程)/.test(text)) {
      return { kind: 'courses' };
    }
    // 超出 MVP 的页面：明确拒绝，只提示不跳转（保持只读小批量）
    if (/(资源库|收藏|我的学习|新闻|实训|就业|微专业|搜索|AI助手|个人中心|首页|主页|设置|资料|账户)/.test(text)) {
      const tip = '这个页面首版语音助手还去不了，只支持课程中心、课程详情和章节学习三个页面，其他页面请用顶部导航栏进入。';
      this.pushSubtitle('assistant', tip);
      this.speak(tip);
      return { kind: 'unsupported' };
    }
    return null;
  }

  private parseIndex(text: string): number | null {
    const m = text.match(/第\s*([0-9一二三四五六七八九十两]+)\s*[门个]/);
    if (m) {
      const raw = m[1];
      if (/^[0-9]+$/.test(raw)) return parseInt(raw, 10);
      if (CN_NUM[raw] != null) return CN_NUM[raw];
      if (raw === '十') return 10;
    }
    if (/(第一|首门|首个)/.test(text)) return 1;
    return null;
  }

  private navigateVoice(target: { kind: 'courses' | 'detail' | 'learn' | 'back' | 'unsupported'; index?: number }): void {
    if (target.kind === 'unsupported') return; // 已在别处提示，不跳转
    const snap = this.contextService.snapshot(this.router.url);
    let path: string[] | null = null;
    let label = '';
    if (target.kind === 'courses' || target.kind === 'back') {
      path = ['/student/courses'];
      label = '课程中心';
    } else if (target.kind === 'detail') {
      if (target.index != null && snap.items.length > 0) {
        const item = snap.items[target.index - 1];
        if (!item) {
          const tip = `当前列表共${snap.items.length}门，没有第${target.index}门。`;
          this.pushSubtitle('assistant', tip);
          this.speak(tip);
          return;
        }
        path = ['/student/courses', item.id];
        label = `课程《${item.title}》的详情页`;
      } else if (snap.courseId) {
        path = ['/student/courses', snap.courseId];
        label = '本课程的详情页';
      } else {
        const tip = '当前页没有可进入的课程，请先到课程中心。';
        this.pushSubtitle('assistant', tip);
        this.speak(tip);
        return;
      }
    } else if (target.kind === 'learn') {
      if (snap.courseId) {
        path = snap.chapterId
          ? ['/student/courses', snap.courseId, 'learn', snap.chapterId]
          : ['/student/courses', snap.courseId, 'learn'];
        label = '章节学习页';
      } else {
        const tip = '请先进入一门课程，我才能带你去学习页。';
        this.pushSubtitle('assistant', tip);
        this.speak(tip);
        return;
      }
    }
    if (!path) return;
    this.router.navigate(path).then(ok => {
      this.ngZone.run(() => {
        if (ok) {
          const msg = `已跳转到${label}。`;
          this.pushSubtitle('assistant', msg);
          this.speak(msg);
          this.focusMainHeading();
        } else {
          const msg = '跳转失败，请重试。';
          this.pushSubtitle('assistant', msg);
          this.speak(msg);
        }
      });
    });
  }

  /** 跳转后把焦点移到新页 h1，读屏器与键盘用户可感知。 */
  private focusMainHeading(): void {
    setTimeout(() => {
      const el = document.querySelector<HTMLElement>(
        'main h1, .learn-course-title, .detail-info__title, .student-hero__title, h1'
      );
      if (el) {
        if (!el.hasAttribute('tabindex')) el.setAttribute('tabindex', '-1');
        try {
          el.focus({ preventScroll: false });
        } catch {
          // 忽略聚焦异常
        }
      }
    }, 350);
  }

  // ── AI 兜底（总结 / 本页问答，共用 Chat SSE） ──

  private summarizePage(): void {
    const snap = this.contextService.snapshot(this.router.url);
    const prompt =
      `你是视障学生的语音助手。请用口语化中文、150字以内总结下面这个页面，不要用 Markdown 表格和代码块。` +
      `注意：你没有操作浏览器页面的能力，绝不要声称已经完成跳转、打开或操作了页面。\n` +
      `【路由】${snap.route}\n【标题】${snap.title}\n【内容】${snap.summary.slice(0, 3000)}`;
    this.callChat(prompt, '正在总结本页内容，请稍候。', this.turn);
  }

  private readAloud(): void {
    const snap = this.contextService.snapshot(this.router.url);
    let text = `${snap.title}。${snap.summary}`;
    if (snap.items.length > 0) {
      const list = snap.items
        .slice(0, 10)
        .map((it, i) => `第${i + 1}门，${it.title}${it.extra ? `，${it.extra}` : ''}。`)
        .join('');
      text += `列表内容：${list}`;
    }
    const plain = this.toSpeechText(text).slice(0, 1200);
    this.lastReply = plain;
    this.pushSubtitle('assistant', plain);
    this.speak(plain);
  }

  private askAi(question: string, myTurn: number = this.turn): void {
    const snap = this.contextService.snapshot(this.router.url);
    const prompt =
      `你是视障学生的语音助手。用户在页面【${snap.title}（${snap.route}）】上提问，请结合下面的页面内容，用口语化中文、200字以内回答，不要用 Markdown 表格和代码块。` +
      `注意：你没有操作浏览器页面的能力，绝不要声称已经完成跳转、打开或操作了页面。\n` +
      `【页面内容】${snap.summary.slice(0, 3000)}\n【用户问题】${question}`;
    this.callChat(prompt, undefined, myTurn);
  }

  private callChat(prompt: string, waitingText?: string, myTurn: number = this.turn): void {
    this.state.set('thinking');
    if (waitingText) this.pushSubtitle('assistant', waitingText);
    let full = '';
    this.chatService.chat({ message: prompt }).subscribe({
      next: chunk => {
        if (myTurn !== this.turn) return; // 已被新指令打断，丢弃旧回复
        if (chunk?.content) full += chunk.content;
      },
      error: () => {
        this.ngZone.run(() => {
          if (myTurn !== this.turn) return;
          this.state.set('idle');
          const msg = 'AI 服务暂时不可用，请稍后重试。';
          this.pushSubtitle('assistant', msg);
          this.speak(msg);
        });
      },
      complete: () => {
        this.ngZone.run(() => {
          // 中途被打断（新指令/退出/关面板）：迟到的回答不再播报
          if (myTurn !== this.turn || this.pendingExit || !this.isOpen()) return;
          this.state.set('idle');
          const reply = full.trim() || '没有拿到回答，请换个问法试试。';
          this.lastReply = reply;
          // 替换掉“正在总结…”占位
          this.subtitles.update(list => {
            const next = [...list];
            if (waitingText && next.length > 0 && next[next.length - 1].text === waitingText) {
              next[next.length - 1] = { role: 'assistant', text: reply };
            } else {
              next.push({ role: 'assistant', text: reply });
            }
            return next.slice(-this.subtitleCap);
          });
          this.speak(reply);
        });
      },
    });
  }
}
