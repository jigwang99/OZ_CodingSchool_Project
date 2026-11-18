using System;

namespace AutoBattler.Pooling
{
    /// <summary>풀 키, 대여 초기화와 오브젝트 자체의 반환 요청 제공.</summary>
    public interface IPoolable
    {
        Enum PoolKey { get; }
        void Init();
        void ReturnToPool();
    }
}
