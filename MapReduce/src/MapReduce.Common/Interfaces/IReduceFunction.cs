using System.Collections.Generic;

namespace MapReduce.Common.Interfaces
{
    /// <summary>
    /// Reduce函数接口
    /// </summary>
    public interface IReduceFunction
    {
        /// <summary>
        /// 执行Reduce操作
        /// </summary>
        /// <param name="key">键</param>
        /// <param name="values">该键对应的所有值</param>
        /// <returns>归约后的值</returns>
        string Reduce(string key, IEnumerable<string> values);
    }
} 