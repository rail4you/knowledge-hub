using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using AspNetIdentityResult = Microsoft.AspNetCore.Identity.IdentityResult;
using ClosedXML.Excel;
using KnowledgeHub.Employment.Dtos;
using KnowledgeHub.Employment.Enums;
using KnowledgeHub.Permissions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Authorization;
using Volo.Abp.Content;
using Volo.Abp.Data;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Identity;
using Volo.Abp.MultiTenancy;

namespace KnowledgeHub.Employment;

public class EmploymentAppService : KnowledgeHubAppService, IEmploymentAppService
{
    private readonly IRepository<JobPosting, Guid> _jobPostingRepository;
    private readonly IRepository<StudentResume, Guid> _resumeRepository;
    private readonly IRepository<JobApplication, Guid> _applicationRepository;
    private readonly IRepository<InterviewSchedule, Guid> _interviewRepository;
    private readonly IRepository<EmploymentGuidanceRecord, Guid> _guidanceRepository;
    private readonly IRepository<EmploymentOutcome, Guid> _outcomeRepository;
    private readonly IRepository<IdentityUser, Guid> _userRepository;
    private readonly IdentityUserManager _userManager;

    public EmploymentAppService(
        IRepository<JobPosting, Guid> jobPostingRepository,
        IRepository<StudentResume, Guid> resumeRepository,
        IRepository<JobApplication, Guid> applicationRepository,
        IRepository<InterviewSchedule, Guid> interviewRepository,
        IRepository<EmploymentGuidanceRecord, Guid> guidanceRepository,
        IRepository<EmploymentOutcome, Guid> outcomeRepository,
        IRepository<IdentityUser, Guid> userRepository,
        IdentityUserManager userManager)
    {
        _jobPostingRepository = jobPostingRepository;
        _resumeRepository = resumeRepository;
        _applicationRepository = applicationRepository;
        _interviewRepository = interviewRepository;
        _guidanceRepository = guidanceRepository;
        _outcomeRepository = outcomeRepository;
        _userRepository = userRepository;
        _userManager = userManager;
    }

    [Authorize(KnowledgeHubPermissions.Employment.PublishJob)]
    public async Task<EmployerProfileDto> GetMyEmployerProfileAsync()
    {
        var user = await GetCurrentIdentityUserAsync();
        return MapEmployerProfile(user);
    }

    [Authorize(KnowledgeHubPermissions.Employment.PublishJob)]
    public async Task<EmployerProfileDto> UpdateMyEmployerProfileAsync(UpdateEmployerProfileDto input)
    {
        if (string.IsNullOrWhiteSpace(input.CompanyName))
        {
            throw new UserFriendlyException("企业名称不能为空。");
        }

        if (string.IsNullOrWhiteSpace(input.UnifiedSocialCreditCode))
        {
            throw new UserFriendlyException("统一社会信用代码不能为空。");
        }

        var user = await GetCurrentIdentityUserAsync();
        user.Name = input.ContactName?.Trim() ?? user.Name;
        if (!string.IsNullOrWhiteSpace(input.Email))
        {
            await EnsureIdentitySuccessAsync(await _userManager.SetEmailAsync(user, input.Email.Trim()));
        }

        user.SetPhoneNumber(input.PhoneNumber?.Trim(), false);
        user.SetProperty("CompanyName", input.CompanyName.Trim());
        user.SetProperty("UnifiedSocialCreditCode", input.UnifiedSocialCreditCode.Trim());
        user.SetProperty("Position", input.Position?.Trim());
        user.SetProperty("Industry", input.Industry?.Trim());
        user.SetProperty("PartnerSchool", input.PartnerSchool?.Trim());
        user.SetProperty("Remark", input.Remark?.Trim());
        await EnsureIdentitySuccessAsync(await _userManager.UpdateAsync(user));

        return MapEmployerProfile(user);
    }

    [Authorize(KnowledgeHubPermissions.Employment.Default)]
    public async Task<JobPostingDto> GetJobAsync(Guid id)
    {
        JobPosting entity;
        using (DataFilter.Disable<IMultiTenant>())
        {
            entity = await _jobPostingRepository.GetAsync(id);
        }

        if (entity.Status != EmploymentJobStatus.Published
            && !await CanReviewJobsAsync()
            && entity.EmployerUserId != CurrentUser.Id)
        {
            throw new AbpAuthorizationException();
        }

        entity.ViewCount += 1;
        await _jobPostingRepository.UpdateAsync(entity, autoSave: true);
        return await MapJobDtoAsync(entity);
    }

    [Authorize(KnowledgeHubPermissions.Employment.Default)]
    public async Task<PagedResultDto<JobPostingDto>> GetPublishedJobListAsync(PagedJobPostingRequestDto input)
    {
        List<JobPosting> items;
        long totalCount;

        using (DataFilter.Disable<IMultiTenant>())
        {
            var query = await _jobPostingRepository.GetQueryableAsync();
            query = query.Where(x => x.Status == EmploymentJobStatus.Published);
            query = ApplyJobFilters(query, input.Filter, input.Location, input.JobType);
            totalCount = await query.LongCountAsync();
            items = await query
                .OrderByDescending(x => x.PublishedAt ?? x.CreationTime)
                .Skip(input.SkipCount)
                .Take(input.MaxResultCount)
                .ToListAsync();
        }

        return new PagedResultDto<JobPostingDto>(totalCount, await MapJobDtosAsync(items));
    }

    [Authorize(KnowledgeHubPermissions.Employment.PublishJob)]
    public async Task<PagedResultDto<JobPostingDto>> GetManageJobListAsync(GetManageJobsInput input)
    {
        var canReview = await CanReviewJobsAsync();
        var currentUserId = GetCurrentUserId();

        List<JobPosting> items;
        long totalCount;
        using (DataFilter.Disable<IMultiTenant>())
        {
            var query = await _jobPostingRepository.GetQueryableAsync();
            query = ApplyJobFilters(query, input.Filter, input.Location, input.JobType);

            if (input.Status.HasValue)
            {
                query = query.Where(x => x.Status == input.Status.Value);
            }

            if (!canReview)
            {
                query = query.Where(x => x.EmployerUserId == currentUserId);
            }

            totalCount = await query.LongCountAsync();
            items = await query
                .OrderByDescending(x => x.CreationTime)
                .Skip(input.SkipCount)
                .Take(input.MaxResultCount)
                .ToListAsync();
        }

        return new PagedResultDto<JobPostingDto>(totalCount, await MapJobDtosAsync(items));
    }

