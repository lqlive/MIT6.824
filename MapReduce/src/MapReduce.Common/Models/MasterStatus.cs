using System.Collections.Generic;
using System.Runtime.Serialization;

namespace MapReduce.Common.Models
{
    /// <summary>
    /// Master状态信息
    /// </summary>
    [DataContract]
    public class MasterStatus
    {
        /// <summary>
        /// 总Map任务数
        /// </summary>
        [DataMember]
        public int TotalMapTasks { get; set; }

        /// <summary>
        /// 已完成Map任务数
        /// </summary>
        [DataMember]
        public int CompletedMapTasks { get; set; }

        /// <summary>
        /// 总Reduce任务数
        /// </summary>
        [DataMember]
        public int TotalReduceTasks { get; set; }

        /// <summary>
        /// 已完成Reduce任务数
        /// </summary>
        [DataMember]
        public int CompletedReduceTasks { get; set; }

        /// <summary>
        /// 当前阶段（Map或Reduce）
        /// </summary>
        [DataMember]
        public string? CurrentPhase { get; set; }

        /// <summary>
        /// 活跃Worker列表
        /// </summary>
        [DataMember]
        public List<string> ActiveWorkers { get; set; } = new List<string>();

        /// <summary>
        /// 是否所有任务都已完成
        /// </summary>
        [DataMember]
        public bool IsCompleted { get; set; }
    }
}