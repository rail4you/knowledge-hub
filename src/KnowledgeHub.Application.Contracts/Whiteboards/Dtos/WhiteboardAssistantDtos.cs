using System;
using System.Collections.Generic;

namespace KnowledgeHub.Whiteboards.Dtos;

/// <summary>白板助手的会话轮次（前端维护，回传给后端做多轮上下文）。</summary>
public class WhiteboardAssistantTurnDto
{
    /// <summary>user / assistant。</summary>
    public string Role { get; set; } = "user";
    public string Content { get; set; } = string.Empty;
}

/// <summary>向白板助手提问。</summary>
public class WhiteboardAssistantAskInputDto
{
    public Guid WhiteboardId { get; set; }

    /// <summary>老师这一轮说的话（也可以是要补全的标题式短句）。</summary>
    public string Message { get; set; } = string.Empty;

    /// <summary>期望产出：<c>text</c>（默认，返回文字）或 <c>image</c>（返回一张教学配图）。</summary>
    public string Mode { get; set; } = "text";

    /// <summary>画布上当前选中的文本（老师点了一段板书再问，助手就知道要改哪一段）。</summary>
    public string? SelectedText { get; set; }

    /// <summary>本次会话前面几轮的问答（用于「继续扩写」「再具体些」这类上下文承接）。</summary>
    public List<WhiteboardAssistantTurnDto>? History { get; set; }
}

/// <summary>助手实际用到的上下文摘要（前端在面板上展示「助手参考了什么」）。</summary>
public class WhiteboardAssistantContextDto
{
    public Guid? CourseId { get; set; }
    public string? CourseName { get; set; }
    public string? BoardTitle { get; set; }
    /// <summary>画布上被读取的文本段数。</summary>
    public int CanvasTextCount { get; set; }
    /// <summary>被读取的教师讨论消息条数。</summary>
    public int ChatMessageCount { get; set; }
    public string? SelectedText { get; set; }
}

/// <summary>助手回复。</summary>
public class WhiteboardAssistantReplyDto
{
    /// <summary>文字回复（Markdown 片段，可直接贴到白板或替换所选文本）。</summary>
    public string Content { get; set; } = string.Empty;

    /// <summary>图片模式下的图片地址（已持久化到本系统存储）。</summary>
    public string? ImageUrl { get; set; }

    /// <summary>图片模式实际使用的画面描述（便于老师复用 / 微调）。</summary>
    public string? ImagePrompt { get; set; }

    public WhiteboardAssistantContextDto Context { get; set; } = new();
}
