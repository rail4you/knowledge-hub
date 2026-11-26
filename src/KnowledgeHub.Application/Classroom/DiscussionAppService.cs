using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using KnowledgeHub.Classroom.Dtos;
using KnowledgeHub.Learning;
using KnowledgeHub.Learning.Enums;
using KnowledgeHub.Permissions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Data;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Users;

namespace KnowledgeHub.Classroom;

/// <summary>
/// 课程讨论答疑。
/// <para>
/// 权限分工：读 = 登录即可（学生要看自己课程的答疑）；
/// 发帖 / 回复 = <c>Classroom.Participate</c>（学生 + 教师都授）；
/// 置顶 / 标记解决 / 采纳 / 删他人帖 = <c>Classroom.Discussion</c>。
/// </para>
/// </summary>
[IgnoreAntiforgeryToken]
[Authorize]
public class DiscussionAppService : KnowledgeHubAppService, IDiscussionAppService
{
    private readonly IRepository<Discussion, Guid> _discussionRepository;
    private readonly IRepository<DiscussionReply, Guid> _replyRepository;
    private readonly ClassroomSupport _support;
    private readonly IStudentActivityLogger _activityLogger;
    private readonly IDataFilter _dataFilter;
    private readonly ICurrentUser _currentUser;

    public DiscussionAppService(
        IRepository<Discussion, Guid> discussionRepository,
        IRepository<DiscussionReply, Guid> replyRepository,
        ClassroomSupport support,
        IStudentActivityLogger activityLogger,
        IDataFilter dataFilter,
        ICurrentUser currentUser)
    {
        _discussionRepository = discussionRepository;
        _replyRepository = replyRepository;
        _support = support;
        _activityLogger = activityLogger;
        _dataFilter = dataFilter;
        _currentUser = currentUser;
    }

    public async Task<PagedResultDto<DiscussionDto>> GetListAsync(GetDiscussionListInput input)
    {
        var me = _currentUser.Id;
        var keyword = input.Filter?.Trim();

        List<Discussion> items;
        int total;
        using (_dataFilter.Disable<IMultiTenant>())
        {
            var query = await _discussionRepository.GetQueryableAsync();
            query = query
                .WhereIf(input.CourseId.HasValue, x => x.CourseId == input.CourseId!.Value)
                .WhereIf(input.ChapterId.HasValue, x => x.ChapterId == input.ChapterId!.Value)
                .WhereIf(input.OnlyUnresolved, x => !x.IsResolved)
                .WhereIf(input.OnlyMine && me.HasValue, x => x.AuthorId == me!.Value)
                .WhereIf(!string.IsNullOrWhiteSpace(keyword),
                    x => x.Title.Contains(keyword!) || x.Content.Contains(keyword!));

            total = await AsyncExecuter.CountAsync(query);
            items = await AsyncExecuter.ToListAsync(
                query
                    .OrderByDescending(x => x.IsPinned)
                    .ThenByDescending(x => x.LastReplyAt ?? x.CreationTime)
                    .Skip(input.SkipCount)
                    .Take(input.MaxResultCount));
        }

        return new PagedResultDto<DiscussionDto>(total, items.Select(MapToDto).ToList());
    }

    public async Task<DiscussionDto> GetAsync(Guid id)
    {
        var discussion = await _discussionRepository.GetAsync(id);
        discussion.IncreaseView();
        await _discussionRepository.UpdateAsync(discussion);

        List<DiscussionReply> replies;
        using (_dataFilter.Disable<IMultiTenant>())
        {
            var query = await _replyRepository.GetQueryableAsync();
            replies = await AsyncExecuter.ToListAsync(
                query.Where(x => x.DiscussionId == id).OrderBy(x => x.CreationTime));
        }

        var dto = MapToDto(discussion);
        dto.Replies = replies.Select(MapReply).ToList();
        return dto;
    }

    [Authorize(KnowledgeHubPermissions.Classroom.Participate)]
    public async Task<DiscussionDto> CreateAsync(CreateDiscussionInput input)
    {
        if (string.IsNullOrWhiteSpace(input.Title))
        {
            throw new UserFriendlyException("请填写问题标题。");
        }

        if (string.IsNullOrWhiteSpace(input.Content))
        {
            throw new UserFriendlyException("请填写问题描述。");
        }

        await _support.EnsureCanParticipateAsync(input.CourseId);

        var authorId = _support.CurrentUserIdOrThrow();
        var discussion = new Discussion(
            GuidGenerator.Create(),
            input.CourseId,
            input.Title.Trim(),
            input.Content.Trim(),
            authorId)
        {
            ChapterId = input.ChapterId,
            KnowledgeResourceId = input.KnowledgeResourceId,
            AuthorName = await _support.GetDisplayNameAsync(authorId)
        };

        await _discussionRepository.InsertAsync(discussion);

        await _activityLogger.LogAsync(
            authorId,
            StudentActivityAction.PostDiscussion,
            input.CourseId,
            "discussion",
            discussion.Id);

        return MapToDto(discussion);
    }

