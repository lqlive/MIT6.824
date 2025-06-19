namespace Raft.Common.Models;

/// <summary>
/// Raft 日志条目
/// </summary>
public class LogEntry
{
    /// <summary>
    /// 日志条目的任期号
    /// </summary>
    public int Term { get; set; }

    /// <summary>
    /// 日志条目的索引位置
    /// </summary>
    public int Index { get; set; }

    /// <summary>
    /// 客户端命令内容
    /// </summary>
    public string Command { get; set; } = string.Empty;

    /// <summary>
    /// 日志条目创建时间戳
    /// </summary>
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;

    public override string ToString()
    {
        return $"LogEntry[Index={Index}, Term={Term}, Command={Command}]";
    }
}