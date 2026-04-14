using System;
using System.Threading;

namespace BulletHeavenGame
{
    // Потокобезопасный рандом для параллельного спавна
    public static class ThreadSafeRandom
    {
        private static readonly ThreadLocal<Random> localRandom =
            new ThreadLocal<Random>(() => new Random(Guid.NewGuid().GetHashCode()));

        public static Random Instance => localRandom.Value;
    }
}