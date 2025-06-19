namespace Raft.Common.Models;

/// <summary>
/// RequestVote RPC 请求消息
/// </summary>
public class VoteRequest
{
    /// <summary>
    /// 候选人的任期号
    /// </summary>
    public int Term { get; set; }

    /// <summary>
    /// 请求投票的候选人ID
    /// </summary>
    public string CandidateId { get; set; } = string.Empty;

    /// <summary>
    /// 候选人最后日志条目的索引值
    /// </summary>
    public int LastLogIndex { get; set; }

    /// <summary>
    /// 候选人最后日志条目的任期号
    /// </summary>
    public int LastLogTerm { get; set; }
}

/// <summary>
/// RequestVote RPC 响应消息
/// </summary>
public class VoteResponse
{
    /// <summary>
    /// 当前任期号，用于候选人更新自己
    /// </summary>
    public int Term { get; set; }

    /// <summary>
    /// 候选人是否获得选票
    /// </summary>
    public bool VoteGranted { get; set; }
}