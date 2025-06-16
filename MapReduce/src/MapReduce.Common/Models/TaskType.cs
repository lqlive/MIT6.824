using System.Runtime.Serialization;

namespace MapReduce.Common.Models
{
    /// <summary>
    /// MapReduce任务类型
    /// </summary>
    [DataContract]
    public enum TaskType
    {
        /// <summary>
        /// Map任务
        /// </summary>
        [EnumMember]
        Map,

        /// <summary>
        /// Reduce任务
        /// </summary>
        [EnumMember]
        Reduce
    }
}