import { Injectable } from '@angular/core';

export interface SseChunk {
  content: string;
  threadId: string;
  isComplete: boolean;
  error?: string;
}

export const SPECIAL_EDU_CATEGORIES = [
  { value: 0, label: '培智' },
  { value: 1, label: '听障' },
  { value: 2, label: '视障' },
  { value: 3, label: '孤独症' },
];

export const SPECIAL_RESOURCE_MODALITIES = [
  { value: 'Text', label: '文本资源' },
  { value: 'ImageDesc', label: '图片描述' },
  { value: 'AudioScript', label: '音频脚本' },
  { value: 'VideoScript', label: '视频脚本' },
  { value: 'SocialStory', label: '社交故事' },
  { value: 'VisualSupport', label: '视觉支持材料' },
  { value: 'BehaviorPlan', label: '行为干预方案' },
  { value: 'BrailleParallel', label: '盲文对照' },
];

@Injectable({ providedIn: 'root' })
export class SpecialEduService {
  private base = '/api/learning/special-edu';

  categoryName(v: number): string {
    return SPECIAL_EDU_CATEGORIES.find(c => c.value === v)?.label ?? String(v);
  }

  modalityName(v: string): string {
    return SPECIAL_RESOURCE_MODALITIES.find(m => m.value === v)?.label ?? v;
  }

  stripCodeBlock(raw: string): string {
    let s = (raw ?? '').trim();
    if (s.startsWith('```')) {
      const i = s.indexOf('\n');
      if (i >= 0) s = s.slice(i + 1);
      if (s.endsWith('```')) s = s.slice(0, -3).trim();
    }
    return s;
  }

  tryParse<T>(raw: string): T | null {
    try {
      return JSON.parse(this.stripCodeBlock(raw)) as T;
    } catch {
      return null;
    }
  }

  async *ssePost<T>(url: string, body: unknown): AsyncGenerator<SseChunk> {
    const res = await fetch(url, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(body),
    });
    if (!res.ok || !res.body) {
      throw new Error(`请求失败: ${res.status}`);
    }
    const reader = res.body.getReader();
    const decoder = new TextDecoder();
    let buf = '';
    for (;;) {
      const { done, value } = await reader.read();
      if (done) break;
      buf += decoder.decode(value, { stream: true });
      const parts = buf.split('\n\n');
      buf = parts.pop() ?? '';
      for (const p of parts) {
        const line = p.split('\n').find(l => l.startsWith('data:'));
        if (!line) continue;
        try {
          const chunk = JSON.parse(line.slice(5).trim()) as SseChunk;
          yield chunk;
          if (chunk.isComplete) return;
        } catch {
          /* ignore partial */
        }
      }
    }
  }

  async downloadBlob(url: string, body: unknown, filename: string): Promise<void> {
    const res = await fetch(url, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(body),
    });
    if (!res.ok) throw new Error(`导出失败: ${res.status}`);
    const blob = await res.blob();
    const a = document.createElement('a');
    a.href = URL.createObjectURL(blob);
    a.download = filename;
    a.click();
    setTimeout(() => URL.revokeObjectURL(a.href), 5000);
  }

  generateTeachingDesign = (body: unknown) => this.ssePost(`${this.base}/generate-teaching-design`, body);
  generateIep = (body: unknown) => this.ssePost(`${this.base}/generate-iep`, body);
  generateResource = (body: unknown) => this.ssePost(`${this.base}/generate-resource`, body);
}
