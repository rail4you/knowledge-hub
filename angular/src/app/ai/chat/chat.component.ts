import { Component, signal, inject, ViewChild, ElementRef, ChangeDetectionStrategy, OnDestroy, OnInit, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { DomSanitizer, SafeHtml } from '@angular/platform-browser';
import { RestService } from '@abp/ng.core';
import { NzInputModule } from 'ng-zorro-antd/input';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzCardModule } from 'ng-zorro-antd/card';
import { NzSpinModule } from 'ng-zorro-antd/spin';
import { NzIconModule } from 'ng-zorro-antd/icon';
import { NzAvatarModule } from 'ng-zorro-antd/avatar';
import { NzTypographyModule } from 'ng-zorro-antd/typography';
import { NzEmptyModule } from 'ng-zorro-antd/empty';
import { NzListModule } from 'ng-zorro-antd/list';
import { NzTagModule } from 'ng-zorro-antd/tag';
import { NzDividerModule } from 'ng-zorro-antd/divider';
import { NzTreeModule, NzTreeNode, NzTreeNodeOptions } from 'ng-zorro-antd/tree';
import { NzTooltipModule } from 'ng-zorro-antd/tooltip';
import { NzPopoverModule } from 'ng-zorro-antd/popover';
import { Subject, takeUntil } from 'rxjs';
import { marked } from 'marked';
import { ChatService, ResourceForChat } from '../services/chat.service';
import { ResourceService } from '../../proxy/resources/resource.service';
import type { ResourceCategoryDto } from '../../proxy/resources/models';

interface ChatMessage {
  id: string;
  role: 'user' | 'assistant' | 'system';
  content: string;
  createdAt: Date;
}

interface SuggestionChip {
  icon: string;
  label: string;
  text: string;
}

/**
 * nz-tree 的数据节点类型：扩展官方 NzTreeNodeOptions，
 * 添加自定义 meta 用于在模板中标识资源叶子。
 * 注意：不能命名为 "origin"，因为 NzTreeNode.origin 已经指向 NzTreeNodeOptions。
 */
type CategoryTreeNode = NzTreeNodeOptions & {
  meta?: {
    resource?: ResourceForChat;
  };
};