    [Authorize(KnowledgeHubPermissions.Employment.PublishJob)]
    public async Task<JobPostingDto> CreateJobAsync(CreateUpdateJobPostingDto input)
    {
        ValidateJobInput(input);

        var currentUser = await GetCurrentIdentityUserAsync();
        var canReview = await CanReviewJobsAsync();
        var profileCompanyName = currentUser.GetProperty<string>("CompanyName");
        var companyName = !string.IsNullOrWhiteSpace(input.CompanyName)
            ? input.CompanyName.Trim()
            : profileCompanyName;
        if (string.IsNullOrWhiteSpace(companyName) && !canReview)
        {
            throw new UserFriendlyException("请先完善企业档案后再发布岗位。");
        }
        if (string.IsNullOrWhiteSpace(companyName))
        {
            companyName = "平台代发";
        }

        var status = ResolveCreateOrUpdateStatus(input.Status, canReview);
        var entity = new JobPosting(GuidGenerator.Create(), currentUser.Id, companyName!, input.Title.Trim(), input.Description.Trim())
        {
            TenantId = CurrentTenant.Id,
            Industry = !string.IsNullOrWhiteSpace(input.Industry)
                ? input.Industry.Trim()
                : currentUser.GetProperty<string>("Industry"),
            Summary = input.Summary?.Trim(),
            Location = input.Location?.Trim(),
            Address = input.Address?.Trim(),
            JobType = input.JobType,
            EducationRequirement = input.EducationRequirement?.Trim(),
            SalaryRange = input.SalaryRange?.Trim(),
            RecruitmentCount = input.RecruitmentCount <= 0 ? 1 : input.RecruitmentCount,
            SkillTags = input.SkillTags?.Trim(),
            Benefits = input.Benefits?.Trim(),
            ContactName = string.IsNullOrWhiteSpace(input.ContactName) ? currentUser.Name : input.ContactName.Trim(),
            ContactPhone = string.IsNullOrWhiteSpace(input.ContactPhone) ? currentUser.PhoneNumber : input.ContactPhone.Trim(),
            ContactEmail = string.IsNullOrWhiteSpace(input.ContactEmail) ? currentUser.Email : input.ContactEmail.Trim(),
            Deadline = input.Deadline,
            Status = status,
            PublishedAt = status == EmploymentJobStatus.Published ? Clock.Now : null
        };

        await _jobPostingRepository.InsertAsync(entity, autoSave: true);
        return await MapJobDtoAsync(entity);
    }

    [Authorize(KnowledgeHubPermissions.Employment.PublishJob)]
    public async Task<JobPostingDto> UpdateJobAsync(Guid id, CreateUpdateJobPostingDto input)
    {
        ValidateJobInput(input);

        var entity = await GetManageableJobAsync(id);
        var canReview = await CanReviewJobsAsync();
        var status = ResolveCreateOrUpdateStatus(input.Status, canReview);

        if (!string.IsNullOrWhiteSpace(input.CompanyName))
        {
            entity.CompanyName = input.CompanyName.Trim();
        }
        if (!string.IsNullOrWhiteSpace(input.Industry))
        {
            entity.Industry = input.Industry.Trim();
        }
        entity.Title = input.Title.Trim();
        entity.Summary = input.Summary?.Trim();
        entity.Description = input.Description.Trim();
        entity.Location = input.Location?.Trim();
        entity.Address = input.Address?.Trim();
        entity.JobType = input.JobType;
        entity.EducationRequirement = input.EducationRequirement?.Trim();
        entity.SalaryRange = input.SalaryRange?.Trim();
        entity.RecruitmentCount = input.RecruitmentCount <= 0 ? 1 : input.RecruitmentCount;
        entity.SkillTags = input.SkillTags?.Trim();
        entity.Benefits = input.Benefits?.Trim();
        entity.ContactName = input.ContactName?.Trim();
        entity.ContactPhone = input.ContactPhone?.Trim();
        entity.ContactEmail = input.ContactEmail?.Trim();
        entity.Deadline = input.Deadline;
        entity.Status = status;

        if (status == EmploymentJobStatus.Published && !entity.PublishedAt.HasValue)
        {
            entity.PublishedAt = Clock.Now;
        }

        await _jobPostingRepository.UpdateAsync(entity, autoSave: true);
        return await MapJobDtoAsync(entity);
    }

    [Authorize(KnowledgeHubPermissions.Employment.PublishJob)]
    public async Task DeleteJobAsync(Guid id)
    {
        var entity = await GetManageableJobAsync(id);
        await DeleteRelatedInterviewsAsync(id);
        await DeleteRelatedApplicationsAsync(id);
        await _jobPostingRepository.DeleteAsync(entity);
    }

    [Authorize(KnowledgeHubPermissions.Employment.ReviewJob)]
    public async Task<JobPostingDto> ReviewJobAsync(Guid id, ReviewJobPostingDto input)
    {
        if (input.Status != EmploymentJobStatus.Published
            && input.Status != EmploymentJobStatus.Rejected
            && input.Status != EmploymentJobStatus.Closed)
        {
            throw new UserFriendlyException("岗位审核状态不合法。");
        }

        JobPosting entity;
        using (DataFilter.Disable<IMultiTenant>())
        {
            entity = await _jobPostingRepository.GetAsync(id);
        }

        entity.Status = input.Status;
        entity.ReviewComment = input.ReviewComment?.Trim();
        entity.ReviewedAt = Clock.Now;
        entity.ReviewedById = CurrentUser.Id;
        if (input.Status == EmploymentJobStatus.Published)
        {
            entity.PublishedAt ??= Clock.Now;
        }

        await _jobPostingRepository.UpdateAsync(entity, autoSave: true);
        return await MapJobDtoAsync(entity);
    }

    [Authorize(KnowledgeHubPermissions.Employment.ManageResume)]
    public async Task<List<StudentResumeDto>> GetMyResumeListAsync()
    {
        var studentId = GetCurrentUserId();
        var items = await _resumeRepository.GetListAsync(x => x.StudentId == studentId);
        return items
            .OrderByDescending(x => x.IsDefault)
            .ThenByDescending(x => x.VersionNo)
            .Select(MapResumeDto)
            .ToList();
    }

    [Authorize(KnowledgeHubPermissions.Employment.ManageResume)]
    public async Task<StudentResumeDto> CreateResumeAsync(CreateUpdateStudentResumeDto input)
    {
        ValidateResumeInput(input);

        var student = await GetCurrentIdentityUserAsync();
        var studentId = student.Id;
        var items = await _resumeRepository.GetListAsync(x => x.StudentId == studentId);
        var nextVersion = items.Count == 0 ? 1 : items.Max(x => x.VersionNo) + 1;
        var shouldBeDefault = input.IsDefault || items.Count == 0;

        if (shouldBeDefault)
        {
            await ClearDefaultResumeAsync(studentId);
        }

        var entity = new StudentResume(GuidGenerator.Create(), studentId, input.Title.Trim(), input.FullName.Trim())
        {
            TenantId = CurrentTenant.Id,
            PhoneNumber = string.IsNullOrWhiteSpace(input.PhoneNumber) ? student.PhoneNumber : input.PhoneNumber.Trim(),
            Email = string.IsNullOrWhiteSpace(input.Email) ? student.Email : input.Email.Trim(),
            SchoolName = input.SchoolName?.Trim(),
            Major = string.IsNullOrWhiteSpace(input.Major) ? student.GetProperty<string>("Major") : input.Major.Trim(),
            Grade = string.IsNullOrWhiteSpace(input.Grade) ? student.GetProperty<string>("Grade") : input.Grade.Trim(),
            ClassName = string.IsNullOrWhiteSpace(input.ClassName) ? student.GetProperty<string>("ClassName") : input.ClassName.Trim(),
            StudentNumber = string.IsNullOrWhiteSpace(input.StudentNumber) ? student.GetProperty<string>("StudentNumber") : input.StudentNumber.Trim(),
            Summary = input.Summary?.Trim(),
            Skills = input.Skills?.Trim(),
            EducationExperience = input.EducationExperience?.Trim(),
            InternshipExperience = input.InternshipExperience?.Trim(),
            ProjectExperience = input.ProjectExperience?.Trim(),
            CertificateText = input.CertificateText?.Trim(),
            AttachmentUrl = input.AttachmentUrl?.Trim(),
            IsDefault = shouldBeDefault,
            VersionNo = nextVersion
        };

        await _resumeRepository.InsertAsync(entity, autoSave: true);
        return MapResumeDto(entity);
    }