    [Authorize(KnowledgeHubPermissions.Classroom.Participate)]
    public async Task<DiscussionReplyDto> ReplyAsync(CreateDiscussionReplyInput input)
    {
        if (string.IsNullOrWhiteSpace(input.Content))
        {
            throw new UserFriendlyException("回复内容不能为空。");
        }

        var discussion = await _discussionRepository.GetAsync(input.DiscussionId);
        await _support.EnsureCanParticipateAsync(discussion.CourseId);

        var authorId = _support.CurrentUserIdOrThrow();
        var reply = new DiscussionReply(
            GuidGenerator.Create(),
            input.DiscussionId,
            input.Content.Trim(),
            authorId)
        {
            ParentReplyId = input.ParentReplyId,
            AuthorName = await _support.GetDisplayNameAsync(authorId)
        };

        await _replyRepository.InsertAsync(reply);

        discussion.AddReply(DateTime.UtcNow);
        await _discussionRepository.UpdateAsync(discussion);

        await _activityLogger.LogAsync(
            authorId,
            StudentActivityAction.PostDiscussion,
            discussion.CourseId,
            "discussion-reply",
            reply.Id);

        return MapReply(reply);
    }

    [Authorize(KnowledgeHubPermissions.Classroom.Discussion)]
    public async Task<DiscussionDto> SetResolvedAsync(Guid id, bool resolved)
    {
        var discussion = await _discussionRepository.GetAsync(id);
        discussion.IsResolved = resolved;
        await _discussionRepository.UpdateAsync(discussion);
        return MapToDto(discussion);
    }

    [Authorize(KnowledgeHubPermissions.Classroom.Discussion)]
    public async Task<DiscussionDto> SetPinnedAsync(Guid id, bool pinned)
    {
        var discussion = await _discussionRepository.GetAsync(id);
        discussion.IsPinned = pinned;
        await _discussionRepository.UpdateAsync(discussion);
        return MapToDto(discussion);
    }

    public async Task<DiscussionDto> AcceptReplyAsync(Guid id, Guid replyId)
    {
        var discussion = await _discussionRepository.GetAsync(id);
        var me = _support.CurrentUserIdOrThrow();

        // 采纳权限：主题作者本人，或教师/管理员
        if (discussion.AuthorId != me && !await IsManagerAsync())
        {
            throw new UserFriendlyException("只有提问者或教师可以采纳答案。");
        }

        List<DiscussionReply> replies;
        using (_dataFilter.Disable<IMultiTenant>())
        {
            var query = await _replyRepository.GetQueryableAsync();
            replies = await AsyncExecuter.ToListAsync(query.Where(x => x.DiscussionId == id));
        }

        foreach (var reply in replies)
        {
            var shouldAccept = reply.Id == replyId;
            if (reply.IsAccepted != shouldAccept)
            {
                reply.IsAccepted = shouldAccept;
                await _replyRepository.UpdateAsync(reply);
            }
        }

        discussion.IsResolved = true;
        await _discussionRepository.UpdateAsync(discussion);
        return MapToDto(discussion);
    }

    public async Task DeleteAsync(Guid id)
    {
        var discussion = await _discussionRepository.GetAsync(id);
        var me = _support.CurrentUserIdOrThrow();
        if (discussion.AuthorId != me && !await IsManagerAsync())
        {
            throw new UserFriendlyException("只能删除自己发布的问题。");
        }

        List<DiscussionReply> replies;
        using (_dataFilter.Disable<IMultiTenant>())
        {
            var query = await _replyRepository.GetQueryableAsync();
            replies = await AsyncExecuter.ToListAsync(query.Where(x => x.DiscussionId == id));
        }

        foreach (var reply in replies)
        {
            await _replyRepository.DeleteAsync(reply);
        }

        await _discussionRepository.DeleteAsync(id);
    }

    public async Task DeleteReplyAsync(Guid replyId)
    {
        var reply = await _replyRepository.GetAsync(replyId);
        var me = _support.CurrentUserIdOrThrow();
        if (reply.AuthorId != me && !await IsManagerAsync())
        {
            throw new UserFriendlyException("只能删除自己的回复。");
        }

        var discussion = await _discussionRepository.FindAsync(reply.DiscussionId);
        await _replyRepository.DeleteAsync(replyId);

        if (discussion != null)
        {
            discussion.RemoveReply();
            await _discussionRepository.UpdateAsync(discussion);
        }
    }

    /// <summary>教师/管理员（有讨论管理权限）即视为可管理任意帖子。</summary>
    private async Task<bool> IsManagerAsync()
    {
        return await AuthorizationService.IsGrantedAsync(KnowledgeHubPermissions.Classroom.Discussion);
    }

    private DiscussionDto MapToDto(Discussion d)
    {
        return new DiscussionDto
        {
            Id = d.Id,
            CourseId = d.CourseId,
            ChapterId = d.ChapterId,
            KnowledgeResourceId = d.KnowledgeResourceId,
            Title = d.Title,
            Content = d.Content,
            AuthorId = d.AuthorId,
            AuthorName = d.AuthorName,
            IsPinned = d.IsPinned,
            IsResolved = d.IsResolved,
            ReplyCount = d.ReplyCount,
            ViewCount = d.ViewCount,
            LastReplyAt = d.LastReplyAt,
            IsMine = _currentUser.Id == d.AuthorId,
            CreationTime = d.CreationTime,
            CreatorId = d.CreatorId,
            LastModificationTime = d.LastModificationTime,
            LastModifierId = d.LastModifierId
        };
    }

    private static DiscussionReplyDto MapReply(DiscussionReply r)
    {
        return new DiscussionReplyDto
        {
            Id = r.Id,
            DiscussionId = r.DiscussionId,
            ParentReplyId = r.ParentReplyId,
            Content = r.Content,
            AuthorId = r.AuthorId,
            AuthorName = r.AuthorName,
            IsAccepted = r.IsAccepted,
            CreationTime = r.CreationTime,
            CreatorId = r.CreatorId
        };
    }
}
