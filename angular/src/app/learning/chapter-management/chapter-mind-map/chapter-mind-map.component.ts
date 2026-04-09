import {
  Component,
  input,
  AfterViewInit,
  OnChanges,
  OnDestroy,
  ElementRef,
  ViewChild,
  ChangeDetectionStrategy,
  ViewEncapsulation,
} from '@angular/core';
import { CommonModule } from '@angular/common';
import type { ChapterDto } from '../../../proxy/courses/dtos/models';

@Component({
  selector: 'app-chapter-mind-map',
  standalone: true,
  imports: [CommonModule],
  template: `
    <div #mindMapContainer class="mind-map-container"></div>
  `,
  styles: [`
    .mind-map-container {
      width: 100%;
      height: 100%;
    }
  `],
  encapsulation: ViewEncapsulation.None,
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ChapterMindMapComponent implements AfterViewInit, OnChanges, OnDestroy {
  chapters = input<ChapterDto[]>([]);
  courseTitle = input<string>('');

  @ViewChild('mindMapContainer', { static: true }) container!: ElementRef<HTMLDivElement>;

  private jsMindInstance: any = null;
  private initialized = false;

  ngAfterViewInit() {
    this.initMindMap();
  }

  ngOnChanges() {
    if (this.initialized) {
      this.initMindMap();
    }
  }

  ngOnDestroy() {
    this.jsMindInstance = null;
    this.initialized = false;
  }

  private async initMindMap() {
    const container = this.container?.nativeElement;
    if (!container) return;

    // Clear previous content
    container.innerHTML = '';
    this.jsMindInstance = null;

    // Dynamic import jsMind library
    const jsMindModule = await import('jsmind');
    // eslint-disable-next-line @typescript-eslint/no-explicit-any
    const JsMind = (jsMindModule as any).default || jsMindModule;

    const mindData = this.buildMindData();

    const options = {
      container: container,
      editable: false,
      theme: 'primary',
      view: {
        engine: 'canvas',
        hmargin: 100,
        vmargin: 50,
        line_width: 2,
        line_color: '#999',
      },
      layout: {
        hspace: 30,
        vspace: 20,
        pspace: 13,
      },
    };

    // eslint-disable-next-line @typescript-eslint/no-explicit-any
    this.jsMindInstance = new (JsMind as any)(options);
    this.jsMindInstance.show(mindData);
    this.initialized = true;
  }

  private buildMindData(): any {
    const chapters = this.chapters();
    const courseTitle = this.courseTitle() || '课程';

    const depthColors = ['#1677ff', '#52c41a', '#fa8c16', '#722ed1', '#eb2f96'];

    const convertNode = (chapter: ChapterDto, direction: number, depth: number): any => {
      const node: any = {
        id: chapter.id || `node-${Math.random().toString(36).substr(2, 9)}`,
        topic: chapter.title || '',
        direction,
        'background-color': depthColors[depth % depthColors.length],
        'font-color': '#fff',
      };

      if (chapter.children?.length) {
        node.children = chapter.children.map(child =>
          convertNode(child, direction, depth + 1)
        );
      }

      return node;
    };

    // Distribute root chapters: alternate left and right for symmetric layout
    const leftChildren: any[] = [];
    const rightChildren: any[] = [];

    chapters.forEach((chapter, index) => {
      const direction = index % 2 === 0 ? 1 : -1; // 1=right, -1=left
      const node = convertNode(chapter, direction, 0);
      if (direction === 1) {
        rightChildren.push(node);
      } else {
        leftChildren.push(node);
      }
    });

    return {
      meta: {
        name: courseTitle,
        author: 'KnowledgeHub',
        version: '0.1',
      },
      format: 'node_tree',
      data: {
        id: 'root',
        topic: courseTitle,
        'background-color': '#1a1a2e',
        'font-color': '#fff',
        'font-size': 18,
        children: [...rightChildren, ...leftChildren],
      },
    };
  }
}
