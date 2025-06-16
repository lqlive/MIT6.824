using System.Collections.Generic;

namespace MapReduce.Common.Interfaces
{
    /// <summary>
    /// Map函数接口
    /// </summary>
    public interface IMapFunction
    {
        /// <summary>
        /// 执行Map操作
        /// </summary>
        /// <param name="filename">输入文件名</param>
        /// <param name="content">文件内容</param>
        /// <returns>键值对列表</returns>
        IEnumerable<KeyValuePair<string, string>> Map(string filename, string content);
    }
}