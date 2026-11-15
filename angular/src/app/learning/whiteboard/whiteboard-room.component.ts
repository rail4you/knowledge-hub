import {
  Component, ChangeDetectionStrategy, inject, signal, computed,
  OnInit, OnDestroy, ElementRef, ViewChild, NgZone, afterNextRender,
} from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router, RouterModule } from '@angular/router';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzIconModule } from 'ng-zorro-antd/icon';
import { NzModalModule } from 'ng-zorro-antd/modal';
import { NzInputModule } from 'ng-zorro-antd/input';
import { NzSpinModule } from 'ng-zorro-antd/spin';
import { NzTagModule } from 'ng-zorro-antd/tag';
import { NzTooltipModule } from 'ng-zorro-antd/tooltip';
import { NzMessageService } from 'ng-zorro-antd/message';
import { NzEmptyModule } from 'ng-zorro-antd/empty';
import { NzSelectModule } from 'ng-zorro-antd/select';
import { NzInputNumberModule } from 'ng-zorro-antd/input-number';
import Konva from 'konva';
import { Subscription } from 'rxjs';
import {
  WhiteboardService, WhiteboardDetail, BoardElement, InvitableTeacher,
} from './whiteboard.service';
import { WhiteboardRealtimeService, PresenceUser } from './whiteboard-realtime.service';
import { WhiteboardChatPanelComponent } from './whiteboard-chat-panel.component';
import { WhiteboardAiPanelComponent } from './whiteboard-ai-panel.component';

/** 文本卡片最小宽度（再小就会变成竖排字） */
const WHITEBOARD_MIN_TEXT_W = 80;

/** 便签最小宽 / 高 */
const WHITEBOARD_MIN_NOTE_W = 100;
const WHITEBOARD_MIN_NOTE_H = 80;

/**
 * 智能白板画布房间（Konva）。
 *
 * <p>参照原型：拖动空白处平移、滚轮缩放、点击元素选中后可拖动/缩放/旋转；
 * 支持文本、便签、以及**从自己课程资源里选的图片与视频**。</p>
 *
 * <p><b>素材口径</b>：图片/视频只存 `resourceId`，不内联 base64 ——
 * 一是避免消息过大，二是让素材始终跟随资源中心的最新版本与权限。</p>
 *
 * <p><b>实时协同</b>：SignalR 群组，元素 upsert/delete 写库并广播；
 * 光标只广播。远端光标带教师姓名浮标，便于两人同时操作时区分。</p>
 */
