#!/usr/bin/env node
/**
 * 修正 `abp generate-proxy` 生成的代理文件中的已知问题。
 *
 * <p>ABP CLI 2.2.1 的生成器有四处会产出不可编译 / 覆盖手工代码的结果，
 * 每次重新生成代理后都需要修复。这个脚本把修复固化为可重复执行的步骤，
 * 避免"生成完还要手工打补丁"这种隐性流程。</p>
 *
 * <p>用法：</p>
 * <pre>
 *   cd angular && abp generate-proxy -t ng -u https://localhost:44305
 *   node scripts/fix-generated-proxy.mjs
 * </pre>
 *
 * <p>幂等：已是目标状态时不做任何改动。</p>
 */
import { readFileSync, writeFileSync } from 'node:fs';
import { join, dirname } from 'node:path';
import { fileURLToPath } from 'node:url';

const here = dirname(fileURLToPath(import.meta.url));
const proxyRoot = join(here, '..', 'src', 'app', 'proxy');

/** 读文件，不存在返回 null */
function read(relPath) {
  try {
    return readFileSync(join(proxyRoot, relPath), 'utf8');
  } catch {
    return null;
  }
}

/** 写文件（仅在内容变化时写） */
function write(relPath, content) {
  const abs = join(proxyRoot, relPath);
  const before = read(relPath);
  if (before === content) {
    console.log(`  · ${relPath} 已是目标状态，跳过`);
    return false;
  }
  writeFileSync(abs, content, 'utf8');
  console.log(`  ✓ ${relPath} 已修复`);
  return true;
}

const fixes = [];

/* ------------------------------------------------------------------
 * ① users/user.service.ts —— 补回被生成器覆盖的手写 changePassword
 *
 * ABP Account 模块的 ProfileController 不在业务模块的 api-definition 里，
 * 生成器永远产不出这个方法，只能手写；重新生成时会被覆盖掉。
 * ------------------------------------------------------------------ */
{
  const path = 'users/user.service.ts';
  const src = read(path);
  if (src && !src.includes('changePassword')) {
    const patched = src
      .replace(
        "import type { MyProfileDto, UpdateMyProfileDto } from './models';",
        "import type { MyProfileDto, UpdateMyProfileDto, ChangeMyPasswordDto } from './models';"
      )
      // 在类结尾的最后一个 "}" 之前插入方法
      .replace(
        /\n\}\s*$/,
        `

  /// 修改当前用户密码（ABP Account ProfileController：POST api/account/my-profile/change-password）
  changePassword = (input: ChangeMyPasswordDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, void>({
      method: 'POST',
      url: '/api/account/my-profile/change-password',
      body: input,
    },
    { apiName: this.apiName,...config });
}
`
      );
    if (patched !== src) {
      write(path, patched);
      fixes.push('users/user.service.ts: changePassword');
    }
  }
}

/* ------------------------------------------------------------------
 * ② users/models.ts —— 补回 ChangeMyPasswordDto（同样被覆盖）
 * ------------------------------------------------------------------ */
{
  const path = 'users/models.ts';
  const src = read(path);
  if (src && !src.includes('ChangeMyPasswordDto')) {
    const patched = src.replace(
      /(export interface UpdateMyProfileDto \{[\s\S]*?\n\}\n)/,
      `$1
/** 修改密码请求体（Volo.Abp.Account.ChangePasswordInput） */
export interface ChangeMyPasswordDto {
  currentPassword?: string;
  newPassword?: string;
}
`
    );
    if (patched !== src) {
      write(path, patched);
      fixes.push('users/models.ts: ChangeMyPasswordDto');
    }
  }
}

/* ------------------------------------------------------------------
 * ③ teaching-agents/agent-run.service.ts —— 流式端点的返回类型被生成成
 *    非法的 `any<AgentMessageChunkDto>`（TypeScript 解析为泛型实参，直接语法错误）
 * ------------------------------------------------------------------ */
{
  const path = 'teaching-agents/agent-run.service.ts';
  const src = read(path);
  if (src && src.includes('any<AgentMessageChunkDto>')) {
    write(path, src.replace(/any<AgentMessageChunkDto>/g, 'AgentMessageChunkDto'));
    fixes.push('teaching-agents/agent-run.service.ts: any<T> 泛型');
  }
}

/* ------------------------------------------------------------------
 * ④ microsoft/asp-net-core/http/models.ts —— 生成器把 headers 的值类型
 *    写成 StringValues 并未生成对应 import，编译报 "Cannot find name"。
 *    该类型在本项目的 Angular 版本里并不从 @angular/common/http 导出，
 *    直接回退成 string（与生成器产物的实际用途一致：表单文件头是字符串字典）。
 * ------------------------------------------------------------------ */
{
  const path = 'microsoft/asp-net-core/http/models.ts';
  const src = read(path);
  if (src && src.includes('StringValues')) {
    write(
      path,
      src
        .replace(/^import \{ StringValues \} from '@angular\/common\/http';\n/m, '')
        .replace(/Record<string, StringValues>/g, 'Record<string, string>')
    );
    fixes.push('microsoft/asp-net-core/http/models.ts: StringValues → string');
  }
}

console.log('\n代理生成后修复完成：' + (fixes.length ? `\n  - ${fixes.join('\n  - ')}` : '无需修复'));
