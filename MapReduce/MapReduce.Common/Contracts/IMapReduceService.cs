using System.ServiceModel;
using System.Threading.Tasks;
using MapReduce.Common.Models;

namespace MapReduce.Common.Contracts
{
    /// <summary>
    /// MapReduce服务接口 - Master和Worker之间的RPC通信
    /// </summary>
    [ServiceContract]
    public interface IMapReduceService
    {
        /// <summary>
        /// Worker请求任务
        /// </summary>
        /// <param name="workerId">Worker ID</param>
        /// <returns>分配的任务，如果没有任务返回null</returns>
        [OperationContract]
        Task<MapReduceTask?> RequestTaskAsync(string workerId);

        /// <summary>
        /// Worker报告任务完成
        /// </summary>
        /// <param name="workerId">Worker ID</param>
        /// <param name="taskId">任务ID</param>
        /// <param name="success">是否成功完成</param>
        /// <param name="outputFiles">输出文件路径列表</param>
        [OperationContract]
        Task ReportTaskCompletionAsync(string workerId, int taskId, bool success, string[] outputFiles);

        /// <summary>
        /// Worker心跳检测
        /// </summary>
        /// <param name="workerId">Worker ID</param>
        [OperationContract]
        Task SendHeartbeatAsync(string workerId);

        /// <summary>
        /// 检查所有任务是否完成
        /// </summary>
        /// <returns>是否所有任务都已完成</returns>
        [OperationContract]
        Task<bool> IsAllTasksCompletedAsync();

        /// <summary>
        /// 获取Master状态信息
        /// </summary>
        /// <returns>Master状态信息</returns>
        [OperationContract]
        Task<MasterStatus> GetMasterStatusAsync();
    }
}