    [Authorize(KnowledgeHubPermissions.Employment.ManageResume)]
    public async Task<StudentResumeDto> UpdateResumeAsync(Guid id, CreateUpdateStudentResumeDto input)
    {
        ValidateResumeInput(input);

        var entity = await GetOwnResumeAsync(id);
        if (input.IsDefault && !entity.IsDefault)
        {
            await ClearDefaultResumeAsync(entity.StudentId);
        }

        entity.Title = input.Title.Trim();
        entity.FullName = input.FullName.Trim();
        entity.PhoneNumber = input.PhoneNumber?.Trim();
        entity.Email = input.Email?.Trim();
        entity.SchoolName = input.SchoolName?.Trim();
        entity.Major = input.Major?.Trim();
        entity.Grade = input.Grade?.Trim();
        entity.ClassName = input.ClassName?.Trim();
        entity.StudentNumber = input.StudentNumber?.Trim();
        entity.Summary = input.Summary?.Trim();
        entity.Skills = input.Skills?.Trim();
        entity.EducationExperience = input.EducationExperience?.Trim();
        entity.InternshipExperience = input.InternshipExperience?.Trim();
        entity.ProjectExperience = input.ProjectExperience?.Trim();
        entity.CertificateText = input.CertificateText?.Trim();
        entity.AttachmentUrl = input.AttachmentUrl?.Trim();
        entity.IsDefault = input.IsDefault || entity.IsDefault;

        await _resumeRepository.UpdateAsync(entity, autoSave: true);
        return MapResumeDto(entity);
    }

    [Authorize(KnowledgeHubPermissions.Employment.ManageResume)]
    public async Task DeleteResumeAsync(Guid id)
    {
        var entity = await GetOwnResumeAsync(id);
        await _resumeRepository.DeleteAsync(entity);

        if (!entity.IsDefault)
        {
            return;
        }

        var items = await _resumeRepository.GetListAsync(x => x.StudentId == entity.StudentId);
        var nextDefault = items.OrderByDescending(x => x.VersionNo).FirstOrDefault();
        if (nextDefault != null)
        {
            nextDefault.IsDefault = true;
            await _resumeRepository.UpdateAsync(nextDefault, autoSave: true);
        }
    }

    [Authorize(KnowledgeHubPermissions.Employment.ManageResume)]
    public async Task<StudentResumeDto> SetDefaultResumeAsync(Guid id)
    {
        var entity = await GetOwnResumeAsync(id);
        await ClearDefaultResumeAsync(entity.StudentId);
        entity.IsDefault = true;
        await _resumeRepository.UpdateAsync(entity, autoSave: true);
        return MapResumeDto(entity);
    }

    [Authorize(KnowledgeHubPermissions.Employment.Default)]
    public async Task<JobApplicationDto> CreateApplicationAsync(CreateJobApplicationDto input)
    {
        var studentId = GetCurrentUserId();
        var resume = await GetOwnResumeAsync(input.ResumeId);

        JobPosting jobPosting;
        using (DataFilter.Disable<IMultiTenant>())
        {
            jobPosting = await _jobPostingRepository.GetAsync(input.JobPostingId);
        }

        if (jobPosting.Status != EmploymentJobStatus.Published)
        {
            throw new UserFriendlyException("当前岗位不可投递。");
        }

        using (DataFilter.Disable<IMultiTenant>())
        {
            var exists = await _applicationRepository.AnyAsync(x => x.JobPostingId == input.JobPostingId && x.StudentId == studentId);
            if (exists)
            {
                throw new UserFriendlyException("您已投递过该岗位。");
            }
        }

        var entity = new JobApplication(GuidGenerator.Create(), input.JobPostingId, studentId, input.ResumeId)
        {
            TenantId = CurrentTenant.Id,
            CoverLetter = input.CoverLetter?.Trim(),
            AppliedAt = Clock.Now
        };

        resume.LastUsedAt = Clock.Now;
        await _resumeRepository.UpdateAsync(resume, autoSave: true);
        await _applicationRepository.InsertAsync(entity, autoSave: true);
        return await MapApplicationDtoAsync(entity);
    }

    [Authorize(KnowledgeHubPermissions.Employment.Default)]
    public async Task<PagedResultDto<JobApplicationDto>> GetMyApplicationListAsync(GetJobApplicationsInput input)
    {
        var studentId = GetCurrentUserId();
        using (DataFilter.Disable<IMultiTenant>())
        {
            var query = await _applicationRepository.GetQueryableAsync();
            query = query.Where(x => x.StudentId == studentId);
            if (input.Status.HasValue)
            {
                query = query.Where(x => x.Status == input.Status.Value);
            }

            var totalCount = await query.LongCountAsync();
            var items = await query
                .OrderByDescending(x => x.AppliedAt)
                .Skip(input.SkipCount)
                .Take(input.MaxResultCount)
                .ToListAsync();

            return new PagedResultDto<JobApplicationDto>(totalCount, await MapApplicationDtosAsync(items));
        }
    }

    [Authorize(KnowledgeHubPermissions.Employment.PublishJob)]
    [Authorize(KnowledgeHubPermissions.Employment.ManageApplication)]
    public async Task<PagedResultDto<JobApplicationDto>> GetJobApplicationListAsync(GetJobApplicationsInput input)
    {
        var canReview = await CanReviewJobsAsync();
        var canManageApplications = await CanManageApplicationsAsync();
        var currentUserId = GetCurrentUserId();
        var ownJobIds = new HashSet<Guid>();

        if (!canReview && !canManageApplications)
        {
            using (DataFilter.Disable<IMultiTenant>())
            {
                ownJobIds = (await _jobPostingRepository.GetListAsync(x => x.EmployerUserId == currentUserId))
                    .Select(x => x.Id)
                    .ToHashSet();
            }
        }

        using (DataFilter.Disable<IMultiTenant>())
        {
            var query = await _applicationRepository.GetQueryableAsync();

            if (input.JobPostingId.HasValue)
            {
                query = query.Where(x => x.JobPostingId == input.JobPostingId.Value);
            }

            if (input.StudentId.HasValue)
            {
                query = query.Where(x => x.StudentId == input.StudentId.Value);
            }

            if (input.Status.HasValue)
            {
                query = query.Where(x => x.Status == input.Status.Value);
            }

            if (!canReview && !canManageApplications)
            {
                query = query.Where(x => ownJobIds.Contains(x.JobPostingId));
            }

            var totalCount = await query.LongCountAsync();
            var items = await query
                .OrderByDescending(x => x.AppliedAt)
                .Skip(input.SkipCount)
                .Take(input.MaxResultCount)
                .ToListAsync();

            return new PagedResultDto<JobApplicationDto>(totalCount, await MapApplicationDtosAsync(items));
        }
    }

