using System;

namespace MapReduce.Common.Utilities
{
    /// <summary>
    /// 哈希辅助类 - 用于确定key应该分配给哪个Reduce任务
    /// </summary>
    public static class HashingHelper
    {
        /// <summary>
        /// 计算key应该分配给哪个Reduce任务
        /// </summary>
        /// <param name="key">键</param>
        /// <param name="reduceCount">Reduce任务总数</param>
        /// <returns>Reduce任务索引</returns>
        public static int HashKey(string key, int reduceCount)
        {
            if (string.IsNullOrEmpty(key))
                return 0;

            // 使用简单的哈希算法，确保相同的key总是分配给同一个Reduce任务
            var hashCode = key.GetHashCode();
            return Math.Abs(hashCode) % reduceCount;
        }

        /// <summary>
        /// 生成中间文件名
        /// </summary>
        /// <param name="mapTaskId">Map任务ID</param>
        /// <param name="reduceTaskId">Reduce任务ID</param>
        /// <returns>中间文件名</returns>
        public static string GetIntermediateFileName(int mapTaskId, int reduceTaskId)
        {
            return $"mr-{mapTaskId}-{reduceTaskId}";
        }

        /// <summary>
        /// 生成最终输出文件名
        /// </summary>
        /// <param name="reduceTaskId">Reduce任务ID</param>
        /// <returns>输出文件名</returns>
        public static string GetOutputFileName(int reduceTaskId)
        {
            return $"mr-out-{reduceTaskId}";
        }
    }
}