@Component({
  selector: 'app-chat',
  standalone: true,
  imports: [
    CommonModule,
    NzInputModule,
    NzButtonModule,
    NzCardModule,
    NzSpinModule,
    NzIconModule,
    NzAvatarModule,
    NzTypographyModule,
    NzEmptyModule,
    NzListModule,
    NzTagModule,
    NzDividerModule,
    NzTreeModule,
    NzTooltipModule,
    NzPopoverModule
  ],
  templateUrl: './chat.component.html',
  styleUrls: ['./chat.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class ChatComponent implements OnInit, OnDestroy {
  private readonly chatService = inject(ChatService);
  private readonly resourceProxy = inject(ResourceService);
  private readonly restService = inject(RestService);
  private readonly sanitizer = inject(DomSanitizer);
  private readonly destroy$ = new Subject<void>();

  @ViewChild('messagesContainer') messagesContainer!: ElementRef;

  messages = signal<ChatMessage[]>([]);
  inputMessage = signal('');
  isLoading = signal(false);
  threadId = signal<string>('');
  resources = signal<ResourceForChat[]>([]);
  categories = signal<ResourceCategoryDto[]>([]);
  selectedResource = signal<ResourceForChat | null>(null);
  resourceSearchText = signal('');
  isResourcesLoading = signal(false);
  isCategoriesLoading = signal(false);

  // 提示按钮：分两组（文档模式 / 通用模式）
  readonly documentSuggestions: SuggestionChip[] = [
    { icon: '📝', label: '总结文档内容', text: '请帮我总结这份文档的主要内容' },
    { icon: '🔑', label: '提取关键点', text: '请提取这份文档的关键要点' },
    { icon: '❓', label: '文档讲了什么', text: '这份文档主要讲了什么？' },
  ];
  readonly generalSuggestions: SuggestionChip[] = [
    { icon: '💡', label: '你能做什么？', text: '你能做什么？请简单介绍一下你的功能。' },
    { icon: '🔍', label: '帮我搜文档', text: '帮我搜索知识库里的相关文档。' },
    { icon: '📚', label: '推荐学习路径', text: '请为我推荐一个学习路径。' }
  ];
  currentSuggestions = computed<SuggestionChip[]>(() =>
    this.selectedResource() ? this.documentSuggestions : this.generalSuggestions
  );

  /** 热门词 */
  hotWords = signal<{ word: string; frequency: number }[]>([]);
  isHotWordsLoading = signal(false);
  showHotWordsPopover = signal(false);

  /** 计算热门词字体大小（词云效果） */
  hotWordFontSize(freq: number): string {
    const words = this.hotWords();
    if (words.length === 0) return '12px';
    const maxFreq = Math.max(...words.map(w => w.frequency));
    const minFreq = Math.min(...words.map(w => w.frequency));
    const range = maxFreq - minFreq || 1;
    const normalized = (freq - minFreq) / range; // 0..1
    // 字体大小 14px ~ 28px
    const size = 14 + normalized * 14;
    return `${Math.round(size)}px`;
  }

  /** 加载当前选中文档的热门词 */
  loadHotWords(): void {
    const res = this.selectedResource();
    if (!res) return;

    this.isHotWordsLoading.set(true);
    this.showHotWordsPopover.set(true);

    this.restService.request<any, { word: string; frequency: number }[]>({
      method: 'GET',
      url: '/api/app/meili-search-admin/hot-words',
      params: { resourceId: res.id, count: 30 }
    }, { apiName: 'KnowledgeHub' }).subscribe({
      next: (data) => {
        this.hotWords.set(data ?? []);
        this.isHotWordsLoading.set(false);
      },
      error: () => {
        this.hotWords.set([]);
        this.isHotWordsLoading.set(false);
      }
    });
  }

  /** 点击热门词后自动搜索 */
  searchByHotWord(word: string): void {
    const res = this.selectedResource();
    if (!res) return;
    // 直接在文档中搜索该词
    this.inputMessage.set(`在文档中搜索关于"${word}"的内容`);
    this.sendMessage();
  }

  inputPlaceholder = computed(() =>
    this.selectedResource()
      ? `输入关于《${this.selectedResource()!.name}》的问题，按 Enter 发送...`
      : '输入您的问题，按 Enter 发送...'
  );
  welcomeTitle = computed(() =>
    this.selectedResource()
      ? `关于《${this.selectedResource()!.name}》的问答`
      : '你好，我是文档问答助手'
  );
  welcomeDescription = computed(() =>
    this.selectedResource()
      ? '请输入关于这份资源的问题'
      : '请从左侧选择一份资源，或直接开始提问'
  );

  filteredResources = computed(() => {
    const search = this.resourceSearchText().toLowerCase();
    if (!search) return this.resources();
    return this.resources().filter(r =>
      r.name.toLowerCase().includes(search)
    );
  });

  /** 树状节点数据：分类 + 资源叶子 */
  categoryTreeNodes = computed<NzTreeNodeOptions[]>(() => {
    const cats = this.categories();
    const res = this.filteredResources();

    // 1. 按 CategoryId 分组资源
    const byCategory = new Map<string, ResourceForChat[]>();
    const noCategory: ResourceForChat[] = [];
    for (const r of res) {
      if (r.categoryId) {
        const list = byCategory.get(r.categoryId) ?? [];
        list.push(r);
        byCategory.set(r.categoryId, list);
      } else {
        noCategory.push(r);
      }
    }

    // 2. 递归构造 nz-tree 节点
    const build = (nodes: ResourceCategoryDto[]): NzTreeNodeOptions[] =>
      nodes.map(c => ({
        title: c.name,
        key: 'cat-' + c.id,
        icon: 'folder',
        expanded: true,
        children: [
          ...build(c.children ?? []),
          ...(c.id ? (byCategory.get(c.id) ?? []) : []).map<NzTreeNodeOptions>(r => ({
            title: r.name,
            key: 'res-' + r.id,
            icon: this.getDocIcon(r.sourceFormat),
            isLeaf: true,
            expanded: false,
            meta: { resource: r }
          }))
        ]
      }));

    const roots = build(cats);

    // 3. 末尾追加"未分类"
    if (noCategory.length > 0) {
      roots.push({
        title: '未分类',
        key: 'cat-uncategorized',
        icon: 'inbox',
        expanded: true,
        children: noCategory.map<NzTreeNodeOptions>(r => ({
          title: r.name,
          key: 'res-' + r.id,
          icon: this.getDocIcon(r.sourceFormat),
          isLeaf: true,
          expanded: false,
          meta: { resource: r }
        }))
      });
    }

    return roots;
  });

  /** 根据扩展名返回对应的文件图标（与分类的 folder 图标明显区分） */
  getDocIcon(ext: string | null | undefined): string {
    const e = (ext ?? '').toLowerCase().replace(/^\./, '');
    switch (e) {
      case 'pdf': return 'file-pdf';
      case 'doc':
      case 'docx': return 'file-word';
      case 'ppt':
      case 'pptx': return 'file-ppt';
      case 'xls':
      case 'xlsx': return 'file-excel';
      case 'jpg':
      case 'jpeg':
      case 'png':
      case 'gif':
      case 'webp': return 'file-image';
      case 'mp4':
      case 'avi':
      case 'mov':
      case 'mkv': return 'video-camera';
      case 'mp3':
      case 'wav':
      case 'flac': return 'audio';
      case 'zip':
      case 'rar':
      case '7z': return 'file-zip';
      case 'txt':
      case 'md': return 'file-text';
      // 兜底：用 file-text（已确认 ng-zorro 默认图标集中存在），
      // 不使用 file-unknown 以免字体未加载导致空白
      default: return 'file-text';
    }
  }

  ngOnInit() {
    this.loadResources();
    this.loadCategories();
  }

  ngOnDestroy() {
    this.destroy$.next();
    this.destroy$.complete();
  }

  private loadResources() {
    this.isResourcesLoading.set(true);
    this.chatService.getResources()
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: (data) => {
          this.resources.set(data);
          this.isResourcesLoading.set(false);
        },
        error: (err) => {
          console.error('Failed to load resources:', err);
          this.isResourcesLoading.set(false);
        }
      });
  }

  private loadCategories() {
    this.isCategoriesLoading.set(true);
    this.resourceProxy.getCategories()
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: (data) => {
          this.categories.set(data ?? []);
          this.isCategoriesLoading.set(false);
        },
        error: (err) => {
          console.error('Failed to load categories:', err);
          this.categories.set([]);
          this.isCategoriesLoading.set(false);
        }
      });
  }

  selectResource(resource: ResourceForChat) {
    const current = this.selectedResource();
    if (current?.id === resource.id) {
      // Deselect
      this.selectedResource.set(null);
    } else {
      this.selectedResource.set(resource);
      // Clear chat when switching document
      this.messages.set([]);
      this.threadId.set('');
    }
  }

  /**
   * nz-tree 节点点击：分类无动作，仅资源叶子响应。
   * 事件回调中 node 是 NzTreeNode 实例，node.origin 是 NzTreeNodeOptions。
   * 自定义数据放在 meta 字段（避开 origin 命名冲突）。
   */
  onTreeClick(event: { node: NzTreeNode; event: MouseEvent }): void {
    const origin = event.node?.origin as NzTreeNodeOptions | undefined;
    if (!origin) return;
    const customMeta = origin['meta'] as { resource?: ResourceForChat } | undefined;
    const resource = customMeta?.resource;
    if (resource) {
      // 阻止事件冒泡（避免影响 nz-tree 的选中高亮逻辑）
      event.event.stopPropagation();
      this.selectResource(resource);
    }
  }

  /** 点击提示按钮：自动填入并发送 */
  onSuggestionClick(text: string): void {
    if (this.isLoading()) return;
    this.inputMessage.set(text);
    this.sendMessage();
  }

  newChat() {
    this.messages.set([]);
    this.threadId.set('');
    this.selectedResource.set(null);
  }

  sendMessage() {
    const content = this.inputMessage().trim();
    if (!content || this.isLoading()) return;

    const userMessage: ChatMessage = {
      id: crypto.randomUUID(),
      role: 'user',
      content,
      createdAt: new Date()
    };

    this.messages.update(msgs => [...msgs, userMessage]);
    this.inputMessage.set('');
    this.isLoading.set(true);
    this.scrollToBottom();

    const assistantMessage: ChatMessage = {
      id: crypto.randomUUID(),
      role: 'assistant',
      content: '',
      createdAt: new Date()
    };
    this.messages.update(msgs => [...msgs, assistantMessage]);

    const selectedRes = this.selectedResource();

    this.chatService.chat({
      message: content,
      threadId: this.threadId() || undefined,
      resourceId: selectedRes?.id,
    })
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: (chunk) => {
          if (chunk.content) {
            this.messages.update(msgs => {
              const lastMsg = msgs[msgs.length - 1];
              if (lastMsg && lastMsg.role === 'assistant') {
                return [...msgs.slice(0, -1), { ...lastMsg, content: lastMsg.content + chunk.content }];
              }
              return msgs;
            });
            this.scrollToBottom();
          }
          if (chunk.threadId && !this.threadId()) {
            this.threadId.set(chunk.threadId);
          }
        },
        error: (err) => {
          console.error('Chat error:', err);
          this.isLoading.set(false);
          this.messages.update(msgs => {
            const lastMsg = msgs[msgs.length - 1];
            if (lastMsg && lastMsg.role === 'assistant') {
              return [...msgs.slice(0, -1), { ...lastMsg, content: '抱歉，发生了错误，请稍后重试。' }];
            }
            return msgs;
          });
        },
        complete: () => {
          this.isLoading.set(false);
        }
      });
  }

  onKeyPress(event: KeyboardEvent) {
    if (event.key === 'Enter' && !event.shiftKey) {
      event.preventDefault();
      this.sendMessage();
    }
  }

  private scrollToBottom() {
    setTimeout(() => {
      const el = this.messagesContainer?.nativeElement;
      if (el) {
        el.scrollTop = el.scrollHeight;
      }
    }, 0);
  }

  formatMessage(content: string): SafeHtml {
    if (!content) return '';
    const cleaned = this.sanitizeAssistantContent(content);
    const html = marked.parse(cleaned, { async: false }) as string;
    return this.sanitizer.bypassSecurityTrustHtml(html);
  }

  private sanitizeAssistantContent(content: string): string {
    let cleaned = content;

    cleaned = cleaned.replace(/<think\b[^>]*>[\s\S]*?<\/think>/gi, '');
    cleaned = cleaned.replace(/<thinking\b[^>]*>[\s\S]*?<\/thinking>/gi, '');
    cleaned = cleaned.replace(/<details\b[^>]*>\s*<summary\b[^>]*>\s*(?:思考|推理|工具|检索|thinking|reasoning|tool)[\s\S]*?<\/details>/gi, '');
    cleaned = cleaned.replace(/```(?:think|thinking|thought|reasoning|tool|tool_call|tool_result)[\s\S]*?```/gi, '');

    // Hide unfinished reasoning/tool blocks while streaming.
    cleaned = cleaned.replace(/<think\b[^>]*>[\s\S]*$/gi, '');
    cleaned = cleaned.replace(/<thinking\b[^>]*>[\s\S]*$/gi, '');
    cleaned = cleaned.replace(/<details\b[^>]*>\s*<summary\b[^>]*>\s*(?:思考|推理|工具|检索|thinking|reasoning|tool)[\s\S]*$/gi, '');
    cleaned = cleaned.replace(/```(?:think|thinking|thought|reasoning|tool|tool_call|tool_result)[\s\S]*$/gi, '');

    cleaned = cleaned
      .split('\n')
      .filter(line => !this.isAuxiliaryLine(line))
      .join('\n');

    cleaned = cleaned.replace(/\n{3,}/g, '\n\n').trim();
    return cleaned || '正在整理答案...';
  }

  private isAuxiliaryLine(line: string): boolean {
    const normalized = line.trim();
    if (!normalized) return false;

    const auxiliaryPatterns = [
      /^思考(过程|如下)?[:：]?$/i,
      /^推理(过程|如下)?[:：]?$/i,
      /^工具(调用|使用|返回|结果)?[:：]?$/i,
      /^检索(过程|步骤|结果)?[:：]?$/i,
      /^调用工具[:：]?/i,
      /^正在调用[:：]?/i,
      /^已(?:调用|检索|搜索|读取)[:：]?/i,
      /^我先(调用|搜索|查看|读取|检索)/i,
      /^让我先(调用|搜索|查看|读取|检索)/i,
      /^先调用\s+/i,
      /^\[?(?:tool|reasoning|thinking)[_\s-]?(?:call|result|step)?\]?[:：]?/i,
      /^SearchPageIndex\b/i,
      /^get_document\b/i,
      /^get_page_content\b/i,
      /^tool[_\s-]?call/i,
      /^tool[_\s-]?result/i,
      /^reasoning[:：]?/i,
      /^thinking[:：]?/i,
    ];

    return auxiliaryPatterns.some(pattern => pattern.test(normalized));
  }
}
