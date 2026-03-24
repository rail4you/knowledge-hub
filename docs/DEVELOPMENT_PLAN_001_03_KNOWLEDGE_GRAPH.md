# 图谱模块开发计划

## 文档信息

| 项目 | 内容 |
|------|------|
| 文档编号 | DEVELOPMENT_PLAN_001_03 |
| 所属系统 | KnowledgeHub |
| 开发阶段 | Phase 1.5: 图谱模块 |
| 最后更新 | 2026-03-25 |

---

## 1. 模块概述

### 1.1 功能目标

基于课程章节数据，展示三种知识图谱可视化：

1. **章节树状图** (Tree Graph) - 展示课程的章节层级结构
2. **思维导图** (Mind Map) - 展示知识点之间的关联和依赖
3. **知识网络图** (Knowledge Network) - 展示知识点之间的复杂关系

### 1.2 需求对照

| 需求编号 | 需求描述 | 实现状态 |
|----------|----------|----------|
| 2 | 可视化知识图谱：知识点关联查询、个性化学习路径推荐 | 进行中 |

---

## 2. 技术准备

### 2.1 已有的依赖

- **ECharts 6.x** - 已在 `angular/package.json` 中定义
- **@types/echarts** - 类型定义
- **progress-dashboard.component** - 已使用 ECharts 实现柱状图

### 2.2 现有代码参考

```typescript
// 参考: angular/src/app/learning/progress/progress-dashboard.component.ts
import * as echarts from 'echarts';
import type { EChartsOption } from 'echarts/index';

// 初始化图表
this.chart = echarts.init(container);
this.chart.setOption({ ... });
```

### 2.3 章节数据结构

```typescript
interface ChapterDto {
  id: string;
  courseId: string;
  parentId: string | null;  // 父章节ID，null表示顶级
  title: string;
  description?: string;
  sortOrder: number;
  children: ChapterDto[];  // 子章节
  knowledgeResources: KnowledgeResourceDto[];
}

interface KnowledgeResourceDto {
  id: string;
  name: string;
  description?: string;
  importanceLevel: number;  // 重要程度 1-5
  difficulty: number;        // 难度等级 1-5
}
```

### 2.4 API端点

```
GET /api/app/chapter/chapter-tree/{courseId}  # 获取章节树
GET /api/app/course/{id}                      # 获取课程详情（含章节）
```

---

## 3. 开发任务

### 3.1 创建图谱路由

**文件**: `angular/src/app/app.routes.ts`

```typescript
{
  path: 'learning',
  children: [
    // ... 现有路由
    {
      path: 'knowledge-graph/:courseId',
      loadComponent: () => import('./learning/knowledge-graph/knowledge-graph.component')
        .then(c => c.KnowledgeGraphComponent),
    },
  ],
},
```

### 3.2 添加菜单项

**文件**: `angular/src/app/route.provider.ts`

```typescript
{
  path: '/learning/knowledge-graph/:courseId',
  name: '::Menu:KnowledgeGraph',
  parentName: '::Menu:Learning',
  layout: eLayoutType.application,
},
```

### 3.3 创建图谱组件

**目录**: `angular/src/app/learning/knowledge-graph/`

**文件结构**:
```
knowledge-graph/
├── knowledge-graph.component.ts      # 主组件（三个Tab切换）
├── chapter-tree-graph.component.ts    # 章节树状图
├── mind-map-graph.component.ts        # 思维导图
└── knowledge-network-graph.component.ts # 知识网络图
```

---

## 4. 图谱类型设计

### 4.1 章节树状图 (Chapter Tree Graph)

**数据结构**:
```typescript
{
  type: 'tree',
  data: [{
    name: '第一章：Python简介',
    children: [
      { name: '1.1 Python历史' },
      { name: '1.2 环境搭建' }
    ]
  }]
}
```

**特性**:
- 横向树状布局
- 点击节点可展开/折叠
- 鼠标悬停显示章节描述
- 双击节点进入该章节学习

### 4.2 思维导图 (Mind Map)

**数据结构**:
```typescript
{
  type: 'tree',
  data: [{
    name: '课程中心',
    children: [
      {
        name: '第一章',
        children: [
          { name: '知识点A' },
          { name: '知识点B' }
        ]
      }
    ]
  }]
}
```

**特性**:
- 从中心向四周发散
- 不同层级使用不同颜色
- 节点大小表示重要程度
- 支持拖拽调整位置

### 4.3 知识网络图 (Knowledge Network)

**数据结构**:
```typescript
{
  nodes: [
    { id: '1', name: '变量', symbolSize: 30 },
    { id: '2', name: '数据类型', symbolSize: 25 },
    { id: '3', name: '运算符', symbolSize: 20 }
  ],
  edges: [
    { source: '1', target: '2' },
    { source: '1', target: '3' }
  ]
}
```

**特性**:
- 力导向布局（force-directed）
- 节点大小表示难度或重要程度
- 连线粗细表示关联强度
- 支持拖拽节点
- 点击节点显示详情

---

## 5. 主组件设计

### 5.1 KnowledgeGraphComponent

```typescript
@Component({
  selector: 'app-knowledge-graph',
  template: `
    <nz-card>
      <nz-tabs>
        <nz-tab nzTitle="章节树">
          <app-chapter-tree-graph [courseId]="courseId" />
        </nz-tab>
        <nz-tab nzTitle="思维导图">
          <app-mind-map-graph [courseId]="courseId" />
        </nz-tab>
        <nz-tab nzTitle="知识网络">
          <app-knowledge-network-graph [courseId]="courseId" />
        </nz-tab>
      </nz-tabs>
    </nz-card>
  `
})
export class KnowledgeGraphComponent {
  courseId = signal('');
}
```

---

## 6. API 调用

### 6.1 获取章节树

```typescript
// 在各图谱组件中调用
const chapters = await this.chapterService.getChapterTree(courseId);
```

### 6.2 数据转换

每个图谱组件需要将 `ChapterDto[]` 转换为对应 ECharts 需要的格式。

---

## 7. 验收标准

### 7.1 功能验收

- [ ] 访问 `/learning/knowledge-graph/:courseId` 显示图谱页面
- [ ] 三个 Tab 可以切换显示不同图谱
- [ ] 章节树状图正确显示层级结构
- [ ] 思维导图以中心发散方式展示
- [ ] 知识网络图显示知识点之间的连接

### 7.2 交互验收

- [ ] 图谱支持缩放和平移
- [ ] 节点悬停显示提示信息
- [ ] 章节树支持展开/折叠
- [ ] 知识网络支持拖拽节点

### 7.3 性能验收

- [ ] 10章 × 10知识点的课程，图谱渲染 < 1秒
- [ ] 缩放/平移操作流畅（60fps）

---

## 8. 依赖项

| 依赖 | 版本 | 用途 |
|------|------|------|
| echarts | ^6.0.0 | 图表库 |
| @types/echarts | ^4.9.22 | 类型定义 |
| ng-zorro-antd | ^21.0.2 | UI组件库 |

---

## 9. 更新记录

| 日期 | 版本 | 更新内容 | 作者 |
|------|------|----------|------|
| 2026-03-25 | 1.0 | 初始版本 | AI |
