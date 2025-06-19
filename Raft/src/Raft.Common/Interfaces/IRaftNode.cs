using Raft.Common.Models;

namespace Raft.Common.Interfaces;

/// <summary>
/// Raft 节点核心接口
/// </summary>
public interface IRaftNode
{
    /// <summary>
    /// 节点ID
    /// </summary>
    string NodeId { get; }

    /// <summary>
    /// 当前状态
    /// </summary>
    RaftNodeState State { get; }

    /// <summary>
    /// 当前任期
    /// </summary>
    int CurrentTerm { get; }

    /// <summary>
    /// 是否为Leader
    /// </summary>
    bool IsLeader { get; }

    /// <summary>
    /// RequestVote RPC - 候选人使用来收集选票
    /// </summary>
    /// <param name="request">投票请求</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>投票响应</returns>
    Task<VoteResponse> RequestVoteAsync(VoteRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// AppendEntries RPC - 领导人使用来复制日志条目，也用作心跳
    /// </summary>
    /// <param name="request">追加条目请求</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>追加条目响应</returns>
    Task<AppendEntriesResponse> AppendEntriesAsync(AppendEntriesRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// 启动Raft节点
    /// </summary>
    /// <param name="cancellationToken">取消令牌</param>
    Task StartAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// 停止Raft节点
    /// </summary>
    /// <param name="cancellationToken">取消令牌</param>
    Task StopAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// 提交客户端命令（仅Leader可用）
    /// </summary>
    /// <param name="command">客户端命令</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>是否成功提交</returns>
    Task<bool> SubmitCommandAsync(string command, CancellationToken cancellationToken = default);
}