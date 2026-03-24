import { Component, signal, inject, ChangeDetectionStrategy } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { NzInputModule } from 'ng-zorro-antd/input';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzCardModule } from 'ng-zorro-antd/card';
import { NzUploadModule, NzUploadChangeParam } from 'ng-zorro-antd/upload';
import { NzSpinModule } from 'ng-zorro-antd/spin';
import { NzIconModule } from 'ng-zorro-antd/icon';
import { NzDividerModule } from 'ng-zorro-antd/divider';
import { NzCollapseModule } from 'ng-zorro-antd/collapse';
import { NzDescriptionsModule } from 'ng-zorro-antd/descriptions';
import { NzTagModule } from 'ng-zorro-antd/tag';
import { NzSkeletonModule } from 'ng-zorro-antd/skeleton';
import { NzMessageService } from 'ng-zorro-antd/message';
import { ChatService } from '../services/chat.service';

interface CaseInput {
  content: string;
  title?: string;
}

interface CaseAnalysisResult {
  title: string;
  summary: string;
  background: {
    industry: string;
    timeframe: string;
    context: string;
    stakeholders: string[];
  };
  keyIssues: {
    id: string;
    title: string;
    description: string;
    impact: string;
    severity: string;
  }[];
  solutions: {
    id: string;
    title: string;
    description: string;
    steps: string[];
    expectedOutcome: string;
  }[];
  keyInsights: string[];
  recommendations: string[];
}

@Component({
  selector: 'app-case-analysis',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    NzInputModule,
    NzButtonModule,
    NzCardModule,
    NzUploadModule,
    NzSpinModule,
    NzIconModule,
    NzDividerModule,
    NzCollapseModule,
    NzDescriptionsModule,
    NzTagModule,
    NzSkeletonModule
  ],
  templateUrl: './case-analysis.component.html',
  styleUrls: ['./case-analysis.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class CaseAnalysisComponent {
  private readonly chatService = inject(ChatService);
  private readonly message = inject(NzMessageService);
  
  input = signal<CaseInput>({ content: '', title: '' });
  result = signal<CaseAnalysisResult | null>(null);
  isLoading = signal(false);
  
  beforeUpload = (file: File): boolean => {
    const isValid = file.size / 1024 / 1024 < 10;
    if (!isValid) {
      this.message.error('文件大小不能超过10MB');
    }
    return isValid;
  };
  
  handleUpload = (info: NzUploadChangeParam): void => {
    const file = info.file;
    if (!file || !file.originFileObj) return;
    if (!this.beforeUpload(file.originFileObj)) return;
    
    const reader = new FileReader();
    reader.onload = () => {
      this.input.update(input => ({
        ...input,
        content: reader.result as string,
        title: file.name
      }));
      this.message.success(`已加载文件: ${file.name}`);
    };
    reader.readAsText(file.originFileObj);
  };
  
  analyze(): void {
    const input = this.input();
    if (!input.content.trim()) {
      this.message.warning('请先上传案例文档或输入案例内容');
      return;
    }
    
    this.isLoading.set(true);
    this.result.set(null);
    
    const prompt = this.buildPrompt();
    let fullResponse = '';
    
    this.chatService.chat({ message: prompt, agent: 'case-analysis' }).subscribe({
      next: (chunk) => {
        if (chunk.content) {
          fullResponse += chunk.content;
          this.tryParseResult(fullResponse);
        }
      },
      error: (err) => {
        console.error('Error analyzing case:', err);
        this.message.error('分析失败，请重试');
        this.isLoading.set(false);
      },
      complete: () => {
        this.isLoading.set(false);
      }
    });
  }
  
  private tryParseResult(response: string): void {
    try {
      const jsonMatch = response.match(/\{[\s\S]*\}/);
      if (jsonMatch) {
        const parsed = JSON.parse(jsonMatch[0]) as CaseAnalysisResult;
        this.result.set(parsed);
      }
    } catch {
      // JSON not yet complete
    }
  }
  
  private buildPrompt(): string {
    const input = this.input();
    return `请分析以下案例，提供多维度分析：

案例标题: ${input.title || '未命名案例'}

案例内容:
${input.content}

请以JSON格式输出分析结果，包含以下字段:
{
  "title": "案例标题",
  "summary": "案例摘要(100字以内)",
  "background": {
    "industry": "所属行业",
    "timeframe": "时间范围",
    "context": "背景描述",
    "stakeholders": ["相关方1", "相关方2"]
  },
  "keyIssues": [
    {"id": "1", "title": "问题标题", "description": "问题描述", "impact": "影响", "severity": "高/中/低"}
  ],
  "solutions": [
    {"id": "1", "title": "方案标题", "description": "方案描述", "steps": ["步骤1"], "expectedOutcome": "预期效果"}
  ],
  "keyInsights": ["洞察1", "洞察2"],
  "recommendations": ["建议1", "建议2"]
}

只输出JSON，不要其他内容。`;
  }
  
  reset(): void {
    this.input.set({ content: '', title: '' });
    this.result.set(null);
  }
  
  getSeverityColor(severity: string): string {
    switch (severity) {
      case '高': return 'red';
      case '中': return 'orange';
      case '低': return 'green';
      default: return 'default';
    }
  }
}
