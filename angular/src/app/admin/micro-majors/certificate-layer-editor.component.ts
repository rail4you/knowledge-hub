import {
  AfterViewInit,
  ChangeDetectionStrategy,
  Component,
  ElementRef,
  EventEmitter,
  Input,
  OnDestroy,
  Output,
  ViewChild,
  inject,
  signal,
} from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzIconModule } from 'ng-zorro-antd/icon';
import { NzInputModule } from 'ng-zorro-antd/input';
import { NzSelectModule } from 'ng-zorro-antd/select';
import { NzSliderModule } from 'ng-zorro-antd/slider';
import { NzSpinModule } from 'ng-zorro-antd/spin';
import { NzSwitchModule } from 'ng-zorro-antd/switch';
import { NzMessageService } from 'ng-zorro-antd/message';
import { NzColorPickerModule } from 'ng-zorro-antd/color-picker';
import { CertificateLayer } from '../../micro-majors/micro-major.service';
import {
  CERTIFICATE_FIELD_TYPES,
  drawCertificate,
  fieldLabel,
  loadCertificateImage,
  sampleText,
} from './certificate-canvas.util';

interface DragState {
  index: number;
  offsetX: number;
  offsetY: number;
}

@Component({
  selector: 'app-certificate-layer-editor',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    NzButtonModule,
    NzIconModule,
    NzInputModule,
    NzSelectModule,
    NzSliderModule,
    NzSpinModule,
    NzSwitchModule,
    NzColorPickerModule,
  ],
  templateUrl: './certificate-layer-editor.component.html',
  styleUrls: ['./certificate-layer-editor.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class CertificateLayerEditorComponent implements AfterViewInit, OnDestroy {
  @Input() set imageUrl(value: string) {
    this._imageUrl = value;
    this.reloadImage();
  }
  get imageUrl(): string {
    return this._imageUrl;
  }

  @Input() set initialLayers(value: CertificateLayer[]) {
    this.layers = value?.length
      ? value.map(l => ({ ...l }))
      : [];
    this.selectedIndex = -1;
    // 若图片已加载完成，立即重绘以显示占位符
    if (this.imageElement) {
      this.redraw();
    }
  }

  @Output() readonly onSave = new EventEmitter<CertificateLayer[]>();
  @Output() readonly onClose = new EventEmitter<void>();

  @ViewChild('canvas') canvasRef!: ElementRef<HTMLCanvasElement>;

  private readonly message = inject(NzMessageService);

  imageLoading = signal(false);
  layers: CertificateLayer[] = [];
  selectedIndex: number = -1;
  fieldTypes = CERTIFICATE_FIELD_TYPES;

  private _imageUrl = '';
  private imageElement?: HTMLImageElement;
  private drag?: DragState;

  get selectedLayer(): CertificateLayer | undefined {
    return this.layers[this.selectedIndex];
  }

  get selectedFieldLabel(): string {
    return this.selectedLayer ? this.selectedLayer.label : '';
  }

  ngAfterViewInit(): void {
    this.reloadImage();
  }

  ngOnDestroy(): void {
    // no-op
  }

  private get canvas(): HTMLCanvasElement {
    return this.canvasRef.nativeElement;
  }

  private reloadImage(): void {
    if (!this._imageUrl) {
      this.imageElement = undefined;
      this.redraw();
      return;
    }
    this.imageLoading.set(true);
    loadCertificateImage(this._imageUrl)
      .then(img => {
        this.imageElement = img;
        this.redraw();
      })
      .catch(err => {
        this.imageLoading.set(false);
        this.message.error(err?.message || '证书图片加载失败');
      })
      .finally(() => this.imageLoading.set(false));
  }

  addField(fieldType: string): void {
    const layer: CertificateLayer = {
      id: `L${Date.now()}-${Math.random().toString(36).slice(2, 7)}`,
      fieldType,
      label: fieldLabel(fieldType),
      customFieldName: fieldType === 'custom' ? '自定义' : undefined,
      x: 50,
      y: 50,
      fontSize: 40,
      color: '#1f1f1f',
      fontWeight: 600,
      center: true,
      fontFamily: undefined,
    };
    this.layers.push(layer);
    this.selectedIndex = this.layers.length - 1;
    this.redraw();
  }

  selectLayer(index: number): void {
    this.selectedIndex = index;
    this.redraw();
  }

  deleteSelected(): void {
    if (this.selectedIndex < 0) return;
    this.layers.splice(this.selectedIndex, 1);
    this.selectedIndex = -1;
    this.redraw();
  }

  /** 供模板在属性编辑时触发重绘（模板内使用 (ngModelChange) 绑定） */
  redrawPublic(): void {
    this.redraw();
  }

  save(): void {
    this.onSave.emit(this.layers.map(l => ({ ...l })));
  }

  close(): void {
    this.onClose.emit();
  }

  onCanvasPointerDown(event: PointerEvent): void {
    if (!this.imageElement) return;
    const pos = this.toCanvasPoint(event);
    const hit = this.hitTest(pos.x, pos.y);
    if (hit >= 0) {
      this.selectedIndex = hit;
      const layer = this.layers[hit];
      const center = this.layerCenter(layer);
      this.drag = {
        index: hit,
        offsetX: pos.x - center.x,
        offsetY: pos.y - center.y,
      };
      this.canvas.setPointerCapture(event.pointerId);
      this.redraw();
    } else {
      this.selectedIndex = -1;
      this.redraw();
    }
  }

  onCanvasPointerMove(event: PointerEvent): void {
    if (!this.drag || !this.imageElement) return;
    const pos = this.toCanvasPoint(event);
    const layer = this.layers[this.drag.index];
    const maxX = this.imageElement.naturalWidth;
    const maxY = this.imageElement.naturalHeight;
    layer.x = Math.max(0, Math.min(100, ((pos.x - this.drag.offsetX) / maxX) * 100));
    layer.y = Math.max(0, Math.min(100, ((pos.y - this.drag.offsetY) / maxY) * 100));
    this.redraw();
  }

  onCanvasPointerUp(): void {
    this.drag = undefined;
  }

  private toCanvasPoint(event: PointerEvent): { x: number; y: number } {
    const rect = this.canvas.getBoundingClientRect();
    const scaleX = this.canvas.width / rect.width;
    const scaleY = this.canvas.height / rect.height;
    return {
      x: (event.clientX - rect.left) * scaleX,
      y: (event.clientY - rect.top) * scaleY,
    };
  }

  private layerCenter(layer: CertificateLayer): { x: number; y: number } {
    if (!this.imageElement) return { x: 0, y: 0 };
    const w = this.imageElement.naturalWidth;
    const h = this.imageElement.naturalHeight;
    return { x: (layer.x / 100) * w, y: (layer.y / 100) * h };
  }

  private hitTest(x: number, y: number): number {
    if (!this.imageElement) return -1;
    const ctx = this.canvas.getContext('2d');
    if (!ctx) return -1;
    const scale = this.imageElement.naturalWidth / 1000;
    // 从最上层开始往回找
    for (let i = this.layers.length - 1; i >= 0; i--) {
      const layer = this.layers[i];
      const fontSize = layer.fontSize * scale;
      ctx.font = `${layer.fontWeight} ${fontSize}px ${layer.fontFamily || 'sans-serif'}`;
      const text = sampleText(layer);
      const width = ctx.measureText(text).width;
      const height = fontSize;
      const cx = (layer.x / 100) * this.imageElement.naturalWidth;
      const cy = (layer.y / 100) * this.imageElement.naturalHeight;
      const pad = 8;
      let left: number, right: number;
      if (layer.center) {
        left = cx - width / 2 - pad;
        right = cx + width / 2 + pad;
      } else {
        left = cx - pad;
        right = cx + width + pad;
      }
      const top = cy - height / 2 - pad;
      const bottom = cy + height / 2 + pad;
      if (x >= left && x <= right && y >= top && y <= bottom) {
        return i;
      }
    }
    return -1;
  }

  private redraw(): void {
    if (!this.imageElement) return;
    drawCertificate(this.canvas, this.imageElement, this.layers, {}, true);
  }
}