@Component({
  selector: 'app-whiteboard-room',
  standalone: true,
  imports: [
    CommonModule, FormsModule, RouterModule,
    NzButtonModule, NzIconModule, NzModalModule, NzInputModule,
    NzSpinModule, NzTagModule, NzTooltipModule, NzEmptyModule, NzSelectModule,
    NzInputNumberModule,
    WhiteboardChatPanelComponent, WhiteboardAiPanelComponent,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="room">
      <!-- 顶部：标题 + 在线成员 + 操作 -->
      <header class="room__head">
        <button nz-button nzType="text" (click)="back()" nz-tooltip nzTooltipTitle="返回白板列表">
          <span nz-icon nzType="arrow-left"></span>
        </button>
        <div class="room__title">
          <h2>{{ board()?.title || '加载中…' }}</h2>
          <p>
            @if (board()?.subject) { {{ board()!.subject }} · }
            创建人 {{ board()?.creatorName }}
            @if (board()?.topic) { · {{ board()!.topic }} }
          </p>
        </div>

        <div class="room__online">
          @for (m of onlineMembers(); track m.userId) {
            <span class="avatar" [style.background]="m.color" [title]="m.userName + '（在线）'">
              {{ initial(m.userName) }}
            </span>
          }
          <span class="room__conn" [class.is-off]="!connected()">
            <i></i>{{ connected() ? '实时协同中' : '连接中…' }}
          </span>
        </div>

        <div class="room__actions">
          @if (board()?.isOwner) {
            <button nz-button (click)="openInvite()">
              <span nz-icon nzType="user-add"></span> 邀请教师共创
            </button>
          }
          <button
            nz-button
            class="room__collab"
            [class.is-active]="!sideCollapsed() && sideTab() === 'collab'"
            (click)="headerSideClick('collab')"
            nz-tooltip
            nzTooltipTitle="文字聊天 / 语音通话"
          >
            <span nz-icon nzType="comment"></span>
            协作
            @if (voiceActive()) {
              <i class="room__live" title="语音通话中"></i>
            }
            @if (chatUnread() > 0) {
              <span class="room__badge">{{ chatUnread() > 99 ? '99+' : chatUnread() }}</span>
            }
          </button>
          <button
            nz-button
            class="room__collab room__collab--ai"
            [class.is-active]="!sideCollapsed() && sideTab() === 'ai'"
            (click)="headerSideClick('ai')"
            nz-tooltip
            nzTooltipTitle="AI 助手：按课程与白板内容生成文本 / 配图"
          >
            <span nz-icon nzType="robot"></span>
            AI 助手
          </button>
          <span class="room__me" [style.borderColor]="myColor()">
            <i [style.background]="myColor()"></i>{{ myName() }}
          </span>
        </div>
      </header>

      <!-- 工具栏 -->
      <div class="room__tools">
        <button nz-button (click)="addText()" [disabled]="!canEdit()">
          <span nz-icon nzType="font-size"></span> 文本
        </button>
        <button nz-button (click)="addNote()" [disabled]="!canEdit()">
          <span nz-icon nzType="pushpin"></span> 便签
        </button>
        <button nz-button (click)="openResourcePicker('image')" [disabled]="!canEdit()">
          <span nz-icon nzType="picture"></span> 从课程资源选图片
        </button>
        <button nz-button (click)="openResourcePicker('video')" [disabled]="!canEdit()">
          <span nz-icon nzType="play-circle"></span> 从课程资源选视频
        </button>
        <span class="room__hint">
          拖动空白处平移 · 滚轮缩放 · 点选元素后可拖动/缩放/旋转
        </span>
      </div>

      <!-- 画布 + 右侧面板（属性 / 协作 / AI 助手） -->
      <div class="room__body">
        <div class="room__canvas">
          <div class="room__stage" #stageHost [class.is-locked]="!canEdit()">
            @if (loading()) {
              <div class="room__loading"><nz-spin nzSimple nzTip="正在加载画布…"></nz-spin></div>
            }
          </div>
        </div>

        <!-- 右侧面板 -->
        <aside class="room__side" [class.is-collapsed]="sideCollapsed()">
          <div class="room__side-head">
            <div class="room__tabs" role="tablist">
              <button
                type="button"
                class="room__tab"
                role="tab"
                [class.is-active]="sideTab() === 'props'"
                [attr.aria-selected]="sideTab() === 'props'"
                (click)="switchSide('props')"
              >
                <span nz-icon nzType="setting"></span> 属性
                @if (hasSelection() && sideTab() !== 'props') {
                  <i class="room__tab-pip" title="有选中的元素"></i>
                }
              </button>
              <button
                type="button"
                class="room__tab"
                role="tab"
                [class.is-active]="sideTab() === 'collab'"
                [attr.aria-selected]="sideTab() === 'collab'"
                (click)="switchSide('collab')"
              >
                <span nz-icon nzType="comment"></span> 协作
                @if (voiceActive()) {
                  <i class="room__live" title="语音通话中"></i>
                }
                @if (chatUnread() > 0 && sideTab() !== 'collab') {
                  <span class="room__tab-dot">{{ chatUnread() > 99 ? '99+' : chatUnread() }}</span>
                }
              </button>
              <button
                type="button"
                class="room__tab"
                role="tab"
                [class.is-active]="sideTab() === 'ai'"
                [attr.aria-selected]="sideTab() === 'ai'"
                (click)="switchSide('ai')"
              >
                <span nz-icon nzType="robot"></span> AI
              </button>
            </div>
            <button
              class="room__side-collapse"
              nz-button
              nzType="text"
              nzSize="small"
              nz-tooltip
              nzTooltipTitle="收起右侧面板"
              (click)="toggleSide()"
            >
              <span nz-icon nzType="right"></span>
            </button>
          </div>

          <div class="room__side-body">
            <!-- ① 元素属性 -->
            <div class="room__side-pane" [class.is-hidden]="sideTab() !== 'props'">
              @if (selected(); as sel) {
                <div class="insp">
                  <div class="insp__head">
                    <span class="insp__type">
                      <span nz-icon [nzType]="iconOf(sel.type)"></span>
                      {{ typeLabel(sel.type) }}
                    </span>
                    <span class="insp__size">{{ sizeText(sel) }}</span>
                  </div>

                  @if (sel.type === 'text' || sel.type === 'note') {
                    <label class="insp__field">
                      <span>内容</span>
                      <textarea
                        nz-input
                        [nzAutosize]="{ minRows: 4, maxRows: 10 }"
                        [ngModel]="sel.text"
                        (ngModelChange)="onTextInput($event)"
                      ></textarea>
                    </label>

                    <label class="insp__field insp__field--range">
                      <span>字号 <b>{{ sel.fs }}</b> px</span>
                      <input
                        type="range" min="12" max="72" step="1"
                        [ngModel]="sel.fs"
                        (ngModelChange)="onFontInput($event)"
                      />
                    </label>

                    <div class="insp__field">
                      <span>{{ sel.type === 'note' ? '尺寸（宽 × 高）' : '宽度' }}</span>
                      <div class="insp__nums">
                        <nz-input-number
                          nzSize="small"
                          [nzMin]="sel.type === 'note' ? 100 : 80"
                          [nzMax]="1600"
                          [nzStep]="10"
                          [ngModel]="widthOf(sel)"
                          (ngModelChange)="onSizeInput('w', $event)"
                        ></nz-input-number>
                        @if (sel.type === 'note') {
                          <span class="insp__x">×</span>
                          <nz-input-number
                            nzSize="small"
                            [nzMin]="80"
                            [nzMax]="1200"
                            [nzStep]="10"
                            [ngModel]="heightOf(sel)"
                            (ngModelChange)="onSizeInput('h', $event)"
                          ></nz-input-number>
                        }
                        <span class="insp__unit">px</span>
                      </div>
                    </div>

                    @if (sel.by) { <p class="insp__by">— {{ sel.by }}</p> }
                  }

                  @if (sel.type === 'image' || sel.type === 'video') {
                    <div class="insp__material">
                      <span class="insp__material-label">素材</span>
                      <span class="insp__material-name">{{ sel.resourceName || '课程资源' }}</span>
                      <span class="insp__tag">{{ sel.aiGenerated ? 'AI 生成' : '课程资源' }}</span>
                    </div>
                  }

                  <button nz-button nzDanger class="insp__delete" (click)="deleteSelected()">
                    <span nz-icon nzType="delete"></span> 删除所选
                  </button>
                </div>
              } @else {
                <div class="insp insp--empty">
                  <span nz-icon nzType="aim"></span>
                  <p>还没有选中元素</p>
                  <small>点选画布上的文本 / 便签 / 图片，就可以在这里改内容、字号与尺寸</small>
                </div>
              }
            </div>

            <!-- ② 协作（聊天 + 语音） -->
            <div class="room__side-pane" [class.is-hidden]="sideTab() !== 'collab'">
              <app-whiteboard-chat-panel
                [embedded]="true"
                (unreadChange)="chatUnread.set($event)"
              />
            </div>

            <!-- ③ AI 助手 -->
            <div class="room__side-pane" [class.is-hidden]="sideTab() !== 'ai'">
              <app-whiteboard-ai-panel
                [whiteboardId]="boardId"
                [courseName]="board()?.courseName || null"
                [selectionKey]="selectedTextKey()"
                [selectionText]="selectedPlainText()"
                (insertText)="insertAssistantText($event)"
                (replaceSelection)="applyAssistantText($event, 'replace')"
                (appendSelection)="applyAssistantText($event, 'append')"
                (insertImage)="insertAssistantImage($event)"
              />
            </div>
          </div>
        </aside>

        <!-- 收起后的图标条：鼠标移到图标上有放大 / 高亮反馈，点一下展开对应页签 -->
        @if (sideCollapsed()) {
          <div class="room__rail">
            <button
              type="button"
              class="rail__btn"
              [class.is-dot]="hasSelection()"
              (click)="switchSide('props')"
              nz-tooltip
              nzTooltipTitle="元素属性"
              nzTooltipPlacement="left"
            >
              <span nz-icon nzType="setting"></span>
              @if (hasSelection()) { <i class="rail__pip"></i> }
            </button>
            <button
              type="button"
              class="rail__btn"
              [class.is-live]="voiceActive()"
              (click)="switchSide('collab')"
              nz-tooltip
              nzTooltipTitle="协作（聊天 / 语音）"
              nzTooltipPlacement="left"
            >
              <span nz-icon nzType="comment"></span>
              @if (chatUnread() > 0) {
                <span class="rail__badge">{{ chatUnread() > 9 ? '9+' : chatUnread() }}</span>
              }
              @if (voiceActive()) { <i class="rail__live"></i> }
            </button>
            <button
              type="button"
              class="rail__btn rail__btn--ai"
              (click)="switchSide('ai')"
              nz-tooltip
              nzTooltipTitle="AI 助手"
              nzTooltipPlacement="left"
            >
              <span nz-icon nzType="robot"></span>
            </button>
            <span class="rail__sep"></span>
            <button
              type="button"
              class="rail__btn"
              (click)="toggleSide()"
              nz-tooltip
              nzTooltipTitle="展开面板"
              nzTooltipPlacement="left"
            >
              <span nz-icon nzType="left"></span>
            </button>
          </div>
        }
      </div>
    </div>

    <!-- 资源选择框 -->
    <nz-modal
      [(nzVisible)]="pickerOpen"
      [nzTitle]="pickerType === 'image' ? '从课程资源选择图片' : '从课程资源选择视频'"
      nzOkText="插入到白板"
      nzCancelText="取消"
      [nzWidth]="820"
      [nzOkDisabled]="!pickedResource"
      (nzOnOk)="insertPickedResource()"
      (nzOnCancel)="pickerOpen = false"
    >
      <ng-container *nzModalContent>
        <div class="picker">
          <div class="picker__filters">
            <nz-select
              style="width:240px"
              nzPlaceHolder="按课程筛选"
              nzAllowClear
              [(ngModel)]="pickerCourseId"
              (ngModelChange)="loadResources()"
            >
              @for (c of myCourses(); track c.id) {
                <nz-option [nzValue]="c.id" [nzLabel]="c.title"></nz-option>
              }
            </nz-select>
            <input nz-input placeholder="搜索资源名称" [(ngModel)]="pickerKeyword"
                   (keyup.enter)="loadResources()" style="width:240px" />
            <button nz-button (click)="loadResources()">查询</button>
          </div>

          @if (resourcesLoading()) {
            <div class="picker__loading"><nz-spin nzSimple></nz-spin></div>
          } @else if (resources().length === 0) {
            <nz-empty nzNotFoundContent="没有符合条件的素材（只列你自己上传的资源）"></nz-empty>
          } @else {
            <div class="picker__grid">
              @for (r of resources(); track r.id) {
                <div class="picker__item"
                     [class.is-picked]="pickedResource?.id === r.id"
                     (click)="pickedResource = r">
                  @if (pickerType === 'image') {
                    <img [src]="resourceThumb(r.id)" [alt]="r.name" loading="lazy" />
                  } @else {
                    <div class="picker__video"><span nz-icon nzType="play-circle" nzTheme="fill"></span></div>
                  }
                  <span class="picker__name" [title]="r.name">{{ r.name }}</span>
                </div>
              }
            </div>
          }
        </div>
      </ng-container>
    </nz-modal>

    <!-- 邀请教师 -->
    <nz-modal
      [(nzVisible)]="inviteOpen"
      nzTitle="邀请同校教师加入共创"
      nzOkText="发送邀请"
      nzCancelText="关闭"
      [nzOkLoading]="inviting()"
      (nzOnOk)="submitInvite()"
      (nzOnCancel)="inviteOpen = false"
    >
      <ng-container *nzModalContent>
        <p class="invite__tip">
          被邀请的教师登录后即可在自己的「智能白板」列表看到此白板，两人同时打开就能实时协作。
        </p>
        @if (teachersLoading()) {
          <div class="picker__loading"><nz-spin nzSimple></nz-spin></div>
        } @else if (teachers().length === 0) {
          <nz-empty nzNotFoundContent="本校没有其他教师可邀请"></nz-empty>
        } @else {
          <div class="invite__list">
            @for (t of teachers(); track t.userId) {
              <label class="invite__row" [class.is-joined]="t.joined">
                <input type="checkbox" [disabled]="t.joined"
                       [checked]="inviteSelected.has(t.userId)"
                       (change)="toggleInvite(t.userId, $event)" />
                <span class="avatar avatar--sm" [style.background]="colorOf(t.userName)">{{ initial(t.userName) }}</span>
                <span class="invite__name">{{ t.name || t.userName }}</span>
                <span class="invite__uid">{{ t.userName }}</span>
                @if (t.joined) { <nz-tag nzColor="green">已加入</nz-tag> }
              </label>
            }
          </div>
        }
      </ng-container>
    </nz-modal>
  `,
  styles: [`
    /* 高度由组件按「可视区 - 本页顶部偏移」实测设置（见 fitViewportHeight），
       避免不同布局（有无面包屑）下要么出现页面滚动条、要么底部输入框被裁掉 */
    :host { display: block; }
    .room { position: relative; display: flex; flex-direction: column; height: 100%; background: #f5f8f7; }
    .room__head { display: flex; align-items: center; gap: 12px; padding: 10px 18px; background: #fff; border-bottom: 1px solid #e0e8e5; }
    .room__title h2 { margin: 0; font-size: 16px; font-weight: 650; }
    .room__title p { margin: 2px 0 0; font-size: 12px; color: #64757a; }
    .room__online { display: flex; align-items: center; gap: 6px; margin-left: auto; }
    .avatar { width: 26px; height: 26px; border-radius: 50%; color: #fff; font-size: 11px; display: grid; place-items: center; font-weight: 600; }
    .avatar--sm { width: 22px; height: 22px; font-size: 10px; }
    .room__conn { display: inline-flex; align-items: center; gap: 5px; margin-left: 8px; font-size: 11px; color: #18a058; }
    .room__conn i { width: 6px; height: 6px; border-radius: 50%; background: #18a058; }
    .room__conn.is-off { color: #d6a21e; }
    .room__conn.is-off i { background: #d6a21e; }
    .room__actions { display: flex; align-items: center; gap: 10px; }
    .room__me { display: inline-flex; align-items: center; gap: 6px; padding: 2px 10px; border: 1px solid; border-radius: 999px; font-size: 12px; }
    .room__me i { width: 8px; height: 8px; border-radius: 50%; }
    .room__tools { display: flex; align-items: center; gap: 8px; padding: 8px 18px; background: #fff; border-bottom: 1px solid #e0e8e5; }
    .room__hint { margin-left: auto; font-size: 12px; color: #96a5aa; }
    .room__body { position: relative; flex: 1 1 auto; display: flex; min-height: 0; }
    .room__canvas { position: relative; flex: 1 1 auto; min-width: 0; display: flex; }
    .room__stage {
      flex: 1 1 auto; position: relative; overflow: hidden;
      background-color: #fbfcfc;
      background-image: radial-gradient(#dbe4e2 1px, transparent 1px);
      background-size: 24px 24px;
    }
    .room__stage.is-locked::after { content: '只读模式'; position: absolute; right: 12px; top: 12px; font-size: 12px; color: #96a5aa; }
    .room__loading { position: absolute; inset: 0; display: grid; place-items: center; }
    /* 右侧面板外壳：tab 头 + 内容区（内容区两个面板都保持挂载，靠 is-hidden 切换） */
    .room__side {
      flex: 0 0 auto; display: flex; flex-direction: column;
      width: 340px; height: 100%; min-height: 0;
      background: #fff; border-left: 1px solid #e0e8e5;
      transition: width .18s ease;
    }
    .room__side.is-collapsed { width: 0; border-left: none; overflow: hidden; }
    .room__side-head {
      flex: 0 0 auto; display: flex; align-items: center; gap: 4px;
      padding: 5px 8px 5px 10px; border-bottom: 1px solid #eef2f1;
    }
    .room__tab {
      position: relative; display: inline-flex; align-items: center; gap: 5px;
      padding: 5px 10px; border: none; border-radius: 8px; cursor: pointer;
      background: transparent; color: #64757a; font-size: 12.5px; font-weight: 600;
    }
    .room__tab:hover { background: #f4f8f6; color: #22333a; }
    .room__tab.is-active { background: #eef8f5; color: #087f7a; }
    .room__tab-dot {
      min-width: 15px; height: 15px; padding: 0 4px; border-radius: 999px;
      background: #e0523f; color: #fff; font-size: 10px; line-height: 15px; text-align: center;
    }
    .room__side-collapse { margin-left: auto; }
    .room__side-body { flex: 1 1 auto; min-height: 0; position: relative; }
    .room__side-pane { position: absolute; inset: 0; display: flex; flex-direction: column; min-height: 0; }
    .room__side-pane.is-hidden { display: none; }
    .room__collab { position: relative; }
    .room__collab.is-active { color: #087f7a; border-color: #b9ded7; background: #f1faf8; }
    .room__collab--ai { color: #4a3f78; }
    .room__collab--ai.is-active { color: #4a3f78; border-color: #d6cdee; background: #f6f3fd; }
    .room__live { width: 7px; height: 7px; border-radius: 50%; background: #18a058; margin-left: 4px; box-shadow: 0 0 0 0 rgba(24,160,88,.5); animation: room-live 1.6s infinite; }
    @keyframes room-live {
      0% { box-shadow: 0 0 0 0 rgba(24,160,88,.45); }
      70% { box-shadow: 0 0 0 7px rgba(24,160,88,0); }
      100% { box-shadow: 0 0 0 0 rgba(24,160,88,0); }
    }
    .room__badge {
      min-width: 16px; height: 16px; padding: 0 4px; margin-left: 4px;
      border-radius: 999px; background: #e0523f; color: #fff;
      font-size: 10px; line-height: 16px; font-weight: 600;
    }
    /* ── 右侧面板（属性 / 协作 / AI） ── */
    .room__side {
      flex: 0 0 auto; display: flex; flex-direction: column;
      width: 360px; height: 100%; min-height: 0;
      background: #fff; border-left: 1px solid #e0e8e5;
      transition: width .18s ease;
    }
    .room__side.is-collapsed { width: 0; border-left: none; overflow: hidden; }
    .room__side-head {
      flex: 0 0 auto; display: flex; align-items: center; gap: 4px;
      padding: 5px 6px 5px 8px; border-bottom: 1px solid #eef2f1;
    }
    .room__tabs { display: flex; align-items: center; gap: 2px; }
    .room__tab {
      position: relative; display: inline-flex; align-items: center; gap: 5px;
      padding: 5px 10px; border: none; border-radius: 8px; cursor: pointer;
      background: transparent; color: #64757a; font-size: 12.5px; font-weight: 600;
      transition: background .15s ease, color .15s ease;
    }
    .room__tab:hover { background: #f4f8f6; color: #22333a; }
    .room__tab.is-active { background: #eef8f5; color: #087f7a; }
    .room__tab.is-active::after {
      content: ''; position: absolute; left: 10px; right: 10px; bottom: -6px;
      height: 2px; border-radius: 2px; background: #087f7a;
    }
    .room__tab-dot {
      min-width: 15px; height: 15px; padding: 0 4px; border-radius: 999px;
      background: #e0523f; color: #fff; font-size: 10px; line-height: 15px; text-align: center;
    }
    .room__tab-pip {
      width: 6px; height: 6px; border-radius: 50%; background: #087f7a;
      box-shadow: 0 0 0 3px rgba(8,127,122,.14);
    }
    .room__side-collapse { margin-left: auto; }
    .room__side-body { flex: 1 1 auto; min-height: 0; position: relative; }
    .room__side-pane {
      position: absolute; inset: 0; display: flex; flex-direction: column; min-height: 0;
      animation: side-pane-in .16s ease;
    }
    .room__side-pane.is-hidden { display: none; }
    @keyframes side-pane-in {
      from { opacity: 0; transform: translateX(6px); }
      to { opacity: 1; transform: translateX(0); }
    }

    /* 收起后的图标条（悬浮在画布右缘） */
    .room__rail {
      position: absolute; right: 10px; top: 50%; transform: translateY(-50%);
      display: flex; flex-direction: column; align-items: center; gap: 4px;
      padding: 6px 5px; border-radius: 14px;
      background: rgba(255,255,255,.96); border: 1px solid #e0e8e5;
      box-shadow: 0 10px 28px rgba(16,32,48,.12);
      backdrop-filter: blur(6px); z-index: 5;
      animation: rail-in .18s ease;
    }
    @keyframes rail-in {
      from { opacity: 0; transform: translate(6px, -50%); }
      to { opacity: 1; transform: translate(0, -50%); }
    }
    .rail__btn {
      position: relative; width: 34px; height: 34px; border: none; border-radius: 10px;
      display: grid; place-items: center; cursor: pointer;
      background: transparent; color: #5b6b71; font-size: 16px;
      transition: background .16s ease, color .16s ease, transform .16s ease, box-shadow .16s ease;
    }
    .rail__btn:hover {
      background: #eef8f5; color: #087f7a; transform: scale(1.08);
      box-shadow: 0 4px 12px rgba(8,127,122,.18);
    }
    .rail__btn:active { transform: scale(.96); }
    .rail__btn--ai:hover { background: #f4f1fd; color: #6b5aa8; box-shadow: 0 4px 12px rgba(107,90,168,.18); }
    .rail__sep { width: 18px; height: 1px; background: #e8efed; margin: 2px 0; }
    .rail__pip {
      position: absolute; top: 5px; right: 5px; width: 6px; height: 6px; border-radius: 50%;
      background: #087f7a; box-shadow: 0 0 0 2px #fff;
    }
    .rail__badge {
      position: absolute; top: 0; right: 0; min-width: 15px; height: 15px; padding: 0 4px;
      border-radius: 999px; background: #e0523f; color: #fff;
      font-size: 9.5px; line-height: 15px; text-align: center; box-shadow: 0 0 0 2px #fff;
    }
    .rail__live {
      position: absolute; bottom: 4px; right: 5px; width: 7px; height: 7px; border-radius: 50%;
      background: #18a058; box-shadow: 0 0 0 2px #fff;
    }

    /* ── 属性面板 ── */
    .insp { display: flex; flex-direction: column; gap: 14px; padding: 14px; overflow-y: auto; }
    .insp__head { display: flex; align-items: center; justify-content: space-between; gap: 8px; }
    .insp__type {
      display: inline-flex; align-items: center; gap: 6px;
      font-size: 13px; font-weight: 650; color: #22333a;
    }
    .insp__size { font-size: 11px; color: #96a5aa; font-variant-numeric: tabular-nums; }
    .insp__field { display: flex; flex-direction: column; gap: 6px; }
    .insp__field > span { font-size: 11.5px; color: #64757a; }
    .insp__field > span b { color: #087f7a; font-weight: 650; }
    .insp__field--range input[type='range'] { width: 100%; accent-color: #087f7a; }
    .insp__nums { display: flex; align-items: center; gap: 6px; }
    .insp__x { font-size: 11px; color: #96a5aa; }
    .insp__unit { font-size: 11px; color: #96a5aa; }
    .insp__by { margin: 0; font-size: 11px; color: #96a5aa; }
    .insp__material {
      display: flex; align-items: center; gap: 6px; flex-wrap: wrap;
      padding: 8px 10px; border-radius: 10px; background: #f7faf9; border: 1px solid #eef2f1;
      font-size: 12px; color: #3c4a50;
    }
    .insp__material-label { font-size: 11px; color: #96a5aa; }
    .insp__material-name { flex: 1 1 auto; min-width: 0; overflow: hidden; text-overflow: ellipsis; white-space: nowrap; }
    .insp__tag {
      padding: 0 6px; border-radius: 8px; font-size: 10.5px;
      background: #eef2f1; color: #64757a; border: 1px solid #e3eae8;
    }
    .insp__delete { margin-top: auto; width: 100%; }
    .insp--empty {
      margin: auto; align-items: center; text-align: center; color: #96a5aa; padding: 24px 20px;
    }
    .insp--empty span { font-size: 22px; }
    .insp--empty p { margin: 8px 0 4px; font-size: 12.5px; color: #6d7d83; }
    .insp--empty small { font-size: 11px; line-height: 1.6; }

    .picker { display: flex; flex-direction: column; gap: 12px; }
    .picker__filters { display: flex; gap: 8px; flex-wrap: wrap; }
    .picker__loading { padding: 40px 0; display: grid; place-items: center; }
    .picker__grid { display: grid; grid-template-columns: repeat(auto-fill, minmax(140px, 1fr)); gap: 10px; max-height: 420px; overflow: auto; }
    .picker__item { display: flex; flex-direction: column; gap: 6px; padding: 8px; border: 1px solid #e0e8e5; border-radius: 10px; cursor: pointer; }
    .picker__item.is-picked { border-color: #087f7a; box-shadow: 0 0 0 2px rgba(8,127,122,.15); }
    .picker__item img { width: 100%; height: 86px; object-fit: cover; border-radius: 6px; background: #eef2f1; }
    .picker__video { height: 86px; display: grid; place-items: center; background: #203b40; border-radius: 6px; color: #fff; font-size: 26px; }
    .picker__name { font-size: 12px; overflow: hidden; text-overflow: ellipsis; white-space: nowrap; }
    .invite__tip { margin: 0 0 12px; font-size: 12px; line-height: 1.7; color: #64757a; }
    .invite__list { display: flex; flex-direction: column; gap: 4px; max-height: 360px; overflow: auto; }
    .invite__row { display: flex; align-items: center; gap: 10px; padding: 8px 10px; border-radius: 8px; cursor: pointer; }
    .invite__row:hover { background: #f5f8f7; }
    .invite__row.is-joined { opacity: .6; cursor: default; }
    .invite__name { font-size: 13px; }
    .invite__uid { font-size: 11px; color: #96a5aa; margin-left: auto; }
  `],
})
export class WhiteboardRoomComponent implements OnInit, OnDestroy {
  @ViewChild('stageHost', { static: true }) stageHost!: ElementRef<HTMLDivElement>;

  private readonly host = inject<ElementRef<HTMLElement>>(ElementRef);
  private readonly onWindowResize = () => this.fitViewportHeight();

  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly api = inject(WhiteboardService);
  private readonly rt = inject(WhiteboardRealtimeService);
  private readonly message = inject(NzMessageService);
  private readonly zone = inject(NgZone);

  readonly board = signal<WhiteboardDetail | null>(null);
  readonly loading = signal(true);
  readonly connected = signal(false);
  readonly myColor = signal('#2f6fed');
  readonly myName = signal('');
  readonly onlineMembers = signal<PresenceUser[]>([]);
  readonly selected = signal<BoardElement | null>(null);
  readonly canEdit = computed(() => this.board()?.canEdit !== false);

  // 右侧面板（默认展开；收起后语音/聊天继续，未读数显示在图标条上）
  readonly sideCollapsed = signal(false);
  readonly chatUnread = signal(0);
  readonly voiceActive = this.rt.selfInVoice;
  /** 右侧面板当前 tab：元素属性 / 协作（聊天 + 语音）/ AI 助手 */
  readonly sideTab = signal<'props' | 'collab' | 'ai'>('collab');
  readonly hasSelection = computed(() => !!this.selected());

  /** 当前选中的画布文本（AI 助手据此「改这一段」） */
  readonly selectedTextKey = computed(() => {
    const sel = this.selected();
    return sel && (sel.type === 'text' || sel.type === 'note') ? sel.id : null;
  });
  readonly selectedPlainText = computed(() => {
    const sel = this.selected();
    if (!sel || (sel.type !== 'text' && sel.type !== 'note')) return null;
    const text = (sel.text || '').trim();
    return text.length ? text : null;
  });

  // 资源选择框
  pickerOpen = false;
  pickerType: 'image' | 'video' = 'image';
  pickerCourseId: string | null = null;
  pickerKeyword = '';
  readonly resources = signal<any[]>([]);
  readonly resourcesLoading = signal(false);
  readonly myCourses = signal<any[]>([]);
  pickedResource: any = null;

  // 邀请
  inviteOpen = false;
  readonly teachers = signal<InvitableTeacher[]>([]);
  readonly teachersLoading = signal(false);
  readonly inviting = signal(false);
  inviteSelected = new Set<string>();

  /** 当前白板 Id（模板里要给 AI 助手传参，故不设 private） */
  boardId = '';
  private stage?: Konva.Stage;
  private layer?: Konva.Layer;
  private cursorLayer?: Konva.Layer;
  private tr?: Konva.Transformer;
  /** 本地元素模型：key → BoardElement */
  private elements = new Map<string, BoardElement>();
  private cursors = new Map<string, Konva.Group>();
  private subs: Subscription[] = [];
  private lastCursorSent = 0;
  private resizeObserver?: ResizeObserver;

  constructor() {
    // afterNextRender 必须在注入上下文（构造函数/字段初始化）里注册，放进 ngOnInit 不会生效
    afterNextRender(() => {
      this.fitViewportHeight();
      requestAnimationFrame(() => this.fitViewportHeight());
    });
  }

  ngOnInit(): void {
    this.boardId = this.route.snapshot.paramMap.get('id') || '';
    if (!this.boardId) { void this.router.navigate(['/learning/whiteboards']); return; }

    this.bindRealtime();
    this.loadBoard();

    // 沉浸式页面：去掉页脚与页底留白（样式见 styles.scss 的 body.kh-board-immersive）
    document.body.classList.add('kh-board-immersive');
    window.addEventListener('resize', this.onWindowResize);
    // 面包屑/主题通常在首帧后完成布局，这里再补一帧校准
    requestAnimationFrame(() => this.fitViewportHeight());
    setTimeout(() => this.fitViewportHeight(), 60);
  }

  /**
   * 让画布房间正好填满可视区。
   *
   * <p>ABP 管理端外壳的顶部高度（顶栏 + 面包屑）随路由与主题变化，写死 `calc(100vh - 76px)`
   * 要么多出空白、要么把右侧聊天的输入框压到屏幕外。这里实测「宿主顶部偏移」后用像素高度撑满，
   * 滚轮缩放画布也就不会误触页面滚动。</p>
   */
  private fitViewportHeight(): void {
    const el = this.host?.nativeElement;
    if (!el) return;
    const top = Math.round(el.getBoundingClientRect().top + window.scrollY);
    const height = Math.max(360, Math.round(window.innerHeight - top));
    if (el.style.height !== `${height}px`) el.style.height = `${height}px`;
  }

  ngOnDestroy(): void {
    this.subs.forEach((s) => s.unsubscribe());
    window.removeEventListener('resize', this.onWindowResize);
    document.body.classList.remove('kh-board-immersive');
    void this.rt.disconnect();
    this.resizeObserver?.disconnect();
    this.stage?.destroy();
  }

  // ── 加载与实时 ─────────────────────────────────────────────

  private loadBoard(): void {
    this.loading.set(true);
    this.api.get(this.boardId).subscribe({
      next: async (d) => {
        this.board.set(d);
        this.myColor.set(d.myColor || '#2f6fed');
        this.myName.set(d.myName || '');
        this.connected.set(false);

        try {
          const joined = await this.rt.join(this.boardId);
          this.myColor.set(joined.myColor || this.myColor());
          this.myName.set(joined.myName || this.myName());
          this.onlineMembers.set(joined.onlineMembers || []);
          this.connected.set(true);

          this.initStage();
          // 服务端全量优先（断线重连/后来者补齐）
          const fromServer = (joined.elements || []).map((e) => this.parse(e.json)).filter(Boolean) as BoardElement[];
          fromServer.forEach((el) => this.upsertLocal(el, false));
          this.layer?.batchDraw();
        } catch (err: any) {
          this.loading.set(false);
          this.message.error(err?.message || '实时连接失败');
        }
        this.loading.set(false);
      },
      error: () => { this.loading.set(false); this.message.error('白板加载失败'); },
    });
  }

  private bindRealtime(): void {
    this.subs.push(
      this.rt.elementUpserted$.subscribe((d) => {
        const el = this.parse(d.json || '{}');
        if (el) { this.upsertLocal(el, false); this.layer?.batchDraw(); }
      }),
      this.rt.elementDeleted$.subscribe((d) => {
        this.removeLocal(d.key);
        this.layer?.batchDraw();
      }),
      this.rt.cursorMoved$.subscribe((c) => this.drawCursor(c)),
      this.rt.presenceJoin$.subscribe((u) => {
        this.onlineMembers.update((list) => [...list.filter((x) => x.userId !== u.userId), { ...u, online: true }]);
        this.message.info(`${u.userName} 已加入协同`);
      }),
      this.rt.presenceLeave$.subscribe((u) => {
        this.onlineMembers.update((list) => list.filter((x) => x.userId !== u.userId));
        this.cursors.get(u.userId)?.destroy();
        this.cursors.delete(u.userId);
        this.cursorLayer?.batchDraw();
      }),
      this.rt.reconnected$.subscribe(() => { void this.rejoin(); }),
    );
  }

  private async rejoin(): Promise<void> {
    try {
      const joined = await this.rt.join(this.boardId);
      this.onlineMembers.set(joined.onlineMembers || []);
      this.connected.set(true);
      this.message.success('已重新连接');
    } catch {
      this.connected.set(false);
    }
  }

  // ── Konva 舞台 ─────────────────────────────────────────────

  private initStage(): void {
    const host = this.stageHost.nativeElement;
    const rect = host.getBoundingClientRect();

    this.stage = new Konva.Stage({
      container: host,
      width: Math.max(320, rect.width),
      height: Math.max(240, rect.height),
      draggable: true,   // 拖空白处平移
    });

    this.layer = new Konva.Layer();
    this.cursorLayer = new Konva.Layer({ listening: false });
    this.stage.add(this.layer, this.cursorLayer);

    this.tr = new Konva.Transformer({
      borderStroke: '#087f7a',
      anchorStroke: '#087f7a',
      anchorSize: 9,
      rotationSnaps: [0, 90, 180, 270],
      // 缩得太小没有意义，还会让文字变竖排/外框变负宽
      boundBoxFunc: (oldBox, newBox) => {
        const min = 40;
        if (Math.abs(newBox.width) < min || Math.abs(newBox.height) < min) {
          return oldBox;
        }
        return newBox;
      },
    });
    this.layer.add(this.tr);

    // 滚轮缩放（以指针为锚点）
    this.stage.on('wheel', (e) => {
      e.evt.preventDefault();
      const oldScale = this.stage!.scaleX();
      const pointer = this.stage!.getPointerPosition();
      if (!pointer) return;
      const k = e.evt.deltaY > 0 ? 1 / 1.08 : 1.08;
      const newScale = Math.min(3, Math.max(0.25, oldScale * k));
      const wp = { x: (pointer.x - this.stage!.x()) / oldScale, y: (pointer.y - this.stage!.y()) / oldScale };
      this.stage!.scale({ x: newScale, y: newScale });
      this.stage!.position({ x: pointer.x - wp.x * newScale, y: pointer.y - wp.y * newScale });
      this.syncGridBackground();
    });

    // 点空白处取消选中
    this.stage.on('click tap', (e) => {
      if (e.target === this.stage) this.select(null);
    });

    // 平移/缩放后同步网格底纹 + 广播视口
    this.stage.on('dragend', (e) => {
      if (e.target === this.stage) {
        this.syncGridBackground();
        void this.rt.viewport(this.boardId, this.stage!.x(), this.stage!.y(), this.stage!.scaleX());
      }
    });

    // 光标上报（50ms 节流）
    this.stage.on('mousemove', () => {
      const now = Date.now();
      if (now - this.lastCursorSent < 50) return;
      this.lastCursorSent = now;
      const p = this.stage!.getRelativePointerPosition();
      if (p) void this.rt.cursor(this.boardId, p.x, p.y);
    });

    // 容器尺寸变化
    this.resizeObserver = new ResizeObserver(() => {
      if (!this.stage) return;
      const r = host.getBoundingClientRect();
      this.stage.size({ width: Math.max(320, r.width), height: Math.max(240, r.height) });
    });
    this.resizeObserver.observe(host);

    this.syncGridBackground();
  }

  private syncGridBackground(): void {
    const host = this.stageHost?.nativeElement;
    if (!host || !this.stage) return;
    const s = this.stage.scaleX() * 24;
    host.style.backgroundSize = `${s}px ${s}px`;
    host.style.backgroundPosition = `${this.stage.x()}px ${this.stage.y()}px`;
  }

  // ── 元素模型 ───────────────────────────────────────────────

  private parse(json: string): BoardElement | null {
    try {
      const o = JSON.parse(json);
      if (!o || !o.id || !o.type) return null;
      return o as BoardElement;
    } catch { return null; }
  }

  /** 本地 upsert：更新模型 + 重建/更新 Konva 节点（remote=true 时不回广播） */
  private upsertLocal(el: BoardElement, broadcast: boolean): void {
    this.elements.set(el.id, el);
    const existing = this.layer?.findOne(`#${this.cssId(el.id)}`) as Konva.Group | undefined;
    if (existing) {
      this.applyAttrs(existing, el);
      this.layer?.batchDraw();
      return;
    }
    this.buildNode(el);
    if (broadcast) void this.rt.upsertElement(this.boardId, el.id, this.typeCode(el.type), el);
  }

  private removeLocal(key: string): void {
    this.elements.delete(key);
    this.layer?.findOne(`#${this.cssId(key)}`)?.destroy();
    if (this.selected()?.id === key) this.select(null);
  }

  /** Konva id 不能以数字开头，统一加前缀并用安全字符 */
  private cssId(key: string): string {
    return 'el-' + key.replace(/[^a-zA-Z0-9_-]/g, '');
  }

  private typeCode(t: BoardElement['type']): number {
    return t === 'text' ? 0 : t === 'note' ? 1 : t === 'image' ? 2 : 3;
  }

  private buildNode(el: BoardElement): void {
    if (!this.layer) return;

    // 文本 / 便签的尺寸一律烘进 w/h：历史上被拉伸过的元素带 sx/sy，
    // 这里折算成真实宽高再清零，保证「边框 = 内容尺寸」，不会再出现文字被整体缩放。
    const isCard = el.type === 'text' || el.type === 'note';
    const baseScaleX = el.sx ?? 1;
    const baseScaleY = el.sy ?? 1;

    const group = new Konva.Group({
      id: this.cssId(el.id),
      x: el.x, y: el.y,
      scaleX: isCard ? 1 : baseScaleX,
      scaleY: isCard ? 1 : baseScaleY,
      rotation: el.rot ?? 0,
      draggable: this.canEdit(),
      name: 'board-el',
    });

    if (el.type === 'text') {
      el.w = Math.max(WHITEBOARD_MIN_TEXT_W, (el.w || 320) * baseScaleX);
      el.sx = 1;
      el.sy = 1;
      group.add(new Konva.Text({
        text: el.text || '',
        fontSize: el.fs || 24,
        fill: el.fill || '#1f2430',
        width: el.w,
        lineHeight: 1.4,
        name: 'card-text',
      }));
    } else if (el.type === 'note') {
      el.w = Math.max(WHITEBOARD_MIN_NOTE_W, (el.w || 200) * baseScaleX);
      el.h = Math.max(WHITEBOARD_MIN_NOTE_H, (el.h || 150) * baseScaleY);
      el.sx = 1;
      el.sy = 1;
      group.add(new Konva.Rect({
        width: el.w, height: el.h, fill: el.bg || '#ffe58a', cornerRadius: 6,
        shadowColor: '#000', shadowBlur: 8, shadowOpacity: 0.18, shadowOffsetY: 3,
        name: 'card-bg',
      }));
      group.add(new Konva.Text({
        x: 12, y: 12, width: el.w - 24, height: el.h - 44,
        text: el.text || '', fontSize: el.fs || 16,
        fill: '#3a2f00', lineHeight: 1.45, wrap: 'word', ellipsis: true,
        name: 'card-text',
      }));
      if (el.by) {
        group.add(new Konva.Text({
          x: 12, y: el.h - 22, width: el.w - 24,
          text: '— ' + el.by, fontSize: 11, fill: 'rgba(58,47,0,.6)',
          name: 'card-by',
        }));
      }
    } else {
      const w = el.w || 260, h = el.h || 170;
      // 占位（图/视频加载前）
      const ph = new Konva.Rect({
        width: w, height: h, fill: 'rgba(136,136,136,.12)', stroke: 'rgba(136,136,136,.35)',
        dash: [6, 4], cornerRadius: 6,
      });
      group.add(ph);

      // AI 生成的配图直接存 URL；课程资源素材才走带 token 的预览地址
      const src = el.url || (el.resourceId ? this.api.resourceUrl(el.resourceId) : '');

      if (el.type === 'image' && src) {
        const img = new Image();
        img.onload = () => {
          ph.destroy();
          group.add(new Konva.Image({ image: img, width: w, height: h }));
          this.layer?.batchDraw();
        };
        img.onerror = () => { this.layer?.batchDraw(); };
        img.src = src;
      } else if (el.type === 'video') {
        const v = document.createElement('video');
        v.src = src;
        v.muted = true; v.loop = true; v.playsInline = true; v.preload = 'auto';
        const icon = new Konva.Group({ x: w / 2, y: h / 2 });
        icon.add(new Konva.Circle({ radius: 26, fill: 'rgba(0,0,0,.55)' }));
        icon.add(new Konva.Text({ x: -7, y: -11, text: '▶', fontSize: 22, fill: '#fff' }));

        v.onloadeddata = () => {
          ph.destroy();
          group.add(new Konva.Image({ image: v, width: w, height: h }));
          group.add(icon);
          this.layer?.batchDraw();
        };
        const anim = new Konva.Animation(() => { /* 每帧重绘以播放视频 */ }, this.layer);
        group.on('click tap', (ev) => {
          ev.cancelBubble = true;
          if (v.paused) { void v.play(); icon.hide(); anim.start(); }
          else { v.pause(); icon.show(); anim.stop(); this.layer?.batchDraw(); }
        });
      }
    }

    // 选中 / 拖动 / 变换
    group.on('click tap', (ev) => {
      ev.cancelBubble = true;
      this.select(el.id);
    });
    group.on('dragstart', () => this.select(el.id));
    group.on('dragend', () => this.commitGeometry(el.id));
    if (isCard) {
      // 拖动缩放锚点时实时把 scale 折成宽/高：文字按新宽度重新换行，字号保持不变
      group.on('transform', () => this.reflowCard(el.id));
    }
    group.on('transformend', () => this.commitGeometry(el.id));

    this.layer.add(group);
    this.tr?.moveToTop();
    this.layer.batchDraw();
  }

  private applyAttrs(node: Konva.Group, el: BoardElement): void {
    node.setAttrs({ x: el.x, y: el.y, rotation: el.rot ?? 0 });

    if (el.type === 'text') {
      // 文本卡片：宽度驱动排版，高度由换行撑开，永远不缩放
      node.scale({ x: 1, y: 1 });
      const textNode = node.findOne('.card-text') as Konva.Text | undefined;
      if (textNode) {
        textNode.text(el.text || '');
        textNode.fontSize(el.fs || textNode.fontSize());
        textNode.fill(el.fill || '#1f2430');
        textNode.width(Math.max(WHITEBOARD_MIN_TEXT_W, el.w || textNode.width()));
        this.autoTextHeight(textNode);
      }
    } else if (el.type === 'note') {
      // 便签：外框宽高由 w/h 决定，正文按宽度重排（字号不变），署名贴底
      node.scale({ x: 1, y: 1 });
      const w = Math.max(WHITEBOARD_MIN_NOTE_W, el.w || 200);
      const h = Math.max(WHITEBOARD_MIN_NOTE_H, el.h || 150);
      const rect = node.findOne('.card-bg') as Konva.Rect | undefined;
      if (rect) {
        rect.width(w);
        rect.height(h);
        if (el.bg) rect.fill(el.bg);
      }
      const textNode = node.findOne('.card-text') as Konva.Text | undefined;
      if (textNode) {
        textNode.text(el.text || '');
        textNode.fontSize(el.fs || textNode.fontSize());
        textNode.width(w - 24);
        textNode.height(h - 44);
      }
      const byNode = node.findOne('.card-by') as Konva.Text | undefined;
      if (byNode) {
        byNode.y(h - 22);
        byNode.width(w - 24);
      }
    } else {
      node.setAttrs({ scaleX: el.sx ?? 1, scaleY: el.sy ?? 1 });
    }

    // 选中元素的边框必须跟着内容一起变
    if (this.selected()?.id === el.id) this.tr?.forceUpdate();
    this.layer?.batchDraw();
  }

  /** 拖动/缩放/旋转结束：把结果折算成模型尺寸，清掉 scale，刷新边框，再广播 */
  private commitGeometry(key: string): void {
    const node = this.layer?.findOne(`#${this.cssId(key)}`) as Konva.Group | undefined;
    const el = this.elements.get(key);
    if (!node || !el) return;

    if (el.type === 'text') {
      // 文本的换行宽度只能从内部 Text 节点读（Group 自身没有 width）
      const textNode = node.findOne('.card-text') as Konva.Text | undefined;
      const width = Math.max(WHITEBOARD_MIN_TEXT_W, (textNode?.width() || el.w || 320) * node.scaleX());
      el.w = Math.round(width);
      el.sx = 1;
      el.sy = 1;
      node.scale({ x: 1, y: 1 });
      if (textNode) {
        textNode.width(el.w);
        this.autoTextHeight(textNode);
      }
    } else if (el.type === 'note') {
      const rect = node.findOne('.card-bg') as Konva.Rect | undefined;
      el.w = Math.round(Math.max(WHITEBOARD_MIN_NOTE_W, (rect?.width() || el.w || 200) * node.scaleX()));
      el.h = Math.round(Math.max(WHITEBOARD_MIN_NOTE_H, (rect?.height() || el.h || 150) * node.scaleY()));
      el.sx = 1;
      el.sy = 1;
      node.scale({ x: 1, y: 1 });
      // 用统一的 applyAttrs 把矩形 / 正文 / 署名一起摆正，避免只改一半
      this.applyAttrs(node, el);
    } else {
      el.sx = node.scaleX();
      el.sy = node.scaleY();
    }
    el.x = Math.round(node.x());
    el.y = Math.round(node.y());
    el.rot = Math.round(node.rotation() * 100) / 100;

    this.elements.set(key, el);
    this.tr?.forceUpdate();
    this.layer?.batchDraw();
    if (this.selected()?.id === key) this.selected.set({ ...el });
    if (this.canEdit()) void this.rt.upsertElement(this.boardId, key, this.typeCode(el.type), el);
  }

  /**
   * 让 Konva.Text 恢复「高度自适应」。
   *
   * <p>Konva 的 `height()` 只接受数字，传 'auto' 会被忽略；直接清掉 height attr，
   * 内部 `_setTextData` 就会按换行后的行数重算高度 —— 文本框高度永远贴合内容。</p>
   */
  private autoTextHeight(textNode: Konva.Text): void {
    textNode.setAttr('height', undefined);
  }

  /**
   * 缩放过程中实时折返 scale（Miro / Figma 的文本卡片行为）：
   * 文本 → 只改变宽度，文字按新宽度重排、高度自动撑开；
   * 便签 → 外框宽高跟着变，正文按宽度重排、字号不变。
   *
   * <p>如果不在 transform 里折返，Konva 会先对整组做 scale，
   * 视觉上就是「文字被拉伸变形、边框和内容对不上」。</p>
   */
  private reflowCard(key: string): void {
    const node = this.layer?.findOne(`#${this.cssId(key)}`) as Konva.Group | undefined;
    const el = this.elements.get(key);
    if (!node || !el) return;

    const scaleX = node.scaleX();
    const scaleY = node.scaleY();

    if (el.type === 'text') {
      const textNode = node.findOne('.card-text') as Konva.Text | undefined;
      if (!textNode) return;
      node.scale({ x: 1, y: 1 });
      textNode.width(Math.max(WHITEBOARD_MIN_TEXT_W, textNode.width() * scaleX));
      this.autoTextHeight(textNode);
    } else if (el.type === 'note') {
      const rect = node.findOne('.card-bg') as Konva.Rect | undefined;
      const textNode = node.findOne('.card-text') as Konva.Text | undefined;
      const byNode = node.findOne('.card-by') as Konva.Text | undefined;
      if (!rect) return;

      node.scale({ x: 1, y: 1 });
      const w = Math.max(WHITEBOARD_MIN_NOTE_W, rect.width() * scaleX);
      const h = Math.max(WHITEBOARD_MIN_NOTE_H, rect.height() * scaleY);
      rect.width(w);
      rect.height(h);
      if (textNode) {
        textNode.width(w - 24);
        textNode.height(h - 44);
      }
      if (byNode) {
        byNode.y(h - 22);
        byNode.width(w - 24);
      }
    }

    this.tr?.forceUpdate();
    this.layer?.batchDraw();
  }

  // ── 交互：选中 / 添加 / 删除 ────────────────────────────────

  select(key: string | null): void {
    if (!this.tr) return;
    if (!key) {
      this.tr.nodes([]);
      this.selected.set(null);
      this.layer?.batchDraw();
      return;
    }
    const node = this.layer?.findOne(`#${this.cssId(key)}`) as Konva.Group | undefined;
    if (!node) return;

    const el = this.elements.get(key) as BoardElement;

    // 文本卡片高度由换行决定：不给上中/下中锚点，避免拖出一个「拉了没反应」的把手
    this.tr.enabledAnchors(
      el.type === 'text'
        ? ['middle-left', 'middle-right', 'top-left', 'top-right', 'bottom-left', 'bottom-right']
        : ['top-left', 'top-center', 'top-right', 'middle-left', 'middle-right', 'bottom-left', 'bottom-center', 'bottom-right'],
    );
    this.tr.keepRatio(false);
    this.tr.nodes([node]);
    this.tr.forceUpdate();
    this.layer?.batchDraw();
    this.selected.set({ ...el });

    // 选中元素 → 直接打开属性页（收起时顺带展开）。
    // 唯一例外：老师正在右侧输入框里打字（聊天 / AI），这时不抢页签，只在「属性」tab 上点亮小圆点。
    if (!this.isTypingInSide()) {
      this.switchSide('props');
    }
  }

  /** 屏幕中心对应的画布坐标 */
  private center(): { x: number; y: number } {
    if (!this.stage) return { x: 120, y: 120 };
    const s = this.stage.scaleX();
    return {
      x: (this.stage.width() / 2 - this.stage.x()) / s - 120 + Math.random() * 40,
      y: (this.stage.height() / 2 - this.stage.y()) / s - 80 + Math.random() * 40,
    };
  }

  private newKey(): string {
    return Date.now().toString(36) + Math.floor(Math.random() * 1e6).toString(36);
  }

  addText(): void {
    const p = this.center();
    const el: BoardElement = {
      id: this.newKey(), type: 'text', ...p, w: 320,
      text: '双击右侧面板编辑文本', fs: 24, fill: '#1f2430',
    };
    this.upsertLocal(el, true);
    this.select(el.id);
  }

  addNote(): void {
    const p = this.center();
    const el: BoardElement = {
      id: this.newKey(), type: 'note', ...p, w: 200, h: 150,
      text: '写下教研想法…', fs: 16, bg: '#ffe58a', by: this.myName(),
    };
    this.upsertLocal(el, true);
    this.select(el.id);
  }

  onTextInput(v: string): void {
    const sel = this.selected();
    if (!sel) return;
    sel.text = v;
    this.elements.set(sel.id, sel);
    const node = this.layer?.findOne(`#${this.cssId(sel.id)}`) as Konva.Group | undefined;
    const t = node?.findOne('.card-text') as Konva.Text | undefined;
    t?.text(v);
    if (sel.type === 'text' && t) this.autoTextHeight(t);
    // 改正文会改变文本高度 → 选中框必须重新贴合内容
    this.tr?.forceUpdate();
    this.layer?.batchDraw();
    this.selected.set({ ...sel });
    void this.rt.upsertElement(this.boardId, sel.id, this.typeCode(sel.type), sel);
  }

  onFontInput(v: number): void {
    const sel = this.selected();
    if (!sel) return;
    sel.fs = Number(v);
    this.elements.set(sel.id, sel);
    const node = this.layer?.findOne(`#${this.cssId(sel.id)}`) as Konva.Group | undefined;
    const t = node?.findOne('.card-text') as Konva.Text | undefined;
    t?.fontSize(sel.fs);
    if (sel.type === 'text' && t) this.autoTextHeight(t);
    // 字号变了，内容高度跟着变 → 同步选中框
    this.tr?.forceUpdate();
    this.layer?.batchDraw();
    this.selected.set({ ...sel });
    void this.rt.upsertElement(this.boardId, sel.id, this.typeCode(sel.type), sel);
  }

  deleteSelected(): void {
    const sel = this.selected();
    if (!sel) return;
    this.removeLocal(sel.id);
    this.select(null);
    this.layer?.batchDraw();
    void this.rt.deleteElement(this.boardId, sel.id);
  }

  // ── AI 助手写回画布 ─────────────────────────────────────────

  /**
   * 把助手产出落到底画布。
   *
   * <p>三种写法：新建文本 / 替换所选 / 追加到所选；图片另走
   * <see cref="insertAssistantImage"/>。全部走 `upsertLocal(..., true)`，
   * 即「本地渲染 + 通过 Hub 广播给其他共创教师」，与手工编辑同一条链路。</p>
   */
  insertAssistantText(text: string): void {
    const content = (text || '').trim();
    if (!content) return;
    if (!this.canEdit()) {
      this.message.warning('你在该白板中是只读成员，无法写入内容');
      return;
    }

    const anchor = this.assistantAnchor();
    const el: BoardElement = {
      id: this.newKey(),
      type: 'text',
      ...anchor,
      w: 460,
      text: content,
      fs: 18,
      fill: '#1f2430',
      aiGenerated: true,
    };
    this.upsertLocal(el, true);
    this.select(el.id);
    this.message.success('已插入到白板');
  }

  /** 替换 / 追加到当前选中的文本（便签） */
  applyAssistantText(text: string, kind: 'replace' | 'append'): void {
    const content = (text || '').trim();
    const sel = this.selected();
    if (!content || !sel) return;
    if (!this.canEdit()) {
      this.message.warning('你在该白板中是只读成员，无法修改内容');
      return;
    }
    if (sel.type !== 'text' && sel.type !== 'note') return;

    sel.text = kind === 'replace' ? content : (sel.text ? `${sel.text}

${content}` : content);
    this.elements.set(sel.id, sel);

    const node = this.layer?.findOne(`#${this.cssId(sel.id)}`) as Konva.Group | undefined;
    const textNode = node?.findOne('.card-text') as Konva.Text | undefined;
    textNode?.text(sel.text);
    if (textNode && sel.type === 'text') this.autoTextHeight(textNode);
    this.tr?.forceUpdate();
    this.layer?.batchDraw();
    this.selected.set({ ...sel });

    void this.rt.upsertElement(this.boardId, sel.id, this.typeCode(sel.type), sel);
    this.message.success(kind === 'replace' ? '已替换所选内容' : '已追加到所选内容');
  }

  /** 把 AI 配图插到画布（直链 URL，不进课程资源） */
  insertAssistantImage(payload: { url: string; prompt: string }): void {
    if (!payload?.url) return;
    if (!this.canEdit()) {
      this.message.warning('你在该白板中是只读成员，无法插入内容');
      return;
    }

    const anchor = this.assistantAnchor();
    const el: BoardElement = {
      id: this.newKey(),
      type: 'image',
      ...anchor,
      w: 360,
      h: 270,
      url: payload.url,
      resourceName: payload.prompt,
      aiGenerated: true,
    };
    this.upsertLocal(el, true);
    this.select(el.id);
    this.message.success('已插入配图');
  }

  /** 新内容放在选中元素下方；没有选中就放在视口中心 */
  private assistantAnchor(): { x: number; y: number } {
    const sel = this.selected();
    if (sel) {
      const height = sel.h || (sel.type === 'text' ? 120 : 160);
      return { x: sel.x, y: sel.y + height + 28 };
    }
    return this.center();
  }

  // ── 远端光标 ───────────────────────────────────────────────

  private drawCursor(c: { userId: string; name: string; color: string; x: number; y: number }): void {
    if (!this.cursorLayer) return;
    let g = this.cursors.get(c.userId);
    if (!g) {
      g = new Konva.Group();
      g.add(new Konva.Line({
        points: [0, 0, 0, 16, 4, 12, 10, 12], closed: true,
        fill: c.color, stroke: '#fff', strokeWidth: 1,
      }));
      const label = new Konva.Label({ x: 10, y: 14 });
      label.add(new Konva.Tag({ fill: c.color, cornerRadius: 4 }));
      label.add(new Konva.Text({ text: c.name, fill: '#fff', fontSize: 11, padding: 3 }));
      g.add(label);
      g.position({ x: c.x, y: c.y });
      this.cursorLayer.add(g);
      this.cursors.set(c.userId, g);
    } else {
      g.to({ x: c.x, y: c.y, duration: 0.4, easing: Konva.Easings.EaseOut });
    }
    this.cursorLayer.batchDraw();
  }

  // ── 资源选择框 ─────────────────────────────────────────────

  openResourcePicker(type: 'image' | 'video'): void {
    this.pickerType = type;
    this.pickerOpen = true;
    this.pickedResource = null;
    this.loadMyCourses();
    this.loadResources();
  }

  private loadMyCourses(): void {
    if (this.myCourses().length) return;
    this.api.getMyCourses().subscribe({
      next: (r) => this.myCourses.set(r.items || []),
      error: () => this.myCourses.set([]),
    });
  }

  loadResources(): void {
    this.resourcesLoading.set(true);
    // 图片=3 / 视频=1（与 ResourceType 枚举一致）
    const typeCode = this.pickerType === 'image' ? 3 : 1;
    this.api.getMyResources(this.pickerKeyword?.trim() || undefined, typeCode, this.pickerCourseId || undefined)
      .subscribe({
        next: (r) => { this.resources.set(r.items || []); this.resourcesLoading.set(false); },
        error: () => { this.resources.set([]); this.resourcesLoading.set(false); },
      });
  }

  resourceThumb(id: string): string { return this.api.resourceUrl(id); }

  insertPickedResource(): void {
    const r = this.pickedResource;
    if (!r) return;
    const p = this.center();
    const isImage = this.pickerType === 'image';
    const el: BoardElement = {
      id: this.newKey(),
      type: isImage ? 'image' : 'video',
      ...p,
      w: isImage ? 280 : 320,
      h: isImage ? 200 : 180,
      resourceId: r.id,
      resourceName: r.name,
      fileExtension: r.fileExtension,
    };
    this.upsertLocal(el, true);
    this.select(el.id);
    this.pickerOpen = false;
    this.pickedResource = null;
  }

  // ── 邀请 ───────────────────────────────────────────────────

  openInvite(): void {
    this.inviteOpen = true;
    this.inviteSelected.clear();
    this.teachersLoading.set(true);
    this.api.getInvitableTeachers(this.boardId).subscribe({
      next: (list) => { this.teachers.set(list || []); this.teachersLoading.set(false); },
      error: () => { this.teachers.set([]); this.teachersLoading.set(false); },
    });
  }

  toggleInvite(userId: string, ev: Event): void {
    const checked = (ev.target as HTMLInputElement).checked;
    if (checked) this.inviteSelected.add(userId); else this.inviteSelected.delete(userId);
  }

  submitInvite(): void {
    const ids = [...this.inviteSelected];
    if (!ids.length) { this.message.warning('请选择要邀请的教师'); return; }
    this.inviting.set(true);
    this.api.invite(this.boardId, ids).subscribe({
      next: (members) => {
        this.inviting.set(false);
        this.inviteOpen = false;
        this.board.update((b) => (b ? { ...b, members, memberCount: members.length } : b));
        this.message.success(`已邀请 ${ids.length} 位教师，对方登录后即可看到此白板`);
      },
      error: (err) => { this.inviting.set(false); this.message.error(err?.error?.error?.message || '邀请失败'); },
    });
  }

  /** 老师是否正在右侧面板的输入框里打字（避免选中元素时把正在写的消息切走） */
  private isTypingInSide(): boolean {
    const active = document.activeElement as HTMLElement | null;
    if (!active || !active.closest('.room__side')) return false;
    return active.tagName === 'TEXTAREA' || active.tagName === 'INPUT';
  }

  // ── 属性面板 ───────────────────────────────────────────────

  typeLabel(t: BoardElement['type']): string {
    return t === 'text' ? '文本' : t === 'note' ? '便签' : t === 'image' ? '图片' : '视频';
  }

  iconOf(t: BoardElement['type']): string {
    return t === 'text' ? 'font-size' : t === 'note' ? 'pushpin' : t === 'image' ? 'picture' : 'play-circle';
  }

  sizeText(el: BoardElement): string {
    const w = Math.round(el.w || 0);
    const h = Math.round(el.h || 0);
    if (el.type === 'text') return `${w} × auto`;
    return `${w} × ${h}`;
  }

  widthOf(el: BoardElement): number {
    return Math.round(el.w || (el.type === 'note' ? 200 : 320));
  }

  heightOf(el: BoardElement): number {
    return Math.round(el.h || 150);
  }

  /** 面板里直接输数值改尺寸（与拖锚点等价：文本改宽度换行，便签改宽高） */
  onSizeInput(axis: 'w' | 'h', value: number | null): void {
    const sel = this.selected();
    if (!sel || value == null || Number.isNaN(value)) return;

    if (axis === 'w') sel.w = Math.max(sel.type === 'note' ? 100 : 80, Math.round(value));
    else sel.h = Math.max(80, Math.round(value));
    this.elements.set(sel.id, sel);

    const node = this.layer?.findOne(`#${this.cssId(sel.id)}`) as Konva.Group | undefined;
    if (node) {
      this.applyAttrs(node, sel);
      this.tr?.forceUpdate();
      this.layer?.batchDraw();
    }
    this.selected.set({ ...sel });
    void this.rt.upsertElement(this.boardId, sel.id, this.typeCode(sel.type), sel);
  }

  // ── 小工具 ─────────────────────────────────────────────────

  initial(name: string): string {
    if (!name) return '师';
    return name.trim().slice(0, 1);
  }

  colorOf(name: string): string {
    const palette = ['#2f6fed', '#e0662f', '#18a058', '#a24bd6', '#d6a21e'];
    let h = 0;
    for (let i = 0; i < (name || '').length; i++) h = (h * 31 + name.charCodeAt(i)) % 997;
    return palette[h % palette.length];
  }

  /** 展开 / 收起整个右侧面板（收起后画布占满，语音与聊天连接不中断） */
  toggleSide(): void {
    this.sideCollapsed.update((v) => !v);
    if (!this.sideCollapsed() && this.sideTab() === 'collab') this.chatUnread.set(0);
  }

  /**
   * 顶部入口（协作 / AI 助手）：点当前已展开的 tab 则收起面板，
   * 否则展开并切到对应 tab。
   */
  headerSideClick(tab: 'collab' | 'ai'): void {
    if (!this.sideCollapsed() && this.sideTab() === tab) {
      this.sideCollapsed.set(true);
      return;
    }
    this.switchSide(tab);
  }

  /** 切换右侧面板 tab；面板收起时点 tab 直接展开 */
  switchSide(tab: 'props' | 'collab' | 'ai'): void {
    this.sideTab.set(tab);
    if (this.sideCollapsed()) this.sideCollapsed.set(false);
    if (tab === 'collab') this.chatUnread.set(0);
  }

  back(): void {
    void this.router.navigate(['/learning/whiteboards']);
  }
}
