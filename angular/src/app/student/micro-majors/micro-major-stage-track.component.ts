import { ChangeDetectionStrategy, Component, computed, input } from '@angular/core';
import { CommonModule } from '@angular/common';
import { MicroMajorEnrollmentStatus } from '../../proxy/micro-majors/enums/micro-major-enrollment-status.enum';
import { MICRO_MAJOR_STAGE_LADDER, microMajorStageIndex } from './micro-major-labels';

/**
 * 微专业「待审核 → 学习中 → 已结业 → 已发证」4 步状态条。
 *
 * 步骤来自后端 MicroMajorEnrollment.Status 的真实映射（见 micro-major-labels.ts），
 * 不使用任何写死的进度值。已取消 / 未报名时整条处于「未开始」形态。
 */
@Component({
  selector: 'app-micro-major-stage-track',
  standalone: true,
  imports: [CommonModule],
  templateUrl: './micro-major-stage-track.component.html',
  styleUrls: ['./micro-major-stage-track.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class MicroMajorStageTrackComponent {
  /** 当前报名状态；null 表示尚未报名 */
  readonly status = input<MicroMajorEnrollmentStatus | null>(null);
  /** 紧凑模式（卡片内使用） */
  readonly compact = input(false);

  readonly steps = MICRO_MAJOR_STAGE_LADDER;

  /** 当前所在步骤（-1 表示尚未开始） */
  readonly currentIndex = computed(() => microMajorStageIndex(this.status()));

  /** 步骤形态：done / current / todo */
  stepClass(index: number): string {
    const current = this.currentIndex();
    if (current < 0) return 'mm-track__step--todo';
    if (index < current) return 'mm-track__step--done';
    if (index === current) return 'mm-track__step--current';
    return 'mm-track__step--todo';
  }

  /** 无报名记录时的提示 */
  readonly emptyHint = computed(() =>
    this.currentIndex() < 0 ? '尚未报名，提交报名后按审核 → 学习 → 结业 → 发证推进。' : '',
  );
}
