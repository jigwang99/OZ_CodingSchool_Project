using System;

namespace AutoBattler.Battle.Combat
{
    /// <summary>피해 증폭과 방어 무시를 반영한 최종 피해 계산. 보호막과 체력 변경은 호출 측에서 처리.</summary>
    public static class DamageResolver
    {
        public static int Calculate(int attack,int defense,float multiplier=1f,float armorIgnoreRatio=0f)
        {
            if(attack<=0 || multiplier<=0f) return 0;
            double armor=Math.Max(0,defense)*(1-Math.Max(0,Math.Min(1,armorIgnoreRatio)));
            double damage=attack*(double)multiplier*100/(100+armor);
            return (int)Math.Max(1,Math.Min(int.MaxValue,Math.Floor(damage)));
        }
    }
}
