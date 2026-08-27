import { Component, ChangeDetectionStrategy, inject, signal, OnInit, OnDestroy } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivatedRoute, Router } from '@angular/router';
import { Subject, takeUntil } from 'rxjs';

interface InfoSection {
  heading: string;
  paragraphs: string[];
  list?: string[];
}

interface InfoPage {
  sections: InfoSection[];
}

const PAGES: Record<string, InfoPage> = {
  about: {
    sections: [
      {
        heading: '平台简介',
        paragraphs: [
          '易课通是在线教学资源库平台，面向教师与学生提供课程、文档、实训、就业等一站式教学服务。平台围绕"以资源为中心、以学习为目标"的理念，将课件、试题、案例、微专业等各类教学素材统一汇聚，配合智能检索与 AI 助手，帮助师生更高效地获取与整理知识。',
        ],
      },
      {
        heading: '核心功能',
        paragraphs: [
          '平台围绕教与学的完整链路，提供以下核心能力：',
        ],
        list: [
          '课程中心：微专业、课程、章节资源的一体化学习路径，支持课件、视频、习题与思维导图；',
          '资源库：统一管理 PDF、Word、PPT、音视频等教学文档，支持分类检索与全文检索；',
          'AI 助手：基于已建索引的教学文档进行问答，答案可溯源、不编造；',
          '智能搜索：对平台资源进行全文语义检索，快速定位所需内容；',
          '实训中心：虚拟仿真与实验实训环境，支持课堂任务发布与学习成果沉淀；',
          '就业服务：简历、招聘直播、就业大厅与就业去向跟踪，为学生提供就业全流程支持。',
        ],
      },
      {
        heading: '我们的理念',
        paragraphs: [
          '我们相信技术应该让教学更简单、让学习更高效。平台始终坚持三个原则：',
        ],
        list: [
          '内容为王：所有 AI 能力都基于平台内真实、已建索引的教学资源，杜绝凭空编造；',
          '师生并重：既服务教师的内容建设与测评需求，也服务学生的自主探究与个性学习；',
          '数据安全：严格遵守数据保护要求，保障师生个人信息与教学数据的安全。',
        ],
      },
    ],
  },
  help: {
    sections: [
      {
        heading: '如何登录平台？',
        paragraphs: [
          '在教学资源库登录页输入学校分配的账号密码即可登录。若登录后提示"租户"选择，请选择你所在院校的租户后再登录。忘记密码时，可通过登录页的"忘记密码"入口找回。',
        ],
      },
      {
        heading: '如何使用 AI 助手？',
        paragraphs: [
          '进入"AI 助手"页面后，先在左侧资源列表选择一份文档（或直接发起通用对话），然后在输入框中输入问题并回车。AI 的回答仅基于已建索引的文档内容，对话记录会自动保存，可随时在左侧"聊天记录"中查看或继续。',
        ],
      },
      {
        heading: '如何检索教学资源？',
        paragraphs: [
          '在"资源库"或"智能搜索"页面输入关键词即可检索。支持按文档类型（PDF、Word、PPT、视频等）与分类筛选；在文档详情中可查看索引状态，只有显示"已索引"的文档才能被 AI 与全文检索使用。',
        ],
      },
      {
        heading: '课程与微专业如何学习？',
        paragraphs: [
          '进入"课程中心"选择课程或微专业，按章节顺序学习即可。每章节可能包含课件、视频、习题、思维导图等资源；学习进度会自动记录，"我的学习"页面可查看整体进度。',
        ],
      },
      {
        heading: '实训与课堂任务如何使用？',
        paragraphs: [
          '"实训"提供仿真实验环境，按实训项目说明完成操作即可。"课堂任务"由教师发布，完成任务并提交后可在列表中查看完成状态。',
        ],
      },
      {
        heading: '遇到问题如何反馈？',
        paragraphs: [
          '如遇到页面异常、资源无法打开或功能报错，请先尝试刷新页面；若问题仍然存在，请联系页面底部的客服或向任课教师反馈，我们会在第一时间处理。',
        ],
      },
    ],
  },
  privacy: {
    sections: [
      {
        heading: '我们收集哪些信息',
        paragraphs: [
          '为提供教学服务，平台会收集以下必要信息：',
        ],
        list: [
          '账号信息：用户名、姓名、角色等身份信息，用于登录、权限管理与学习记录关联；',
          '学习记录：课程学习进度、作业/习题作答、AI 对话记录、收藏与检索行为，用于展示学习进度与提供个性化服务；',
          '设备与日志信息：IP 地址、浏览器类型、访问时间等，用于保障服务安全与稳定性。',
        ],
      },
      {
        heading: '我们如何使用信息',
        paragraphs: [
          '所收集的信息仅用于教学服务目的：提供与维护平台功能、记录与展示学习进度、生成学习分析、保障账号与数据安全，以及在法律法规允许的范围内改进服务质量。',
        ],
      },
      {
        heading: '信息的共享与披露',
        paragraphs: [
          '除以下情形外，我们不会向第三方共享你的个人信息：',
        ],
        list: [
          '经你明确同意或授权；',
          '根据法律法规、行政或司法机关的要求；',
          '为保障平台安全、防止欺诈等所必需的合理情形；',
          '与提供技术支撑的服务商合作，且仅限其完成必要职能。',
        ],
      },
      {
        heading: '信息安全',
        paragraphs: [
          '我们采用加密传输、访问控制、日志审计等技术与管理措施保护信息安全，并对存储的数据进行分级管理。尽管我们尽最大努力保障安全，仍建议你妥善保管账号密码，不在公共场所随意登录。',
        ],
      },
      {
        heading: '你的权利',
        paragraphs: [
          '你有权查询、更正你的个人信息；可以随时删除自己的 AI 对话记录，也可以联系管理员申请删除账户及相关数据。',
        ],
      },
      {
        heading: '政策更新',
        paragraphs: [
          '本政策可能随平台功能与法律法规变化而更新。更新后将在本页面公示，重大变更会通过站内通知等方式提醒。继续使用平台即视为接受更新后的政策。',
        ],
      },
    ],
  },
};

@Component({
  selector: 'app-student-info',
  standalone: true,
  imports: [CommonModule],
  templateUrl: './student-info.component.html',
  styleUrls: ['./student-info.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class StudentInfoComponent implements OnInit, OnDestroy {
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly destroy$ = new Subject<void>();

  readonly page = signal<InfoPage | null>(null);

  ngOnInit(): void {
    this.route.paramMap
      .pipe(takeUntil(this.destroy$))
      .subscribe(params => {
        const key = params.get('type') ?? '';
        const page = PAGES[key];
        if (!page) {
          this.router.navigate(['/student']);
          return;
        }
        this.page.set(page);
      });
  }

  ngOnDestroy(): void {
    this.destroy$.next();
    this.destroy$.complete();
  }
}