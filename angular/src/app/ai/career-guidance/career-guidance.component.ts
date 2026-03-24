import { Component, signal, inject, ChangeDetectionStrategy } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { NzInputModule } from 'ng-zorro-antd/input';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzCardModule } from 'ng-zorro-antd/card';
import { NzFormModule } from 'ng-zorro-antd/form';
import { NzSelectModule } from 'ng-zorro-antd/select';
import { NzDividerModule } from 'ng-zorro-antd/divider';
import { NzCollapseModule } from 'ng-zorro-antd/collapse';
import { NzDescriptionsModule } from 'ng-zorro-antd/descriptions';
import { NzTagModule } from 'ng-zorro-antd/tag';
import { NzProgressModule } from 'ng-zorro-antd/progress';
import { NzSkeletonModule } from 'ng-zorro-antd/skeleton';
import { NzTableModule } from 'ng-zorro-antd/table';
import { NzTimelineModule } from 'ng-zorro-antd/timeline';
import { NzMessageService } from 'ng-zorro-antd/message';
import { ChatService } from '../services/chat.service';

interface StudentProfile {
  major: string;
  grade: string;
  gpa: number | null;
  skills: string;
  interests: string;
  projects: string;
  certifications: string;
  careerGoal: string;
}

interface CareerAdvice {
  assessment: {
    strengths: string[];
    areasForImprovement: string[];
    interests: string[];
    careerMatchScore: number;
  };
  recommendedPaths: {
    title: string;
    description: string;
    matchScore: number;
    requiredSkills: string[];
    salaryRange: string;
    growthPotential: string;
  }[];
  skillGaps: {
    skill: string;
    currentLevel: string;
    targetLevel: string;
    priority: string;
  }[];
  actionPlan: {
    id: string;
    title: string;
    description: string;
    timeline: string;
    priority: string;
  }[];
  nextSteps: string[];
}

@Component({
  selector: 'app-career-guidance',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    NzInputModule,
    NzButtonModule,
    NzCardModule,
    NzFormModule,
    NzSelectModule,
    NzDividerModule,
    NzCollapseModule,
    NzDescriptionsModule,
    NzTagModule,
    NzProgressModule,
    NzSkeletonModule,
    NzTableModule,
    NzTimelineModule
  ],
  templateUrl: './career-guidance.component.html',
  styleUrls: ['./career-guidance.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class CareerGuidanceComponent {
  private readonly chatService = inject(ChatService);
  private readonly message = inject(NzMessageService);
  
  profile = signal<StudentProfile>({
    major: '',
    grade: '',
    gpa: null,
    skills: '',
    interests: '',
    projects: '',
    certifications: '',
    careerGoal: ''
  });
  
  result = signal<CareerAdvice | null>(null);
  isLoading = signal(false);
  
  readonly majors = [
    '计算机科学与技术',
    '软件工程',
    '数据科学与大数据技术',
    '人工智能',
    '电子信息工程',
    '通信工程',
    '自动化',
    '机械工程',
    '电气工程',
    '工商管理',
    '金融学',
    '会计学',
    '市场营销',
    '经济学',
    '法学',
    '英语',
    '数学与应用数学',
    '物理学',
    '化学',
    '生物科学',
    '其他'
  ];
  
  readonly grades = [
    '大一',
    '大二',
    '大三',
    '大四',
    '研一',
    '研二',
    '研三',
    '已毕业'
  ];
  
  generate(): void {
    const profile = this.profile();
    if (!profile.major || !profile.grade) {
      this.message.warning('请填写专业和年级');
      return;
    }
    
    this.isLoading.set(true);
    this.result.set(null);
    
    const prompt = this.buildPrompt();
    let fullResponse = '';
    
    this.chatService.chat({ message: prompt, agent: 'career-guidance' }).subscribe({
      next: (chunk) => {
        if (chunk.content) {
          fullResponse += chunk.content;
          this.tryParseResult(fullResponse);
        }
      },
      error: (err) => {
        console.error('Error generating career advice:', err);
        this.message.error('生成失败，请重试');
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
        const parsed = JSON.parse(jsonMatch[0]) as CareerAdvice;
        this.result.set(parsed);
      }
    } catch {
      // JSON not yet complete
    }
  }
  
  private buildPrompt(): string {
    const p = this.profile();
    return `请根据以下学生档案提供职业规划建议：

专业: ${p.major}
年级: ${p.grade}
GPA: ${p.gpa || '未提供'}
技能: ${p.skills || '未提供'}
兴趣: ${p.interests || '未提供'}
项目经验: ${p.projects || '未提供'}
证书: ${p.certifications || '未提供'}
职业目标: ${p.careerGoal || '未提供'}

请以JSON格式输出建议，包含以下字段:
{
  "assessment": {
    "strengths": ["优势1"],
    "areasForImprovement": ["待提升1"],
    "interests": ["兴趣方向1"],
    "careerMatchScore": 85
  },
  "recommendedPaths": [
    {
      "title": "职业方向",
      "description": "描述",
      "matchScore": 90,
      "requiredSkills": ["技能1"],
      "salaryRange": "薪资范围",
      "growthPotential": "发展潜力描述"
    }
  ],
  "skillGaps": [
    {
      "skill": "技能名",
      "currentLevel": "当前水平",
      "targetLevel": "目标水平",
      "priority": "高/中/低"
    }
  ],
  "actionPlan": [
    {
      "id": "1",
      "title": "行动项标题",
      "description": "描述",
      "timeline": "时间线",
      "priority": "优先级"
    }
  ],
  "nextSteps": ["下一步1"]
}

只输出JSON，不要其他内容。`;
  }
  
  reset(): void {
    this.profile.set({
      major: '',
      grade: '',
      gpa: null,
      skills: '',
      interests: '',
      projects: '',
      certifications: '',
      careerGoal: ''
    });
    this.result.set(null);
  }
  
  getPriorityColor(priority: string): string {
    switch (priority) {
      case '高': return 'red';
      case '中': return 'orange';
      case '低': return 'green';
      default: return 'default';
    }
  }
}
