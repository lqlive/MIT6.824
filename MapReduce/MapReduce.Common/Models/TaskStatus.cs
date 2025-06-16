using System.Runtime.Serialization;

namespace MapReduce.Common.Models
{
    /// <summary>
    /// 任务状态
    /// </summary>
    [DataContract]
    public enum TaskStatus
    {
        /// <summary>
        /// 空闲状态，等待分配
        /// </summary>
        [EnumMember]
        Idle,

        /// <summary>
        /// 正在执行中
        /// </summary>
        [EnumMember]
        InProgress,

        /// <summary>
        /// 已完成
        /// </summary>
        [EnumMember]
        Completed,

        /// <summary>
        /// 执行失败
        /// </summary>
        [EnumMember]
        Failed
    }
}