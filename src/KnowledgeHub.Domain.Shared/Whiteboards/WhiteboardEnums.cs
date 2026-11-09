namespace KnowledgeHub.Whiteboards;

/// <summary>白板状态。</summary>
public enum WhiteboardStatus : byte
{
    /// <summary>草稿（仅创建者可见）。</summary>
    Draft = 0,

    /// <summary>进行中（成员可见可协作）。</summary>
    Active = 1,

    /// <summary>已归档（只读）。</summary>
    Archived = 2,
}

/// <summary>白板成员角色。</summary>
public enum WhiteboardMemberRole : byte
{
    /// <summary>创建者：可改信息、邀请成员、清空画布。</summary>
    Owner = 0,

    /// <summary>共创者：可增删改画布元素。</summary>
    Editor = 1,

    /// <summary>只读观察者。</summary>
    Viewer = 2,
}

/// <summary>画布元素类型（与前端 Konva 渲染分支一一对应）。</summary>
public enum WhiteboardElementType : byte
{
    /// <summary>自由文本。</summary>
    Text = 0,

    /// <summary>便签（带底色与作者署名）。</summary>
    Note = 1,

    /// <summary>图片（素材来自课程资源）。</summary>
    Image = 2,

    /// <summary>视频（素材来自课程资源）。</summary>
    Video = 3,
}