    [Authorize(KnowledgeHubPermissions.Employment.PublishJob)]
    [Authorize(KnowledgeHubPermissions.Employment.ManageApplication)]
    public async Task<JobApplicationDto> UpdateApplicationStatusAsync(Guid id, UpdateJobApplicationStatusDto input)
    {
        var entity = await GetManageableApplicationAsync(id);
        entity.Status = input.Status;
        entity.EmployerRemark = input.EmployerRemark?.Trim();
        entity.ReviewedAt = Clock.Now;
        await _applicationRepository.UpdateAsync(entity, autoSave: true);
        return await MapApplicationDtoAsync(entity);
    }

    [Authorize(KnowledgeHubPermissions.Employment.ScheduleInterview)]
    public async Task<InterviewScheduleDto> ScheduleInterviewAsync(CreateUpdateInterviewScheduleDto input)
    {
        if (string.IsNullOrWhiteSpace(input.InterviewerName))
        {
            throw new UserFriendlyException("面试官不能为空。");
        }

        var application = await GetManageableApplicationAsync(input.ApplicationId);
        var entity = new InterviewSchedule(
            GuidGenerator.Create(),
            application.Id,
            application.JobPostingId,
            application.StudentId,
            input.InterviewerName.Trim(),
            input.ScheduledAt)
        {
            TenantId = application.TenantId,
            EmployerUserId = CurrentUser.Id,
            InterviewerPhone = input.InterviewerPhone?.Trim(),
            Location = input.Location?.Trim(),
            MeetingUrl = input.MeetingUrl?.Trim(),
            Note = input.Note?.Trim()
        };

        application.Status = EmploymentApplicationStatus.InterviewScheduled;
        application.ReviewedAt = Clock.Now;
        await _applicationRepository.UpdateAsync(application, autoSave: true);
        await _interviewRepository.InsertAsync(entity, autoSave: true);
        return await MapInterviewDtoAsync(entity);
    }

    [Authorize(KnowledgeHubPermissions.Employment.ScheduleInterview)]
    public async Task<InterviewScheduleDto> UpdateInterviewAsync(Guid id, CreateUpdateInterviewScheduleDto input)
    {
        var entity = await GetManageableInterviewAsync(id);
        entity.InterviewerName = input.InterviewerName.Trim();
        entity.InterviewerPhone = input.InterviewerPhone?.Trim();
        entity.ScheduledAt = input.ScheduledAt;
        entity.Location = input.Location?.Trim();
        entity.MeetingUrl = input.MeetingUrl?.Trim();
        entity.Note = input.Note?.Trim();
        await _interviewRepository.UpdateAsync(entity, autoSave: true);
        return await MapInterviewDtoAsync(entity);
    }

    [Authorize(KnowledgeHubPermissions.Employment.ScheduleInterview)]
    public async Task<InterviewScheduleDto> RecordInterviewResultAsync(Guid id, RecordInterviewResultDto input)
    {
        var entity = await GetManageableInterviewAsync(id);
        entity.Result = input.Result;
        entity.Summary = input.Summary?.Trim();
        entity.ResultComment = input.ResultComment?.Trim();
        entity.ResultRecordedAt = Clock.Now;
        await _interviewRepository.UpdateAsync(entity, autoSave: true);

        var application = await _applicationRepository.GetAsync(entity.ApplicationId);
        application.Status = input.Result switch
        {
            EmploymentInterviewResult.Passed => EmploymentApplicationStatus.Offered,
            EmploymentInterviewResult.Failed => EmploymentApplicationStatus.Rejected,
            EmploymentInterviewResult.Deferred => EmploymentApplicationStatus.Viewed,
            _ => EmploymentApplicationStatus.InterviewScheduled
        };
        application.ReviewedAt = Clock.Now;
        await _applicationRepository.UpdateAsync(application, autoSave: true);

        return await MapInterviewDtoAsync(entity);
    }

    [Authorize(KnowledgeHubPermissions.Employment.Default)]
    public async Task<PagedResultDto<InterviewScheduleDto>> GetInterviewListAsync(GetInterviewSchedulesInput input)
    {
        var currentUserId = CurrentUser.Id;
        var canReview = await CanReviewJobsAsync();
        var canSchedule = await AuthorizationService.IsGrantedAsync(KnowledgeHubPermissions.Employment.ScheduleInterview);
        HashSet<Guid> ownJobIds = [];

        if (!canReview && canSchedule && currentUserId.HasValue)
        {
            using (DataFilter.Disable<IMultiTenant>())
            {
                ownJobIds = (await _jobPostingRepository.GetListAsync(x => x.EmployerUserId == currentUserId.Value))
                    .Select(x => x.Id)
                    .ToHashSet();
            }
        }

        using (DataFilter.Disable<IMultiTenant>())
        {
            var query = await _interviewRepository.GetQueryableAsync();

            if (input.JobPostingId.HasValue)
            {
                query = query.Where(x => x.JobPostingId == input.JobPostingId.Value);
            }

            if (input.StudentId.HasValue)
            {
                query = query.Where(x => x.StudentId == input.StudentId.Value);
            }

            if (input.ApplicationId.HasValue)
            {
                query = query.Where(x => x.ApplicationId == input.ApplicationId.Value);
            }

            if (input.Result.HasValue)
            {
                query = query.Where(x => x.Result == input.Result.Value);
            }

            if (!canReview)
            {
                if (canSchedule && currentUserId.HasValue)
                {
                    query = query.Where(x => ownJobIds.Contains(x.JobPostingId));
                }
                else if (currentUserId.HasValue)
                {
                    query = query.Where(x => x.StudentId == currentUserId.Value);
                }
                else
                {
                    query = query.Where(_ => false);
                }
            }

            var totalCount = await query.LongCountAsync();
            var items = await query
                .OrderByDescending(x => x.ScheduledAt)
                .Skip(input.SkipCount)
                .Take(input.MaxResultCount)
                .ToListAsync();

            return new PagedResultDto<InterviewScheduleDto>(totalCount, await MapInterviewDtosAsync(items));
        }
    }

    [Authorize(KnowledgeHubPermissions.Employment.ManageGuidance)]
    public async Task<EmploymentGuidanceRecordDto> CreateGuidanceRecordAsync(CreateEmploymentGuidanceRecordDto input)
    {
        if (string.IsNullOrWhiteSpace(input.Title) || string.IsNullOrWhiteSpace(input.Content))
        {
            throw new UserFriendlyException("指导标题和内容不能为空。");
        }

        if (input.ApplicationId.HasValue)
        {
            await _applicationRepository.GetAsync(input.ApplicationId.Value);
        }

        var entity = new EmploymentGuidanceRecord(
            GuidGenerator.Create(),
            input.StudentId,
            GetCurrentUserId(),
            input.Title.Trim(),
            input.Content.Trim())
        {
            TenantId = CurrentTenant.Id,
            ApplicationId = input.ApplicationId,
            SourceType = input.SourceType,
            CareerGoal = input.CareerGoal?.Trim(),
            GuidedAt = Clock.Now
        };

        await _guidanceRepository.InsertAsync(entity, autoSave: true);
        return await MapGuidanceDtoAsync(entity);
    }

