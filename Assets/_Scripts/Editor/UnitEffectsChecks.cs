#if UNITY_EDITOR
using System;
using System.Linq;
using System.Threading;
using AutoBattler.Assets;
using AutoBattler.Battle.Combat;
using AutoBattler.Battle.Flow;
using AutoBattler.Battle.Grid;
using AutoBattler.Battle.Placement;
using AutoBattler.Battle.Units;
using AutoBattler.Data.Units;
using AutoBattler.Run;
using Cysharp.Threading.Tasks;
using UnityEditor;
using UnityEngine;

namespace AutoBattler.EditorTools
{
    public static class UnitEffectsChecks
    {
        private static int checks;
        private static void Expect(bool condition,string message)
        { if(!condition) throw new Exception("Effects check: "+message); checks++; }
        private static bool Near(float a,float b)=>Mathf.Abs(a-b)<.0001f;
        [MenuItem("PixelTactics/Checks/Run Effects Domain Checks")]
        public static void RunDomain()
        {
            checks=0;
            var stats=new UnitStatState();
            var baseStats=new UnitStat(UnitType.Character2,100,100,100,1f,1,1f);
            stats.Initialize(baseStats);
            var skill=new ModifierSource(ModifierSourceKind.Skill,2001,1);
            var trait=new ModifierSource(ModifierSourceKind.ClassTrait,4001);
            var augment=new ModifierSource(ModifierSourceKind.Augment,5001);
            var flat=stats.AddModifier(skill,new StatModifierSpec(StatType.AttackDamage,ModifierOperation.Flat,20f));
            var percent=stats.AddModifier(trait,new StatModifierSpec(StatType.AttackDamage,ModifierOperation.Percent,.25f));
            Expect(stats.Current.AttackDamage==150 && baseStats.AttackDamage==100,"기본값 보존과 고정·비율 계산");
            stats.RemoveModifier(flat); Expect(stats.Current.AttackDamage==125,"개별 Handle 해제");
            stats.RemoveSource(skill); Expect(stats.Current.AttackDamage==125,"다른 출처 유지");
            stats.ReplaceSource(trait,new StatModifierSpec(StatType.AttackDamage,ModifierOperation.Percent,.25f));
            stats.ReplaceSource(trait,new StatModifierSpec(StatType.AttackDamage,ModifierOperation.Percent,.25f));
            Expect(stats.Current.AttackDamage==125 && stats.ModifierCount==1,"출처 재계산의 중복 방지");
            stats.AddModifier(augment,new StatModifierSpec(StatType.AttackSpeed,ModifierOperation.Percent,.2f));
            Expect(Near(stats.Current.AttackCooldown,1f/1.2f),"공격 간격 계산");
            stats.AddModifier(skill,new StatModifierSpec(StatType.CritChance,ModifierOperation.Flat,.2f));
            stats.AddModifier(skill,new StatModifierSpec(StatType.CritMultiplier,ModifierOperation.Flat,.3f));
            Expect(Near(stats.CritChance,.3f) && Near(stats.CritMultiplier,1.8f),"치명타 수정자");
            stats.AddModifier(skill,new StatModifierSpec(StatType.SkillPower,ModifierOperation.Percent,.2f));
            Expect(Near(stats.SkillPower,1.2f),"스킬 위력 수정자");
            stats.Initialize(baseStats);
            var fresh=stats.AddModifier(skill,new StatModifierSpec(StatType.AttackDamage,ModifierOperation.Flat,10f));
            Expect(!stats.RemoveModifier(flat) && stats.Current.AttackDamage==110,"재초기화 후 이전 Handle 무효");
            stats.ClearModifiers();

            var health=new UnitHealthState(100);
            var effects=new UnitEffectState(stats,health.Heal);
            var buff=new EffectDefinition(3001,EffectKind.StatModifier,25f,2f,stat:StatType.AttackDamage);
            var handle=effects.Apply(buff,skill);
            Expect(stats.Current.AttackDamage==125,"효과에서 수정자 등록");
            effects.Tick(1f); effects.Apply(buff,skill);
            Expect(effects.Active.Count==1 && Near(effects.Active[0].RemainingSeconds,2f),"동일 효과 갱신과 시간 연장");
            effects.Tick(2.1f);
            Expect(stats.Current.AttackDamage==100 && !effects.Remove(handle),"만료 후 수정자·Handle 정리");
            var stack=new EffectDefinition(3002,EffectKind.StatModifier,10f,2f,EffectStacking.Stack);
            effects.Apply(stack,skill); effects.Apply(stack,skill);
            Expect(stats.Current.AttackDamage==120,"중첩 가능한 효과");
            effects.RemoveSource(skill); Expect(stats.Current.AttackDamage==100,"출처 효과 일괄 해제");

            var weakSlow=new EffectDefinition(3003,EffectKind.Slow,.2f,5f);
            var strongSlow=new EffectDefinition(3004,EffectKind.Slow,.5f,1f);
            effects.Apply(weakSlow,trait); effects.Apply(strongSlow,skill);
            Expect(Near(effects.SlowRate,.5f),"최강 감속 적용");
            effects.Tick(1.1f); Expect(Near(effects.SlowRate,.2f),"강한 감속 만료 후 약한 감속 유지");
            var stun=effects.Apply(new EffectDefinition(3005,EffectKind.Stun,1f,1f),skill);
            Expect(effects.IsStunned,"기절 상태"); effects.Remove(stun); Expect(!effects.IsStunned,"기절 해제");
            effects.Clear();

            effects.Apply(new EffectDefinition(3006,EffectKind.Shield,10f,1f),skill);
            effects.Apply(new EffectDefinition(3007,EffectKind.Shield,30f,5f),trait);
            Expect(effects.AbsorbDamage(15)==0 && effects.Shield==25 && effects.Active.Count==1,"먼저 만료할 보호막 소진");
            Expect(effects.AbsorbDamage(35)==10 && effects.Shield==0,"잔여 피해만 체력으로 전달");
            var shield=new EffectDefinition(3008,EffectKind.Shield,20f,2f);
            effects.Apply(shield,skill); effects.AbsorbDamage(10); effects.Apply(shield,skill);
            Expect(effects.Shield==20 && effects.Active.Count==1,"보호막 갱신의 중복 누적 방지");
            effects.Tick(2.1f); Expect(effects.Shield==0,"보호막 만료");
            health.TakeDamage(90);
            effects.Apply(new EffectDefinition(3009,EffectKind.Heal,200f),skill);
            Expect(health.CurrentHp==100 && effects.Active.Count==0,"즉시 회복과 최대 체력 제한");
            health.TakeDamage(100); Expect(health.Heal(100)==0,"사망 대상 회복 차단");
            health.RestoreFullHealth(); health.SetMaxHp(200);
            Expect(health.CurrentHp==100 && health.MaxHp==200,"최대 체력 증가로 자동 회복하지 않음");
            health.Heal(100); health.SetMaxHp(100); Expect(health.CurrentHp==100,"최대 체력 감소 제한");
            health.Died+=()=>health.SetMaxHp(50);
            health.TakeDamage(1000); Expect(health.MaxHp==50 && !health.IsAlive,"사망 처리 중 최대 체력 변경 반영");
            Expect(DamageResolver.Calculate(100,100)==50,"방어 계산");
            Expect(DamageResolver.Calculate(100,100,1f,.5f)==66,"부분 방어 무시와 절삭");
            Expect(DamageResolver.Calculate(100,100,1f,1f)==100,"완전 방어 무시");
            Expect(DamageResolver.Calculate(int.MaxValue,0,10f)==int.MaxValue,"큰 피해 계산의 오버플로 방지");
            Debug.Log("UNIT_EFFECTS_DOMAIN_PASS "+checks);
        }

