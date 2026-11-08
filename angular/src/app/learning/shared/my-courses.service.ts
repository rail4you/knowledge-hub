import { Injectable, inject } from '@angular/core';
import { ConfigStateService } from '@abp/ng.core';
import type { Observable } from 'rxjs';
import { map } from 'rxjs/operators';
import type { CourseDto } from '../../proxy/courses/dtos/models';
import { ManagedCourseService } from '../course-list/managed-course.service';
import { getCurrentUserRoles } from '../../auth/current-user.utils';

/** 管理员角色：可看本校全部课程（不受「只看自己的」限制）。 */
const ADMIN_ROLES = ['admin', 'SchoolAdmin', 'LeagueAdmin'];

/**
 * 「课程内容」导航下各页面统一的课程下拉数据源。
 *
 * <p>
 * 口径：<b>教师只列自己创建的课程</b>（`TeacherId / CreatorId` = 当前用户），
 * 院校 / 联盟 / 平台管理员列本校全部课程。
 * 章节管理、章节习题、课程资源、章节资源、教案管理、案例库等页面都用它，
 * 避免各页各写一套 `courseService.getList()` 把别人的课程也列出来。
 * </p>
 */
@Injectable({ providedIn: 'root' })
export class MyCoursesService {
  private readonly managedCourseService = inject(ManagedCourseService);
  private readonly configState = inject(ConfigStateService);

  /** 当前用户是否管理员（可跨教师管理课程）。 */
  isCourseAdmin(): boolean {
    const roles = getCurrentUserRoles(this.configState);
    return ADMIN_ROLES.some(r => roles.includes(r));
  }

  /**
   * 取课程列表：教师=自己创建的；管理员=本校全部。
   * @param maxResultCount 最多取多少门（默认 300）
   */
  getCourses(maxResultCount = 300): Observable<CourseDto[]> {
    return this.managedCourseService
      .getList({
        onlyMine: this.isCourseAdmin() ? undefined : true,
        skipCount: 0,
        maxResultCount,
      })
      .pipe(map(res => (res.items || []) as CourseDto[]));
  }
}
