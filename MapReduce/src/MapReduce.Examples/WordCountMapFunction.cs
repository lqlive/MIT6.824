using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using MapReduce.Common.Interfaces;

namespace MapReduce.Examples
{
    /// <summary>
    /// 单词计数Map函数实现
    /// </summary>
    public class WordCountMapFunction : IMapFunction
    {
        public IEnumerable<KeyValuePair<string, string>> Map(string filename, string content)
        {
            // 使用正则表达式分割单词，只保留字母
            var words = Regex.Split(content.ToLower(), @"[^a-zA-Z]+")
                            .Where(word => !string.IsNullOrWhiteSpace(word));

            // 为每个单词生成键值对 (word, "1")
            foreach (var word in words)
            {
                yield return new KeyValuePair<string, string>(word.Trim(), "1");
            }
        }
    }
}