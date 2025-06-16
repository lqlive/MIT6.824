using System;
using System.Collections.Generic;
using System.Runtime.Serialization;

namespace MapReduce.Common.Models
{
    /// <summary>
    /// MapReduce任务
    /// </summary>
    [DataContract]
    public class MapReduceTask
    {
        /// <summary>
        /// 任务ID
        /// </summary>
        [DataMember]
        public int TaskId { get; set; }

        /// <summary>
        /// 任务类型
        /// </summary>
        [DataMember]
        public TaskType TaskType { get; set; }

        /// <summary>
        /// 任务状态
        /// </summary>
        [DataMember]
        public TaskStatus Status { get; set; }

        /// <summary>
        /// 输入文件路径列表
        /// </summary>
        [DataMember]
        public List<string> InputFiles { get; set; } = new List<string>();

        /// <summary>
        /// 输出文件路径
        /// </summary>
        [DataMember]
        public string? OutputFile { get; set; }

        /// <summary>
        /// Reduce任务数量（用于Map任务的中间文件分片）
        /// </summary>
        [DataMember]
        public int ReduceCount { get; set; }

        /// <summary>
        /// Reduce任务索引（用于Reduce任务）
        /// </summary>
        [DataMember]
        public int ReduceIndex { get; set; }

        /// <summary>
        /// 任务分配时间
        /// </summary>
        [DataMember]
        public DateTime AssignedTime { get; set; }

        /// <summary>
        /// 分配给的Worker ID
        /// </summary>
        [DataMember]
        public string? AssignedWorkerId { get; set; }

        /// <summary>
        /// 中间文件路径列表（用于Reduce任务）
        /// </summary>
        [DataMember]
        public List<string> IntermediateFiles { get; set; } = new List<string>();
    }
}