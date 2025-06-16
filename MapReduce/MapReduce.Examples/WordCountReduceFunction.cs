using System.Collections.Generic;
using System.Linq;
using MapReduce.Common.Interfaces;

namespace MapReduce.Examples
{
    /// <summary>
    /// 单词计数Reduce函数实现
    /// </summary>
    public class WordCountReduceFunction : IReduceFunction
    {
        public string Reduce(string key, IEnumerable<string> values)
        {
            // 计算该单词出现的总次数
            var count = values.Count(v => v == "1");
            return count.ToString();
        }
    }
} 