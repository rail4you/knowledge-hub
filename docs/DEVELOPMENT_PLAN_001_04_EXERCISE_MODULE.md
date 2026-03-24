# 习题模块开发计划

## 文档信息

| 项目 | 内容 |
|------|------|
| 文档编号 | DEVELOPMENT_PLAN_001_04 |
| 所属系统 | KnowledgeHub |
| 开发阶段 | Phase 3: 习题模块 |
| 最后更新 | 2026-03-25 |

---

## 1. 模块概述

### 1.1 功能目标

实现课程的习题管理功能，支持多种题型（单选、多选、判断、填空、简答、案例分析），支持 AI 生成习题和自动批改。

### 1.2 需求对照

| 需求编号 | 需求描述 | 实现状态 |
|----------|----------|----------|
| 4 | 章节测试与反馈：多题型测试、自动批改、测试报告 | 进行中 |

---

## 2. 技术准备

### 2.1 已有的后端实体

```
src/KnowledgeHub.Domain/Exams/
├── Exercise.cs           ✅ 习题实体
├── Exam.cs              ✅ 考试实体
├── ExamExercise.cs       ✅ 考试习题关联
├── StudentExam.cs        ✅ 学生考试记录
├── StudentAnswer.cs      ✅ 学生答案
└── Enums/
    ├── ExerciseType.cs   ✅ 习题类型枚举
    ├── ExamType.cs      ✅ 考试类型枚举
    └── ExamStatus.cs    ✅ 考试状态枚举
```

### 2.2 Exercise 实体字段

```csharp
public class Exercise : FullAuditedEntity<Guid>, IMultiTenant
{
    public Guid? TenantId { get; set; }
    public Guid CourseId { get; set; }
    public Guid? ChapterId { get; set; }
    public Guid? KnowledgeResourceId { get; set; }
    public string Title { get; set; }
    public string QuestionContent { get; set; }
    public ExerciseType Type { get; set; }  // SingleChoice, MultiChoice, TrueFalse, FillBlank, ShortAnswer, Essay, CaseAnalysis
    public string? Options { get; set; }    // JSON 格式存储选项
    public string Answer { get; set; }
    public string? AnswerExplanation { get; set; }
    public int Difficulty { get; set; } = 1
    public int Score { get; set; } = 1
    public bool IsAiGenerated { get; set; }
}
```

### 2.3 习题类型

```csharp
public enum ExerciseType
{
    SingleChoice = 0,    // 单选题
    MultiChoice = 1,     // 多选题
    TrueFalse = 2,       // 判断题
    FillBlank = 3,       // 填空题
    ShortAnswer = 4,     // 简答题
    Essay = 5,          // 论述题
    CaseAnalysis = 6    // 案例分析题
}
```

### 2.4 API 端点

| 方法 | 端点 | 说明 |
|------|------|------|
| GET | /api/app/exercise | 获取习题列表 |
| GET | /api/app/exercise/{id} | 获取习题详情 |
| POST | /api/app/exercise | 创建习题 |
| PUT | /api/app/exercise/{id} | 更新习题 |
| DELETE | /api/app/exercise/{id} | 删除习题 |
| GET | /api/app/exercise/by-course/{courseId} | 获取课程的习题 |
| GET | /api/app/exercise/by-chapter/{chapterId} | 获取章节的习题 |

---

## 3. 开发任务

### 3.1 后端任务

#### T4.1: ExerciseAppService 实现 ✅ 已完成

**文件**：`src/KnowledgeHub.Application/Exams/ExerciseAppService.cs`

**功能**：
- CRUD 操作
- 按课程/章节获取习题
- 手动映射（ObjectMapper 问题）

#### T4.2: 数据库迁移 ✅ 已完成

**迁移文件**：`AddExamTables`

**创建的表**：
- `AppExercises` - 习题表
- `AppExams` - 考试表
- `AppExamExercises` - 考试习题关联表
- `AppStudentExams` - 学生考试记录表
- `AppStudentAnswers` - 学生答案表

### 3.2 前端任务

#### T4.3: 习题列表组件

**文件**：`angular/src/app/learning/exercise/exercise-list.component.ts`

**功能**：
- 显示课程/章节的习题列表
- 按类型筛选
- 创建/编辑/删除习题
- 分页支持

#### T4.4: 习题编辑组件

**文件**：`angular/src/app/learning/exercise/exercise-form.component.ts`

**功能**：
- 单选题/多选题表单（选项配置）
- 判断题表单
- 填空题表单
- 简答/论述题表单
- 题目内容编辑器

#### T4.5: 习题练习组件

**文件**：`angular/src/app/learning/exercise/exercise-practice.component.ts`

**功能**：
- 答题界面
- 答案提交
- 自动评分（客观题）
- 答案解析显示

---

## 4. 组件设计

### 4.1 习题列表组件

```typescript
@Component({
  selector: 'app-exercise-list',
  template: `
    <nz-card nzTitle="习题管理">
      <div class="filters">
        <nz-select [(ngModel)]="selectedType" (ngModelChange)="filterByType()">
          <nz-option [nzValue]="null" nzLabel="全部类型"></nz-option>
          <nz-option [nzValue]="0" nzLabel="单选题"></nz-option>
          <nz-option [nzValue]="1" nzLabel="多选题"></nz-option>
          <nz-option [nzValue]="2" nzLabel="判断题"></nz-option>
          <nz-option [nzValue]="3" nzLabel="填空题"></nz-option>
        </nz-select>
        <button nz-button nzType="primary" (click)="createExercise()">
          创建习题
        </button>
      </div>
      <nz-table [nzData]="exercises"></nz-table>
    </nz-card>
  `
})
export class ExerciseListComponent { }
```

### 4.2 题型配置

| 题型 | 选项 | 答案格式 | 自动批改 |
|------|------|----------|----------|
| 单选题 | 4个选项 | A/B/C/D | ✅ |
| 多选题 | 多个选项 | ABCD | ✅ |
| 判断题 | - | true/false | ✅ |
| 填空题 | - | 文本 | ❌ (需AI) |
| 简答题 | - | 文本 | ❌ (需AI) |
| 论述题 | - | 长文本 | ❌ (需AI) |
| 案例分析 | - | 长文本 | ❌ (需AI) |

---

## 5. API 调用

### 5.1 获取课程习题

```typescript
const exercises = await this.exerciseService.getByCourse(courseId);
```

### 5.2 创建习题

```typescript
const exercise = await this.exerciseService.create({
  courseId: 'xxx',
  chapterId: 'xxx',
  title: '习题标题',
  questionContent: '题目内容',
  type: ExerciseType.SingleChoice,
  options: JSON.stringify([
    { key: 'A', content: '选项1' },
    { key: 'B', content: '选项2' },
    { key: 'C', content: '选项3' },
    { key: 'D', content: '选项4' }
  ]),
  answer: 'A',
  difficulty: 2,
  score: 5
});
```

---

## 6. 验收标准

### 6.1 后端验收

- [x] ExerciseAppService 实现完成
- [x] 数据库迁移创建成功
- [ ] API 端点测试通过

### 6.2 前端验收

- [ ] 习题列表页面显示正确
- [ ] 可以创建单选题/多选题
- [ ] 可以创建判断题/填空题
- [ ] 习题练习页面可答题
- [ ] 客观题自动评分正确

---

## 7. 更新记录

| 日期 | 版本 | 更新内容 | 作者 |
|------|------|----------|------|
| 2026-03-25 | 1.0 | 初始版本 | AI |
| 2026-03-25 | 1.1 | 完成 ExerciseAppService 和数据库迁移 | AI |
