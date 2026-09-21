using System.Runtime.CompilerServices;

namespace OngekiFumenEditor.Avalonia.Utils
{
    /// <summary>
    /// 进程内唯一令牌生成器（原 <c>RandomHepler.RequestUniqueValue</c>）。
    /// 令牌只承诺「全局唯一 + 只做相等比较」：不承诺可比较新旧（无顺序语义），也不承诺随机（可预测），
    /// 因此不要用它做安全用途。当前消费方是缓存失效判据：
    /// <c>BpmList.ContentVersion</c>，以及依赖它的 MeterChangeList / SoflanList 各自的内容缓存。
    /// </summary>
    public static class NonceGenerator
    {
        private static int next = 0;

        /// <summary>
        /// 取下一个令牌。实现为 Interlocked 自增，故并发调用不会取到相同值；
        /// 累计 2^31 次后回绕，实际不可达。
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int Next()
        {
            return Interlocked.Increment(ref next);
        }
    }
}
