using System;
using System.IO;

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
        /// <param name="outputDirectory">输出目录，为空则使用当前目录</param>
        /// <returns>中间文件的完整路径</returns>
        public static string GetIntermediateFileName(int mapTaskId, int reduceTaskId, string outputDirectory = "")
        {
            var fileName = $"mr-{mapTaskId}-{reduceTaskId}";

            if (string.IsNullOrEmpty(outputDirectory))
                return fileName;

            return Path.Combine(outputDirectory, fileName);
        }

        /// <summary>
        /// 生成最终输出文件名
        /// </summary>
        /// <param name="reduceTaskId">Reduce任务ID</param>
        /// <param name="outputDirectory">输出目录，为空则使用当前目录</param>
        /// <returns>输出文件的完整路径</returns>
        public static string GetOutputFileName(int reduceTaskId, string outputDirectory = "")
        {
            var fileName = $"mr-out-{reduceTaskId}";

            if (string.IsNullOrEmpty(outputDirectory))
                return fileName;

            return Path.Combine(outputDirectory, fileName);
        }

        /// <summary>
        /// 确保输出目录存在
        /// </summary>
        /// <param name="outputDirectory">输出目录</param>
        public static void EnsureOutputDirectoryExists(string outputDirectory)
        {
            if (!string.IsNullOrEmpty(outputDirectory) && !Directory.Exists(outputDirectory))
            {
                Directory.CreateDirectory(outputDirectory);
            }
        }
    }
}