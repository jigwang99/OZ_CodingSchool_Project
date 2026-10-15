using AutoBattler.Battle.Flow;
using AutoBattler.Battle.Grid;
using AutoBattler.Battle.Placement;
using AutoBattler.Data.Units;
using AutoBattler.Data.Snapshot;
using AutoBattler.Presentation.Units;
using UnityEngine;

namespace AutoBattler.Battle.Units
{
    public enum UnitTeam { Ally, Enemy }
    /// <summary>공통 데이터와 전투 문맥. 수치는 JSON 정의에서 주입.</summary>
    [DefaultExecutionOrder(-100)]
    [DisallowMultipleComponent, RequireComponent(typeof(UnitView),typeof(UnitHealth))]
    [RequireComponent(typeof(UnitStats),typeof(UnitEffects))]
    [RequireComponent(typeof(UnitMana),typeof(UnitSkill))]
    public sealed class Unit : MonoBehaviour
    {
        // 이전 프리팹의 테스트용 연결만 유지. 도전에서는 Configure로 덮어씀.
        [SerializeField] private UnitData unitData;
        [SerializeField] private UnitType unitType;
        [SerializeField] private BattleFlowController battleFlow;
        [SerializeField] private HexGridLayout gridLayout;
        [SerializeField] private BenchLayout benchLayout;
        public UnitStat Stats=>StatModifiers?.Current;
        public UnitStats StatModifiers { get; private set; }
        public UnitEffects Effects { get; private set; }
        public UnitMana Mana { get; private set; }
        public UnitSkill Skill { get; private set; }
        public int SpawnVersion { get; private set; }
        public UnitDefinition Definition { get; private set; }
        public UnitTeam Team { get; private set; }
        public int InstanceId { get; private set; }
        public int Star { get; private set; }
        public float MoveSeconds { get; private set; }=.3f;
        public float WindupSeconds { get; private set; }=.2f;
        public float ProjectileSpeed { get; private set; }=8f;
        public UnitView View { get; private set; }
        public UnitHealth Health { get; private set; }
        public UnitMove Move { get; private set; }
        public UnitCombat Combat { get; private set; }
        public HexGridLayout GridLayout=>gridLayout;
        public BattleFlowController BattleFlow=>battleFlow;
        public bool IsOnBoard=>Team==UnitTeam.Enemy || (View!=null && View.State!=null && View.State.Location.Kind==UnitLocationKind.Board);
        public bool IsInCombat=>isActiveAndEnabled && Stats!=null && IsOnBoard && battleFlow!=null
            && battleFlow.isActiveAndEnabled && battleFlow.CurrentPhase==BattlePhase.Combat;
        private HexCoord? combatCell, enemyCell;
        public HexCoord? CurrentCell=>combatCell ?? enemyCell ?? View?.State?.Cell;
        private void Awake()
        {
            View=GetComponent<UnitView>(); Health=GetComponent<UnitHealth>();
            Move=GetComponent<UnitMove>(); Combat=GetComponent<UnitCombat>();
            StatModifiers=GetComponent<UnitStats>(); Effects=GetComponent<UnitEffects>();
            Mana=GetComponent<UnitMana>(); Skill=GetComponent<UnitSkill>();
            Health.BindUnit(this);
            Skill.BindUnit(this);
            if(unitData!=null && unitData.TryGetStat(unitType,out UnitStat stats))
            { StatModifiers.Initialize(stats); Health.Initialize(Stats.MaxHp,Stats.Defense); }
        }
        public void Configure(UnitDefinition definition,int star,int instanceId,UnitTeam team,
            DataRepository data,BattleFlowController flow,HexGridLayout grid,BenchLayout bench,HexCoord? enemyPosition=null,int bossProfile=0)
        {
            Skill.ResetForRound(); SpawnVersion++;
            Effects.Clear();
            Definition=definition; Star=star; InstanceId=instanceId; Team=team;
            battleFlow=flow; gridLayout=grid; benchLayout=bench; enemyCell=enemyPosition; combatCell=null;
            MoveSeconds=data.Rule(EconomyRule.MoveSeconds); WindupSeconds=data.Rule(EconomyRule.WindupSeconds);
            ProjectileSpeed=data.Rule(EconomyRule.ProjectileSpeed);
            float multiplier=star==2?data.Rule(EconomyRule.StarMultiplier):1f;
            float bossHp=bossProfile==3?2.5f:bossProfile>0?2f:1f;
            var baseStats=new UnitStat(UnitType.Character2,Mathf.FloorToInt(definition.Hp*multiplier*bossHp),
                Mathf.FloorToInt(definition.Attack*multiplier*(bossProfile>0?1.2f:1f)),definition.Armor,
                1f,definition.Range,definition.Interval);
            StatModifiers.Initialize(baseStats,definition.CritChance,definition.CritMultiplier);
            Effects.BindBattleFlow(flow);
            Health.Initialize(Stats.MaxHp,Stats.Defense);
            Mana.Initialize(100,bossProfile>0?100:0);
            Skill.Initialize(data,definition.SkillId);
            Combat.ResetForRound();
            int layer=team==UnitTeam.Ally?LayerMask.NameToLayer("AllyUnit"):0;
            gameObject.layer=layer<0?0:layer;
            gameObject.name=$"{team}_{definition.Name}_{star}Star_{instanceId}";
        }
        public void BeginBattle() { Skill.ResetForRound(); Move.Stop(); Effects.Clear(); combatCell=enemyCell ?? View?.State?.Cell; Health.RestoreFullHealth(); Mana.ResetForRound(); Combat.ResetForRound(); }
        public void RestorePreparation()
        {
            Skill.ResetForRound(); Move.Stop(); Effects.Clear(); combatCell=null; Health.RestoreFullHealth(); Mana.ResetForRound(); Combat.ResetForRound();
            if(Team==UnitTeam.Ally && View!=null) View.SnapToLocation(gridLayout,benchLayout);
        }
        internal void SetCombatCell(HexCoord cell)=>combatCell=cell;
        private void OnDisable()
        {
            SpawnVersion++;
            if(Skill!=null) Skill.ResetForRound();
            if(Mana!=null) Mana.Clear();
            if(Move!=null) Move.Stop();
            if(Effects!=null) Effects.Clear();
            if(StatModifiers!=null) StatModifiers.ClearModifiers();
        }
#if UNITY_EDITOR
        public void ConfigureEditorDefaults() => unitData=null;
#endif
    }
}