    [Authorize(KnowledgeHubPermissions.Employment.Default)]
    public async Task<EmploymentGuidanceRecordDto> CreateMyAIGuidanceRecordAsync(CreateMyAIGuidanceRecordDto input)
    {
        if (string.IsNullOrWhiteSpace(input.Title) || string.IsNullOrWhiteSpace(input.Content))
        {
            throw new UserFriendlyException("指导标题和内容不能为空。");
        }

        var studentId = GetCurrentUserId();

        // AI 记录无教师：TeacherId = null，SourceType = AI
        var entity = new EmploymentGuidanceRecord(
            GuidGenerator.Create(),
            studentId,
            teacherId: null,
            input.Title.Trim(),
            input.Content)
        {
            TenantId = CurrentTenant.Id,
            ApplicationId = null,
            SourceType = EmploymentGuidanceSourceType.AI,
            CareerGoal = input.CareerGoal?.Trim(),
            GuidedAt = Clock.Now
        };

        await _guidanceRepository.InsertAsync(entity, autoSave: true);
        return await MapGuidanceDtoAsync(entity);
    }

    [Authorize(KnowledgeHubPermissions.Employment.Default)]
    public async Task<PagedResultDto<EmploymentGuidanceRecordDto>> GetMyGuidanceRecordListAsync(GetEmploymentGuidanceRecordsInput input)
    {
        var currentUserId = GetCurrentUserId();
        var query = await _guidanceRepository.GetQueryableAsync();
        query = query.Where(x => x.StudentId == currentUserId);

        if (input.ApplicationId.HasValue)
        {
            query = query.Where(x => x.ApplicationId == input.ApplicationId.Value);
        }

        var totalCount = await query.LongCountAsync();
        var items = await query
            .OrderByDescending(x => x.GuidedAt)
            .Skip(input.SkipCount)
            .Take(input.MaxResultCount)
            .ToListAsync();

        return new PagedResultDto<EmploymentGuidanceRecordDto>(totalCount, await MapGuidanceDtosAsync(items));
    }

    [Authorize(KnowledgeHubPermissions.Employment.ManageGuidance)]
    public async Task<PagedResultDto<EmploymentGuidanceRecordDto>> GetGuidanceRecordListAsync(GetEmploymentGuidanceRecordsInput input)
    {
        var query = await _guidanceRepository.GetQueryableAsync();
        if (input.StudentId.HasValue)
        {
            query = query.Where(x => x.StudentId == input.StudentId.Value);
        }

        if (input.ApplicationId.HasValue)
        {
            query = query.Where(x => x.ApplicationId == input.ApplicationId.Value);
        }

        var totalCount = await query.LongCountAsync();
        var items = await query
            .OrderByDescending(x => x.GuidedAt)
            .Skip(input.SkipCount)
            .Take(input.MaxResultCount)
            .ToListAsync();

        return new PagedResultDto<EmploymentGuidanceRecordDto>(totalCount, await MapGuidanceDtosAsync(items));
    }

    [Authorize(KnowledgeHubPermissions.Employment.Default)]
    public async Task<EmploymentOutcomeDto> SaveOutcomeAsync(CreateUpdateEmploymentOutcomeDto input)
    {
        var currentUserId = CurrentUser.Id;
        var canManageOutcome = await AuthorizationService.IsGrantedAsync(KnowledgeHubPermissions.Employment.ManageOutcome);
        if (!canManageOutcome && currentUserId != input.StudentId)
        {
            throw new AbpAuthorizationException();
        }

        if (string.IsNullOrWhiteSpace(input.EmployerName) || string.IsNullOrWhiteSpace(input.JobTitle))
        {
            throw new UserFriendlyException("去向单位和岗位名称不能为空。");
        }

        EmploymentOutcome entity;
        if (input.Id.HasValue)
        {
            entity = await _outcomeRepository.GetAsync(input.Id.Value);
            if (!canManageOutcome && entity.StudentId != currentUserId)
            {
                throw new AbpAuthorizationException();
            }
        }
        else
        {
            entity = new EmploymentOutcome(GuidGenerator.Create(), input.StudentId, input.EmployerName.Trim(), input.JobTitle.Trim())
            {
                TenantId = CurrentTenant.Id
            };
        }

        if (input.ApplicationId.HasValue)
        {
            await _applicationRepository.GetAsync(input.ApplicationId.Value);
        }

        if (input.IsPrimary)
        {
            var existingItems = await _outcomeRepository.GetListAsync(x => x.StudentId == input.StudentId && x.Id != entity.Id);
            foreach (var item in existingItems.Where(x => x.IsPrimary))
            {
                item.IsPrimary = false;
                await _outcomeRepository.UpdateAsync(item);
            }
        }

        entity.StudentId = input.StudentId;
        entity.ApplicationId = input.ApplicationId;
        entity.EmployerName = input.EmployerName.Trim();
        entity.JobTitle = input.JobTitle.Trim();
        entity.Status = input.Status;
        entity.EmploymentType = input.EmploymentType?.Trim();
        entity.Region = input.Region?.Trim();
        entity.SalaryRange = input.SalaryRange?.Trim();
        entity.StartDate = input.StartDate;
        entity.ConfirmedAt = input.ConfirmedAt == default ? Clock.Now : input.ConfirmedAt;
        entity.Remark = input.Remark?.Trim();
        entity.IsPrimary = input.IsPrimary;

        if (input.Id.HasValue)
        {
            await _outcomeRepository.UpdateAsync(entity, autoSave: true);
        }
        else
        {
            await _outcomeRepository.InsertAsync(entity, autoSave: true);
        }

        return await MapOutcomeDtoAsync(entity);
    }

    [Authorize(KnowledgeHubPermissions.Employment.Default)]
    public async Task<PagedResultDto<EmploymentOutcomeDto>> GetOutcomeListAsync(GetEmploymentOutcomeListInput input)
    {
        var currentUserId = CurrentUser.Id;
        var canManageOutcome = await AuthorizationService.IsGrantedAsync(KnowledgeHubPermissions.Employment.ManageOutcome);
        var canViewStatistics = await AuthorizationService.IsGrantedAsync(KnowledgeHubPermissions.Employment.ViewStatistics);

        using (DataFilter.Disable<IMultiTenant>())
        {
            var query = await _outcomeRepository.GetQueryableAsync();

            if (input.StudentId.HasValue)
            {
                query = query.Where(x => x.StudentId == input.StudentId.Value);
            }

            if (input.Status.HasValue)
            {
                query = query.Where(x => x.Status == input.Status.Value);
            }

            if (input.OnlyPrimary.HasValue)
            {
                query = query.Where(x => x.IsPrimary == input.OnlyPrimary.Value);
            }

            if (!canManageOutcome && !canViewStatistics)
            {
                if (!currentUserId.HasValue)
                {
                    throw new AbpAuthorizationException();
                }

                query = query.Where(x => x.StudentId == currentUserId.Value);
            }

            var totalCount = await query.LongCountAsync();
            var items = await query
                .OrderByDescending(x => x.ConfirmedAt)
                .Skip(input.SkipCount)
                .Take(input.MaxResultCount)
                .ToListAsync();

            return new PagedResultDto<EmploymentOutcomeDto>(totalCount, await MapOutcomeDtosAsync(items));
        }
    }

