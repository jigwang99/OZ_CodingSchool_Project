using System;

namespace AutoBattler.Battle.Units
{
    /// <summary>성급별 원본 기물 수와 기존 단계 배율을 공통으로 계산.</summary>
    public static class UnitStarRules
    {
        public const int Maximum=3;
        public static int Copies(int star)=>star switch
        {
            1=>1,2=>3,3=>9,_=>throw new ArgumentOutOfRangeException(nameof(star))
        };
        public static float StatMultiplier(int star,float stepMultiplier)
        {
            if(star<1 || star>Maximum) throw new ArgumentOutOfRangeException(nameof(star));
            return (float)Math.Pow(stepMultiplier,star-1);
        }
        public static int SalePrice(int cost,int star)=>cost*Copies(star)-(star-1);
    }
}