        public static async UniTask RunPlay(RunController run)
        {
            using var assets=new AssetService();
            var root=new GameObject("EffectProbes");
            var pool=new UnitPrefabPool(assets,root.transform);
            var flowRoot=new GameObject("EffectProbeFlow",typeof(BattleFlowController));
            var flow=flowRoot.GetComponent<BattleFlowController>();
            var definition=run.State.Data.Units.Values.First();
            var reference=run.Catalog.VisualPrefab(definition.VisualId);
            await pool.PreloadAsync(reference,CancellationToken.None);
            var ally=pool.Rent(reference); var enemy=pool.Rent(reference);
            var skill=new ModifierSource(ModifierSourceKind.Skill,2001,700001);
            var trait=new ModifierSource(ModifierSourceKind.ClassTrait,4001);
            try
            {
                var board=new BoardState(); var placement=new PlacementService(board);
                var state=new UnitState(700001); var start=HexCoord.FromOffset(2,3);
                Expect(placement.TryRegisterUnit(state,UnitLocation.OnBoard(start)),"검사 유닛 배치");
                ally.Configure(definition,1,state.InstanceId,UnitTeam.Ally,run.State.Data,flow,run.Grid,run.Bench);
                ally.View.Bind(state,board); ally.transform.position=run.Grid.CellToWorld(start);
                enemy.Configure(definition,1,700002,UnitTeam.Enemy,run.State.Data,flow,run.Grid,run.Bench,start.GetNeighbor(0));
                enemy.transform.position=run.Grid.CellToWorld(start.GetNeighbor(0));
                ally.gameObject.SetActive(true); enemy.gameObject.SetActive(true);
                ally.BeginBattle(); enemy.BeginBattle(); Expect(flow.TryStartCombat(),"검사 전투 시작");

                ally.StatModifiers.ReplaceSource(trait,new StatModifierSpec(StatType.Defense,ModifierOperation.Flat,100f));
                int expected=DamageResolver.Calculate(20,definition.Armor+100);
                Expect(ally.Health.TakeDamage(20)==expected,"변경된 방어력으로 피해를 한 번 계산");
                ally.Health.RestoreFullHealth();
                int resolved=DamageResolver.Calculate(100,ally.Stats.Defense);
                ally.Effects.Apply(new EffectDefinition(3010,EffectKind.Shield,resolved-1,5f),skill);
                Expect(ally.Health.TakeDamage(100)==1,"방어력 적용 후 보호막 흡수");
                ally.Effects.Apply(new EffectDefinition(3011,EffectKind.Heal,10000f),skill);
                Expect(ally.Health.CurrentHp==ally.Health.MaxHp,"실제 체력 회복");

                var maxHp=new StatModifierSpec(StatType.MaxHp,ModifierOperation.Flat,100f);
                ally.StatModifiers.ReplaceSource(trait,maxHp); ally.Health.Heal(1000);
                int increased=ally.Health.CurrentHp;
                ally.StatModifiers.ReplaceSource(trait,maxHp);
                Expect(ally.Health.CurrentHp==increased,"최대 체력 수정자 교체 시 중간 감소 없음");
                ally.StatModifiers.RemoveSource(trait);
                Expect(ally.Health.MaxHp==definition.Hp && ally.Health.CurrentHp==definition.Hp,"최대 체력 버프 해제와 제한");

                var slow=ally.Effects.Apply(new EffectDefinition(3012,EffectKind.Slow,.5f,10f),skill);
                float began=Time.time;
                Expect(await ally.Move.MoveToCellAsync(start.GetNeighbor(0)),"감속 중 실제 이동");
                Expect(Time.time-began>=ally.MoveSeconds*1.8f,"감속이 이동 시간에 반영");
                ally.Effects.Remove(slow);
                var move=ally.Move.MoveToCellAsync(start);
                var stunned=ally.Effects.Apply(new EffectDefinition(3013,EffectKind.Stun,1f,10f),skill);
                Expect(!ally.Move.CanMove && !ally.Combat.CanAttack,"기절 중 이동·공격 차단");
                Expect(!await move && ally.CurrentCell==start.GetNeighbor(0),"진행 중 이동 취소와 셀 복귀");
                int hp=ally.Health.CurrentHp;
                Expect(ally.Combat.ReceiveAttack(1)>0 && ally.Health.CurrentHp<hp,"기절 대상도 피해를 받음");
                ally.Effects.Remove(stunned);

                enemy.Configure(definition,1,enemy.InstanceId,UnitTeam.Enemy,run.State.Data,flow,run.Grid,run.Bench,start);
                enemy.transform.position=run.Grid.CellToWorld(start);
                ally.Combat.ResetForRound(); hp=enemy.Health.CurrentHp;
                var attack=ally.Combat.AttackAsync(enemy.Combat,CancellationToken.None);
                Expect(ally.Combat.IsAttacking,"공격 선딜 시작");
                ally.Effects.Apply(new EffectDefinition(3014,EffectKind.Stun,1f,10f),skill);
                await attack;
                Expect(enemy.Health.CurrentHp==hp && ally.Combat.Mana==10,"기절로 선딜 취소하고 지급 마나 유지");
                ally.Effects.Clear();
                int baseAttack=ally.Stats.AttackDamage;
                ally.Effects.Apply(new EffectDefinition(3015,EffectKind.StatModifier,25f,.2f,stat:StatType.AttackDamage),skill);
                Expect(ally.Stats.AttackDamage==baseAttack+25,"실제 공격력 버프 적용");
                await UniTask.WaitUntil(()=>ally.Effects.Active.Count==0).Timeout(TimeSpan.FromSeconds(3));
                Expect(ally.Stats.AttackDamage==baseAttack,"전투 중 효과 자동 만료");
                ally.Effects.Apply(new EffectDefinition(3016,EffectKind.StatModifier,100f,-1f,stat:StatType.MaxHp),skill);
                ally.Effects.Apply(new EffectDefinition(3017,EffectKind.Shield,10f,10f),skill);
                ally.Health.TakeDamage(int.MaxValue);
                Expect(!ally.Health.IsAlive && ally.Effects.Active.Count==0 && ally.Stats.MaxHp==definition.Hp
                    && ally.Health.MaxHp==definition.Hp,"사망 시 효과와 최대 체력 수정자 정리");
                ally.RestorePreparation();
                ally.Effects.Apply(new EffectDefinition(3018,EffectKind.Stun,1f,10f),skill);
                Expect(flow.TryFinishCombat() && flow.TryBeginPreparation(),"결과와 다음 준비 전환");
                Expect(!ally.Effects.IsStunned && ally.Effects.Active.Count==0,"단계 전환 시 효과 정리");

                ally.Effects.Apply(new EffectDefinition(3019,EffectKind.StatModifier,10f,-1f),skill);
                var stale=ally.Effects.Apply(new EffectDefinition(3020,EffectKind.Shield,10f,-1f),skill);
                ally.StatModifiers.AddModifier(trait,StatType.AttackDamage,ModifierOperation.Flat,10f);
                pool.Return(ally); var reused=pool.Rent(reference);
                reused.Configure(definition,1,state.InstanceId,UnitTeam.Enemy,run.State.Data,flow,run.Grid,run.Bench,start);
                reused.gameObject.SetActive(true);
                Expect(reused==ally && reused.Effects.Active.Count==0 && reused.StatModifiers.State.ModifierCount==0,"풀 재사용의 이전 효과·수정자 정리");
                var fresh=reused.Effects.Apply(new EffectDefinition(3021,EffectKind.Shield,10f,1f),skill);
                Expect(!reused.Effects.Remove(stale) && reused.Effects.Shield==10,"이전 효과 Handle로 새 효과 제거 불가");
                reused.Effects.Remove(fresh); pool.Return(reused); pool.Return(enemy);
            }
            finally
            {
                await pool.DisposeAsync(); UnityEngine.Object.Destroy(root); UnityEngine.Object.Destroy(flowRoot);
            }
            Expect(assets.ActiveHandleCount==0,"검사 프리팹 핸들 해제");
            Debug.Log("UNIT_EFFECTS_PLAY_PASS stats-shield-heal-stun-slow-expire-death-phase-pool");
        }
        public static void BuildAndPlay()
        {
            RunDomain(); MilestoneChecks.RunAll(); CharacterPrefabSetup.BuildForVerification();
        }
    }
}
#endif
