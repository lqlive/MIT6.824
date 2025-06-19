namespace Raft.Common.Models;

/// <summary>
/// AppendEntries RPC 请求消息
/// </summary>
public class AppendEntriesRequest
{
    /// <summary>
    /// 领导人的任期号
    /// </summary>
    public int Term { get; set; }

    /// <summary>
    /// 领导人的ID，以便于跟随者重定向请求
    /// </summary>
    public string LeaderId { get; set; } = string.Empty;

    /// <summary>
    /// 新日志条目紧随之前的索引值
    /// </summary>
    public int PrevLogIndex { get; set; }

    /// <summary>
    /// PrevLogIndex条目的任期号
    /// </summary>
    public int PrevLogTerm { get; set; }

    /// <summary>
    /// 准备存储的日志条目（表示心跳时为空；一次性发送多个是为了提高效率）
    /// </summary>
    public List<LogEntry> Entries { get; set; } = new();

    /// <summary>
    /// 领导人已经提交的日志的索引值
    /// </summary>
    public int LeaderCommit { get; set; }
}

/// <summary>
/// AppendEntries RPC 响应消息
/// </summary>
public class AppendEntriesResponse
{
    /// <summary>
    /// 当前的任期号，用于领导人去更新自己
    /// </summary>
    public int Term { get; set; }

    /// <summary>
    /// 跟随者包含了匹配上PrevLogIndex和PrevLogTerm的日志时为真
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// 用于快速回退的优化字段（可选）
    /// </summary>
    public int ConflictIndex { get; set; } = -1;
}