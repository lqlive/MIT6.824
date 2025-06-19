namespace Raft.Common.Models;

/// <summary>
/// Raft 节点状态枚举
/// </summary>
public enum RaftNodeState
{
    /// <summary>
    /// 跟随者 - 被动接收来自Leader的指令
    /// </summary>
    Follower,

    /// <summary>
    /// 候选者 - 参与Leader选举过程
    /// </summary>
    Candidate,

    /// <summary>
    /// 领导者 - 处理客户端请求，管理日志复制
    /// </summary>
    Leader
}