    [Authorize(KnowledgeHubPermissions.Employment.ViewStatistics)]
    public async Task<List<EmploymentStatisticsRowDto>> GetStatisticsAsync(EmploymentStatisticsInput input)
    {
        using (DataFilter.Disable<IMultiTenant>())
        {
            var outcomes = await _outcomeRepository.GetListAsync(x => x.IsPrimary);
            if (input.Status.HasValue)
            {
                outcomes = outcomes.Where(x => x.Status == input.Status.Value).ToList();
            }

            var studentIds = outcomes.Select(x => x.StudentId).Distinct().ToList();
            var students = studentIds.Count == 0
                ? []
                : await _userRepository.GetListAsync(x => studentIds.Contains(x.Id));

            var studentMap = students.ToDictionary(x => x.Id, x => x);
            var result = outcomes
                .Where(x => studentMap.ContainsKey(x.StudentId))
                .Select(x =>
                {
                    var student = studentMap[x.StudentId];
                    return new
                    {
                        Major = student.GetProperty<string>("Major") ?? "未填写",
                        Grade = student.GetProperty<string>("Grade") ?? "未填写",
                        x.Status
                    };
                })
                .Where(x => string.IsNullOrWhiteSpace(input.Major) || x.Major.Contains(input.Major!, StringComparison.OrdinalIgnoreCase))
                .Where(x => string.IsNullOrWhiteSpace(input.Grade) || x.Grade.Contains(input.Grade!, StringComparison.OrdinalIgnoreCase))
                .GroupBy(x => new { x.Major, x.Grade, x.Status })
                .Select(x => new EmploymentStatisticsRowDto
                {
                    Major = x.Key.Major,
                    Grade = x.Key.Grade,
                    Status = x.Key.Status,
                    StudentCount = x.Count(),
                    OutcomeCount = x.Count()
                })
                .OrderBy(x => x.Major)
                .ThenBy(x => x.Grade)
                .ThenBy(x => x.Status)
                .ToList();

            return result;
        }
    }

    [Authorize(KnowledgeHubPermissions.Employment.ExportReport)]
    public async Task<IRemoteStreamContent> ExportStatisticsAsync(EmploymentStatisticsInput input)
    {
        var rows = await GetStatisticsAsync(input);
        using var workbook = new XLWorkbook();
        var worksheet = workbook.Worksheets.Add("就业统计");

        worksheet.Cell(1, 1).Value = "专业";
        worksheet.Cell(1, 2).Value = "年级";
        worksheet.Cell(1, 3).Value = "去向状态";
        worksheet.Cell(1, 4).Value = "学生数";
        worksheet.Cell(1, 5).Value = "去向数";

        for (var i = 0; i < rows.Count; i++)
        {
            var row = rows[i];
            worksheet.Cell(i + 2, 1).Value = row.Major;
            worksheet.Cell(i + 2, 2).Value = row.Grade;
            worksheet.Cell(i + 2, 3).Value = row.Status.ToString();
            worksheet.Cell(i + 2, 4).Value = row.StudentCount;
            worksheet.Cell(i + 2, 5).Value = row.OutcomeCount;
        }

        worksheet.Columns().AdjustToContents();

        var stream = new MemoryStream();
        workbook.SaveAs(stream);
        stream.Position = 0;
        return new RemoteStreamContent(stream, $"就业统计_{Clock.Now:yyyyMMddHHmmss}.xlsx", "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet");
    }

    private static IQueryable<JobPosting> ApplyJobFilters(
        IQueryable<JobPosting> query,
        string? filter,
        string? location,
        EmploymentJobType? jobType)
    {
        if (!string.IsNullOrWhiteSpace(filter))
        {
            query = query.Where(x =>
                x.Title.Contains(filter) ||
                (x.CompanyName != null && x.CompanyName.Contains(filter)) ||
                (x.SkillTags != null && x.SkillTags.Contains(filter)));
        }

        if (!string.IsNullOrWhiteSpace(location))
        {
            query = query.Where(x => x.Location != null && x.Location.Contains(location));
        }

        if (jobType.HasValue)
        {
            query = query.Where(x => x.JobType == jobType.Value);
        }

        return query;
    }

    private async Task<List<JobPostingDto>> MapJobDtosAsync(List<JobPosting> items)
    {
        if (items.Count == 0)
        {
            return [];
        }

        using (DataFilter.Disable<IMultiTenant>())
        {
            var jobIds = items.Select(x => x.Id).ToList();
            var applications = await _applicationRepository.GetListAsync(x => jobIds.Contains(x.JobPostingId));
            var applicationCountMap = applications
                .GroupBy(x => x.JobPostingId)
                .ToDictionary(x => x.Key, x => x.Count());

            var currentUserId = CurrentUser.Id;
            var appliedJobIds = currentUserId.HasValue
                ? applications.Where(x => x.StudentId == currentUserId.Value).Select(x => x.JobPostingId).ToHashSet()
                : [];

            return items.Select(item => MapJobDto(item, applicationCountMap, appliedJobIds)).ToList();
        }
    }

    private async Task<JobPostingDto> MapJobDtoAsync(JobPosting entity)
    {
        using (DataFilter.Disable<IMultiTenant>())
        {
            var applications = await _applicationRepository.GetListAsync(x => x.JobPostingId == entity.Id);
            var applicationCountMap = new Dictionary<Guid, int> { [entity.Id] = applications.Count };
            var appliedJobIds = CurrentUser.Id.HasValue
                ? applications.Where(x => x.StudentId == CurrentUser.Id.Value).Select(x => x.JobPostingId).ToHashSet()
                : [];
            return MapJobDto(entity, applicationCountMap, appliedJobIds);
        }
    }

    private static JobPostingDto MapJobDto(
        JobPosting entity,
        Dictionary<Guid, int> applicationCountMap,
        HashSet<Guid> appliedJobIds)
    {
        return new JobPostingDto
        {
            Id = entity.Id,
            CreationTime = entity.CreationTime,
            LastModificationTime = entity.LastModificationTime,
            EmployerUserId = entity.EmployerUserId,
            CompanyName = entity.CompanyName,
            Industry = entity.Industry,
            Title = entity.Title,
            Summary = entity.Summary,
            Description = entity.Description,
            Location = entity.Location,
            Address = entity.Address,
            JobType = entity.JobType,
            EducationRequirement = entity.EducationRequirement,
            SalaryRange = entity.SalaryRange,
            RecruitmentCount = entity.RecruitmentCount,
            SkillTags = entity.SkillTags,
            Benefits = entity.Benefits,
            ContactName = entity.ContactName,
            ContactPhone = entity.ContactPhone,
            ContactEmail = entity.ContactEmail,
            Deadline = entity.Deadline,
            Status = entity.Status,
            ReviewComment = entity.ReviewComment,
            ReviewedAt = entity.ReviewedAt,
            PublishedAt = entity.PublishedAt,
            ViewCount = entity.ViewCount,
            ApplicationCount = applicationCountMap.TryGetValue(entity.Id, out var count) ? count : 0,
            HasApplied = appliedJobIds.Contains(entity.Id)
        };
    }

    private static StudentResumeDto MapResumeDto(StudentResume entity)
    {
        return new StudentResumeDto
        {
            Id = entity.Id,
            CreationTime = entity.CreationTime,
            LastModificationTime = entity.LastModificationTime,
            StudentId = entity.StudentId,
            Title = entity.Title,
            FullName = entity.FullName,
            PhoneNumber = entity.PhoneNumber,
            Email = entity.Email,
            SchoolName = entity.SchoolName,
            Major = entity.Major,
            Grade = entity.Grade,
            ClassName = entity.ClassName,
            StudentNumber = entity.StudentNumber,
            Summary = entity.Summary,
            Skills = entity.Skills,
            EducationExperience = entity.EducationExperience,
            InternshipExperience = entity.InternshipExperience,
            ProjectExperience = entity.ProjectExperience,
            CertificateText = entity.CertificateText,
            AttachmentUrl = entity.AttachmentUrl,
            IsDefault = entity.IsDefault,
            VersionNo = entity.VersionNo,
            LastUsedAt = entity.LastUsedAt
        };
    }

