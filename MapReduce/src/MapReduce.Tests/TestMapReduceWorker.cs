using MapReduce.Worker;
using MapReduce.Common.Contracts;
using MapReduce.Common.Interfaces;
using MapReduce.Examples;
using System.Reflection;

namespace MapReduce.Tests
{
    /// <summary>
    /// 用于测试的MapReduceWorker实现
    /// 使用DirectMasterService而不是HTTP通信
    /// </summary>
    public class TestMapReduceWorker : MapReduceWorker
    {
        public TestMapReduceWorker() : base("test-endpoint", "test-output")
        {
            // 使用反射替换内部的HTTP服务为DirectMasterService
            var masterServiceField = typeof(MapReduceWorker).GetField("_masterService",
                BindingFlags.NonPublic | BindingFlags.Instance);

            if (masterServiceField != null)
            {
                masterServiceField.SetValue(this, new DirectMasterService());
            }
        }
    }
}