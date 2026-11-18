using System;

namespace AutoBattler.Run
{
    /// <summary>추첨마다 샘플 한 개를 사용하여 시드와 호출 횟수로 난수 위치 복원.</summary>
    public sealed class RunRandom
    {
        private readonly Random random;
        public int Seed { get; }
        public int DrawCount { get; private set; }

        public RunRandom(int seed,int drawCount=0)
        {
            if(drawCount<0 || drawCount>1000000) throw new ArgumentOutOfRangeException(nameof(drawCount));
            Seed=seed; random=new Random(seed);
            for(int i=0;i<drawCount;i++) random.NextDouble();
            DrawCount=drawCount;
        }

        public int Next(int maximum)
        {
            if(maximum<=0) throw new ArgumentOutOfRangeException(nameof(maximum));
            DrawCount++;
            return (int)(random.NextDouble()*maximum);
        }
    }
}