    private async Task<List<JobApplicationDto>> MapApplicationDtosAsync(List<JobApplication> items)
    {
        if (items.Count == 0)
        {
            return [];
        }

        using (DataFilter.Disable<IMultiTenant>())
        {
            var jobIds = items.Select(x => x.JobPostingId).Distinct().ToList();
            var studentIds = items.Select(x => x.StudentId).Distinct().ToList();
            var resumeIds = items.Select(x => x.ResumeId).Distinct().ToList();
            var jobs = await _jobPostingRepository.GetListAsync(x => jobIds.Contains(x.Id));
            var students = await _userRepository.GetListAsync(x => studentIds.Contains(x.Id));
            var resumes = await _resumeRepository.GetListAsync(x => resumeIds.Contains(x.Id));
            var jobMap = jobs.ToDictionary(x => x.Id, x => x);
            var studentMap = students.ToDictionary(x => x.Id, x => x);
            var resumeMap = resumes.ToDictionary(x => x.Id, x => x);

            return items.Select(item => MapApplicationDto(item, jobMap, studentMap, resumeMap)).ToList();
        }
    }

    private async Task<JobApplicationDto> MapApplicationDtoAsync(JobApplication entity)
    {
        return (await MapApplicationDtosAsync([entity])).First();
    }

    private static JobApplicationDto MapApplicationDto(
        JobApplication entity,
        Dictionary<Guid, JobPosting> jobMap,
        Dictionary<Guid, IdentityUser> studentMap,
        Dictionary<Guid, StudentResume> resumeMap)
    {
        return new JobApplicationDto
        {
            Id = entity.Id,
            CreationTime = entity.CreationTime,
            LastModificationTime = entity.LastModificationTime,
            JobPostingId = entity.JobPostingId,
            JobTitle = jobMap.GetValueOrDefault(entity.JobPostingId)?.Title,
            CompanyName = jobMap.GetValueOrDefault(entity.JobPostingId)?.CompanyName,
            StudentId = entity.StudentId,
            StudentName = studentMap.TryGetValue(entity.StudentId, out var student) ? GetUserDisplayName(student) : null,
            ResumeId = entity.ResumeId,
            ResumeTitle = resumeMap.GetValueOrDefault(entity.ResumeId)?.Title,
            CoverLetter = entity.CoverLetter,
            Status = entity.Status,
            AppliedAt = entity.AppliedAt,
            ReviewedAt = entity.ReviewedAt,
            EmployerRemark = entity.EmployerRemark
        };
    }

    private async Task<List<InterviewScheduleDto>> MapInterviewDtosAsync(List<InterviewSchedule> items)
    {
        if (items.Count == 0)
        {
            return [];
        }

        using (DataFilter.Disable<IMultiTenant>())
        {
            var jobIds = items.Select(x => x.JobPostingId).Distinct().ToList();
            var studentIds = items.Select(x => x.StudentId).Distinct().ToList();
            var jobs = await _jobPostingRepository.GetListAsync(x => jobIds.Contains(x.Id));
            var students = await _userRepository.GetListAsync(x => studentIds.Contains(x.Id));
            var jobMap = jobs.ToDictionary(x => x.Id, x => x);
            var studentMap = students.ToDictionary(x => x.Id, x => x);

            return items.Select(item => new InterviewScheduleDto
            {
                Id = item.Id,
                CreationTime = item.CreationTime,
                LastModificationTime = item.LastModificationTime,
                ApplicationId = item.ApplicationId,
                JobPostingId = item.JobPostingId,
                JobTitle = jobMap.GetValueOrDefault(item.JobPostingId)?.Title,
                StudentId = item.StudentId,
                StudentName = studentMap.TryGetValue(item.StudentId, out var student) ? GetUserDisplayName(student) : null,
                EmployerUserId = item.EmployerUserId,
                InterviewerName = item.InterviewerName,
                InterviewerPhone = item.InterviewerPhone,
                ScheduledAt = item.ScheduledAt,
                Location = item.Location,
                MeetingUrl = item.MeetingUrl,
                Note = item.Note,
                Result = item.Result,
                Summary = item.Summary,
                ResultComment = item.ResultComment,
                ResultRecordedAt = item.ResultRecordedAt
            }).ToList();
        }
    }

    private async Task<InterviewScheduleDto> MapInterviewDtoAsync(InterviewSchedule entity)
    {
        return (await MapInterviewDtosAsync([entity])).First();
    }

    private async Task<List<EmploymentGuidanceRecordDto>> MapGuidanceDtosAsync(List<EmploymentGuidanceRecord> items)
    {
        if (items.Count == 0)
        {
            return [];
        }

        using (DataFilter.Disable<IMultiTenant>())
        {
            var studentIds = items.Select(x => x.StudentId).Distinct().ToList();
            // AI 记录 TeacherId 为 null，过滤掉避免无效查询
            var teacherIds = items.Where(x => x.TeacherId.HasValue).Select(x => x.TeacherId!.Value).Distinct().ToList();
            var users = await _userRepository.GetListAsync(x => studentIds.Contains(x.Id) || teacherIds.Contains(x.Id));
            var userMap = users.ToDictionary(x => x.Id, x => x);

            return items.Select(item => new EmploymentGuidanceRecordDto
            {
                Id = item.Id,
                CreationTime = item.CreationTime,
                LastModificationTime = item.LastModificationTime,
                StudentId = item.StudentId,
                StudentName = userMap.TryGetValue(item.StudentId, out var student) ? GetUserDisplayName(student) : null,
                ApplicationId = item.ApplicationId,
                TeacherId = item.TeacherId,
                TeacherName = item.TeacherId.HasValue && userMap.TryGetValue(item.TeacherId.Value, out var teacher)
                    ? GetUserDisplayName(teacher)
                    : null,
                Title = item.Title,
                Content = item.Content,
                SourceType = item.SourceType,
                CareerGoal = item.CareerGoal,
                GuidedAt = item.GuidedAt
            }).ToList();
        }
    }

    private async Task<EmploymentGuidanceRecordDto> MapGuidanceDtoAsync(EmploymentGuidanceRecord entity)
    {
        return (await MapGuidanceDtosAsync([entity])).First();
    }

    private async Task<List<EmploymentOutcomeDto>> MapOutcomeDtosAsync(List<EmploymentOutcome> items)
    {
        if (items.Count == 0)
        {
            return [];
        }

        using (DataFilter.Disable<IMultiTenant>())
        {
            var studentIds = items.Select(x => x.StudentId).Distinct().ToList();
            var students = await _userRepository.GetListAsync(x => studentIds.Contains(x.Id));
            var studentMap = students.ToDictionary(x => x.Id, x => x);

            return items.Select(item => new EmploymentOutcomeDto
            {
                Id = item.Id,
                CreationTime = item.CreationTime,
                LastModificationTime = item.LastModificationTime,
                StudentId = item.StudentId,
                StudentName = studentMap.TryGetValue(item.StudentId, out var student) ? GetUserDisplayName(student) : null,
                ApplicationId = item.ApplicationId,
                EmployerName = item.EmployerName,
                JobTitle = item.JobTitle,
                Status = item.Status,
                EmploymentType = item.EmploymentType,
                Region = item.Region,
                SalaryRange = item.SalaryRange,
                StartDate = item.StartDate,
                ConfirmedAt = item.ConfirmedAt,
                Remark = item.Remark,
                IsPrimary = item.IsPrimary
            }).ToList();
        }
    }

