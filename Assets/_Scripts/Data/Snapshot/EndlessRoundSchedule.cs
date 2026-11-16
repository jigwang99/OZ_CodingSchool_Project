using System;
using System.Collections.Generic;
using System.Linq;

namespace AutoBattler.Data.Snapshot
{
    /// <summary>도전 시드와 라운드 번호로 무한 전투 편성을 추첨.</summary>
    internal sealed class EndlessRoundSchedule
    {
        public const int RoundsPerSegment = 5;
        public const int StartingLives = 3;
        private const int OpeningRounds = 15;
        private const int ExclusiveChancePercent = 20;
        private static readonly int[] AugmentRounds = { 2, 6, 11 };
        private readonly RoundDefinition[][] opening;
        private readonly RoundDefinition[] battles;
        private readonly RoundDefinition[] bosses;
        private readonly RoundDefinition[] exclusiveBattles;
        private readonly RoundDefinition[] exclusiveBosses;

        public EndlessRoundSchedule(IEnumerable<RoundDefinition> campaign, IEnumerable<RoundDefinition> exclusive)
        {
            RoundDefinition[] campaignRounds = campaign.OrderBy(round => round.TemplateId).ToArray();
            RoundDefinition[] exclusiveRounds = exclusive.OrderBy(round => round.TemplateId).ToArray();
            opening = Enumerable.Range(1, OpeningRounds)
                .Select(id => campaignRounds.Where(round => round.Id == id && round.IsBoss == IsBossRound(id)
                    && (id != 1 || round.Enemies.Count == 1)).ToArray())
                .ToArray();
            battles = campaignRounds.Where(round => !round.IsBoss).ToArray();
            bosses = campaignRounds.Where(round => round.IsBoss).ToArray();
            exclusiveBattles = exclusiveRounds.Where(round => !round.IsBoss).ToArray();
            exclusiveBosses = exclusiveRounds.Where(round => round.IsBoss).ToArray();
            if (battles.Length == 0 || bosses.Length == 0)
                throw new ArgumentException("무한 모드에 사용할 일반전·보스전 편성이 없음.");
            for (int index = 0; index < opening.Length; index++)
                if (opening[index].Length == 0)
                    throw new ArgumentException($"무한 모드 {index + 1}라운드의 동번호 전투 편성이 없음.");
        }

        public IEnumerable<int> AugmentRoundIds => AugmentRounds;
        public static bool IsBossRound(int round) => round % RoundsPerSegment == 0;

        public RoundDefinition GetRound(int round, int seed, int savedTemplateId)
        {
            if (round <= 0) throw new ArgumentOutOfRangeException(nameof(round));
            bool boss = IsBossRound(round);
            bool openingRound = round <= OpeningRounds;
            RoundDefinition[] normal = openingRound ? opening[round - 1] : boss ? bosses : battles;
            RoundDefinition[] exclusive = openingRound ? Array.Empty<RoundDefinition>() : boss ? exclusiveBosses : exclusiveBattles;
            RoundDefinition template = savedTemplateId > 0
                ? Array.Find(normal, entry => entry.TemplateId == savedTemplateId)
                    ?? Array.Find(exclusive, entry => entry.TemplateId == savedTemplateId)
                : null;
            if (template == null)
            {
                // 상점·증강 난수와 분리해 미리보기나 UI 호출 순서가 적 편성에 영향을 주지 않도록 처리.
                Random random = new Random(unchecked(seed ^ (int)((uint)round * 0x9E3779B9u)));
                RoundDefinition[] pool = exclusive.Length > 0 && random.Next(100) < ExclusiveChancePercent ? exclusive : normal;
                template = pool[random.Next(pool.Length)];
            }
            return new RoundDefinition(round, template, Array.IndexOf(AugmentRounds, round) >= 0);
        }

        public float HealthMultiplier(int round) => Scale(round, .04f);
        public float AttackMultiplier(int round) => Scale(round, .02f);

        private float Scale(int round, float growth)
        {
            // 큰 라운드 값에서도 기존 정수 스탯 계산 범위 유지.
            return Math.Min(100f, 1f + Math.Max(0, round - OpeningRounds) * growth);
        }
    }
}