    private async Task<EmploymentOutcomeDto> MapOutcomeDtoAsync(EmploymentOutcome entity)
    {
        return (await MapOutcomeDtosAsync([entity])).First();
    }

    private EmployerProfileDto MapEmployerProfile(IdentityUser user)
    {
        return new EmployerProfileDto
        {
            UserId = user.Id,
            ContactName = user.Name,
            PhoneNumber = user.PhoneNumber,
            Email = user.Email,
            CompanyName = user.GetProperty<string>("CompanyName"),
            UnifiedSocialCreditCode = user.GetProperty<string>("UnifiedSocialCreditCode"),
            Position = user.GetProperty<string>("Position"),
            Industry = user.GetProperty<string>("Industry"),
            PartnerSchool = user.GetProperty<string>("PartnerSchool"),
            Remark = user.GetProperty<string>("Remark")
        };
    }

    private async Task<JobPosting> GetManageableJobAsync(Guid id)
    {
        JobPosting entity;
        using (DataFilter.Disable<IMultiTenant>())
        {
            entity = await _jobPostingRepository.GetAsync(id);
        }

        if (!await CanReviewJobsAsync() && entity.EmployerUserId != CurrentUser.Id)
        {
            throw new AbpAuthorizationException();
        }

        return entity;
    }

    private async Task<StudentResume> GetOwnResumeAsync(Guid id)
    {
        var entity = await _resumeRepository.GetAsync(id);
        if (entity.StudentId != CurrentUser.Id)
        {
            throw new AbpAuthorizationException();
        }

        return entity;
    }

    private async Task<JobApplication> GetManageableApplicationAsync(Guid id)
    {
        JobApplication entity;
        using (DataFilter.Disable<IMultiTenant>())
        {
            entity = await _applicationRepository.GetAsync(id);
        }

        if (await CanReviewJobsAsync())
        {
            return entity;
        }

        if (await CanManageApplicationsAsync())
        {
            return entity;
        }

        JobPosting job;
        using (DataFilter.Disable<IMultiTenant>())
        {
            job = await _jobPostingRepository.GetAsync(entity.JobPostingId);
        }

        if (job.EmployerUserId != CurrentUser.Id)
        {
            throw new AbpAuthorizationException();
        }

        return entity;
    }

    private async Task<InterviewSchedule> GetManageableInterviewAsync(Guid id)
    {
        InterviewSchedule entity;
        using (DataFilter.Disable<IMultiTenant>())
        {
            entity = await _interviewRepository.GetAsync(id);
        }

        if (await CanReviewJobsAsync())
        {
            return entity;
        }

        JobPosting job;
        using (DataFilter.Disable<IMultiTenant>())
        {
            job = await _jobPostingRepository.GetAsync(entity.JobPostingId);
        }

        if (job.EmployerUserId != CurrentUser.Id)
        {
            throw new AbpAuthorizationException();
        }

        return entity;
    }

    private async Task DeleteRelatedApplicationsAsync(Guid jobPostingId)
    {
        using (DataFilter.Disable<IMultiTenant>())
        {
            var applications = await _applicationRepository.GetListAsync(x => x.JobPostingId == jobPostingId);
            foreach (var application in applications)
            {
                await _applicationRepository.DeleteAsync(application);
            }
        }
    }

    private async Task DeleteRelatedInterviewsAsync(Guid jobPostingId)
    {
        using (DataFilter.Disable<IMultiTenant>())
        {
            var interviews = await _interviewRepository.GetListAsync(x => x.JobPostingId == jobPostingId);
            foreach (var interview in interviews)
            {
                await _interviewRepository.DeleteAsync(interview);
            }
        }
    }

    private async Task ClearDefaultResumeAsync(Guid studentId)
    {
        var items = await _resumeRepository.GetListAsync(x => x.StudentId == studentId && x.IsDefault);
        foreach (var item in items)
        {
            item.IsDefault = false;
            await _resumeRepository.UpdateAsync(item);
        }
    }

    private async Task<IdentityUser> GetCurrentIdentityUserAsync()
    {
        var currentUserId = GetCurrentUserId();
        using (DataFilter.Disable<IMultiTenant>())
        {
            return await _userRepository.GetAsync(currentUserId);
        }
    }

    private Guid GetCurrentUserId()
    {
        return CurrentUser.Id ?? throw new UserFriendlyException("当前用户未登录。");
    }

    private async Task<bool> CanReviewJobsAsync()
    {
        return await AuthorizationService.IsGrantedAsync(KnowledgeHubPermissions.Employment.ReviewJob);
    }

    private async Task<bool> CanManageApplicationsAsync()
    {
        return await AuthorizationService.IsGrantedAsync(KnowledgeHubPermissions.Employment.ManageApplication);
    }

    private static string GetUserDisplayName(IdentityUser user)
    {
        var displayName = string.Join(" ", new[] { user.Name, user.Surname }.Where(x => !string.IsNullOrWhiteSpace(x))).Trim();
        return string.IsNullOrWhiteSpace(displayName) ? user.UserName ?? user.Email ?? user.Id.ToString("N") : displayName;
    }

    private static EmploymentJobStatus ResolveCreateOrUpdateStatus(EmploymentJobStatus inputStatus, bool canReview)
    {
        if (canReview)
        {
            return inputStatus;
        }

        return inputStatus switch
        {
            EmploymentJobStatus.Published => EmploymentJobStatus.PendingReview,
            EmploymentJobStatus.Rejected => EmploymentJobStatus.PendingReview,
            EmploymentJobStatus.Closed => EmploymentJobStatus.Draft,
            _ => inputStatus
        };
    }

    private static void ValidateJobInput(CreateUpdateJobPostingDto input)
    {
        if (string.IsNullOrWhiteSpace(input.Title))
        {
            throw new UserFriendlyException("岗位名称不能为空。");
        }

        if (string.IsNullOrWhiteSpace(input.Description))
        {
            throw new UserFriendlyException("岗位描述不能为空。");
        }
    }

    private static void ValidateResumeInput(CreateUpdateStudentResumeDto input)
    {
        if (string.IsNullOrWhiteSpace(input.Title))
        {
            throw new UserFriendlyException("简历标题不能为空。");
        }

        if (string.IsNullOrWhiteSpace(input.FullName))
        {
            throw new UserFriendlyException("姓名不能为空。");
        }
    }

    private static Task EnsureIdentitySuccessAsync(AspNetIdentityResult result)
    {
        if (result.Succeeded)
        {
            return Task.CompletedTask;
        }

        var message = string.Join(";", result.Errors.Select(x => x.Description));
        throw new UserFriendlyException(string.IsNullOrWhiteSpace(message) ? "用户信息更新失败。" : message);
    }
}
