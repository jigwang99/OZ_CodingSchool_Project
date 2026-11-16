using AutoBattler.Data.Snapshot;
using AutoBattler.Data.Augments;
using AutoBattler.Data.Stages;
using System.Collections.Generic;
using System.Linq;
using AutoBattler.Assets;
using AutoBattler.Battle.Flow;
using AutoBattler.Battle.Grid;
using AutoBattler.Battle.Placement;
using AutoBattler.Battle.Units;
using AutoBattler.Presentation.Battle;
using AutoBattler.Presentation.Units;
using AutoBattler.Run;
using AutoBattler.Utility;
using UnityEngine;

namespace AutoBattler.Managers
{
    /// <summary>한 도전의 유닛 로스터와 풀 대여·반환, 배치 표현 연결.</summary>
    [DisallowMultipleComponent]
    public sealed class UnitManager : Singleton<UnitManager>
    {
        private static readonly int[] FrontlineCompletedItems={6101,6105,6106}; // 수호자의 투구, 수호의 펜던트, 거인의 갑옷
        private static readonly int[] BacklineCompletedItems={6110,6114,6113}; // 질풍의 장화, 전투마도 반지, 파괴자의 건틀릿
        private readonly Dictionary<int,Unit> allies=new Dictionary<int,Unit>();
        private readonly List<Unit> enemies=new List<Unit>();
        private readonly List<Unit> summons=new List<Unit>();
        private UnitPrefabPool pool;
        private AssetCatalog catalog;
        private BattleFlowController flow;
        private UnitDragController drag;
        private HexGridLayout grid;
        private BenchLayout bench;
        private RunState state;
        private System.Func<int,Sprite> itemIcon;
        public void ConfigureItemIcons(System.Func<int,Sprite> provider)=>itemIcon=provider;

        public IReadOnlyCollection<Unit> Allies=>allies.Values;
        public IReadOnlyCollection<Unit> Enemies=>enemies.AsReadOnly();
        public IReadOnlyList<Unit> Summons=>summons.AsReadOnly();
        public Unit[] Combatants=>allies.Values.Where(unit=>unit.IsOnBoard)
            .Concat(enemies).Concat(summons).OrderBy(unit=>unit.InstanceId).ToArray();

        public void Initialize(UnitPrefabPool unitPool,AssetCatalog assetsCatalog,BattleFlowController phases,
            UnitDragController input,HexGridLayout layout,BenchLayout seats)
        {
            pool=unitPool; catalog=assetsCatalog; flow=phases; drag=input; grid=layout; bench=seats;
        }

        public void Bind(RunState runState)=>state=runState;

        public void SyncAllies(BattleVisualService visuals=null)
        {
            if(state==null) return;
            if(state.CanPrepare) drag.ClearSelection();
            else drag.CancelDrag();
            foreach(KeyValuePair<int, Unit> pair in allies.ToArray())
            {
                if(state.Units.Any(owned=>owned.Placement.InstanceId==pair.Key)) continue;
                pool.Return(pair.Value); allies.Remove(pair.Key);
            }
            foreach(OwnedUnit owned in state.Units)
            {
                int id=owned.Placement.InstanceId;
                bool initialize=false;
                if(!allies.TryGetValue(id,out Unit unit))
                {
                    unit=pool.Rent(catalog.VisualPrefab(state.Data.Units[owned.DefinitionId].VisualId));
                    ConfigureUnit(unit,owned.DefinitionId,owned.Star,id,UnitTeam.Ally); initialize=true;
                    drag.BindView(unit.View,owned.Placement);
                    unit.gameObject.SetActive(true); allies.Add(id,unit);
                }
                else if(unit.Star!=owned.Star)
                {
                    bool promoted=owned.Star>unit.Star;
                    ConfigureUnit(unit,owned.DefinitionId,owned.Star,id,UnitTeam.Ally);
                    initialize=true;
                    if(promoted) visuals?.PlayPromotion(unit);
                }
                unit.Equipment.Apply(state.Items.Equipped(id),state.Data,itemIcon);
                if(state.CanPrepare || initialize) AugmentApplicator.ApplyPersistent(unit,state.Augments.Selected);
            }
            Physics2D.SyncTransforms();
        }

        public void CreateEnemies()
        {
            if(state==null) return;
            ReturnBattleEnemies();
            int id=100000;
            float healthMultiplier=state.Data.EnemyHealthMultiplier(state.Round);
            float attackMultiplier=state.Data.EnemyAttackMultiplier(state.Round);
            foreach(EnemyDefinition definition in state.CurrentRound.Enemies)
            {
                HexCoord cell=HexCoord.FromOffset(definition.Col,definition.Row);
                Unit unit=pool.Rent(catalog.VisualPrefab(state.Data.Units[definition.UnitId].VisualId));
                ConfigureUnit(unit,definition.UnitId,definition.Star,id++,UnitTeam.Enemy,cell,definition.BossProfileId,healthMultiplier,attackMultiplier);
                UnitState displayState=new UnitState(unit.InstanceId) { Location=UnitLocation.OnBoard(cell) };
                unit.View.Bind(displayState,null);
                unit.transform.position=grid.CellToWorld(cell);
                unit.gameObject.SetActive(true); enemies.Add(unit);
            }
            ApplyEnemyEquipment();
        }

        private void ApplyEnemyEquipment()
        {
            int roundsPerSegment=state.Data.RoundsPerSegment;
            if(enemies.Count==0 || roundsPerSegment<=0) return;
            int segment=(state.Round-1)/roundsPerSegment+1;
            if(segment!=2 && segment!=3) return;

            Unit[] lineup=enemies.OrderBy(unit=>unit.CurrentCell.Value.Q).ToArray();
            int itemCount=1;
            if(segment==3)
            {
                bool lateStage=state.Data.IsEndless
                    || (state.Data.Stages.TryGetValue(state.Data.StageId,out StageRule stage) && stage.Chapter>=4);
                itemCount=lateStage?ItemInventoryState.EquipmentCapacity:2;
            }
            if(lineup.Length==1)
            {
                // 단독 편성은 한 기가 전방·후방 장비를 함께 사용.
                int frontlineCount=System.Math.Min(itemCount,ItemInventoryState.EquipmentCapacity-1);
                EquipEnemy(lineup[0],FrontlineCompletedItems.Take(frontlineCount).Append(BacklineCompletedItems[0]));
                return;
            }
            EquipEnemy(lineup[0],FrontlineCompletedItems.Take(itemCount));
            EquipEnemy(lineup[lineup.Length-1],BacklineCompletedItems.Take(itemCount));
        }

        private void EquipEnemy(Unit unit,IEnumerable<int> definitions)
        {
            // 적 장비에 음수 ID를 사용해 플레이어 보관함과 분리.
            OwnedItem[] equipment=definitions.Where(state.Data.Items.ContainsKey).Select((definitionId,slot)=>
                new OwnedItem(-(unit.InstanceId*ItemInventoryState.EquipmentCapacity+slot),definitionId,state.Data.StageId)
                {
                    OwnerUnitId=unit.InstanceId,
                    Slot=slot
                }).ToArray();
            if(equipment.Length==0) return;
            unit.Equipment.Apply(equipment,state.Data,itemIcon);
            unit.Health.RestoreFullHealth();
        }

        private void ConfigureUnit(Unit unit,int definitionId,int star,int id,UnitTeam team,HexCoord? cell=null,int boss=0,
            float healthMultiplier=1f,float attackMultiplier=1f)
        {
            unit.Configure(state.Data.Units[definitionId],star,id,team,state.Data,flow,grid,bench,cell,boss,
                healthMultiplier:healthMultiplier,attackMultiplier:attackMultiplier);
            unit.Identity.Apply(unit);
        }

        public void CreateBattleSummons()
        {
            ReturnBattleSummons();
            AugmentDefinition augment=state.Augments.Selected.FirstOrDefault(selected=>selected.Effect==AugmentEffect.TrainingDummy);
            if(augment==null) return;
            HashSet<HexCoord> occupied=new HashSet<HexCoord>(Combatants.Where(unit=>unit.CurrentCell.HasValue).Select(unit=>unit.CurrentCell.Value));
            // 적과 가까운 열부터 중앙에 가까운 빈 칸 선택. 로스터 보드에는 등록하지 않음.
            for(int col=HexGridLayout.AllyColumns-1;col>=0;col--)
                foreach(int row in Enumerable.Range(0,HexGridLayout.Rows).OrderBy(value=>Mathf.Abs(value-(HexGridLayout.Rows-1)*.5f)))
                {
                    HexCoord cell=HexCoord.FromOffset(col,row);
                    if(occupied.Contains(cell)) continue;
                    UnitDefinition definition=new UnitDefinition(new UnitRow {
                        unitId=9001,name="훈련용 허수아비",hp=Mathf.FloorToInt(augment.Value+augment.SecondaryValue*state.Level),
                        attack=0,armor=20,attackRange=1,attackInterval=1f,critMultiplier=1f,attackType="Melee"
                    });
                    Unit unit=pool.Rent(catalog.TrainingDummy);
                    unit.Configure(definition,1,200000,UnitTeam.Ally,state.Data,flow,grid,bench,cell,0,true,false);
                    unit.View.Bind(new UnitState(unit.InstanceId) {Location=UnitLocation.OnBoard(cell)},null);
                    unit.transform.position=grid.CellToWorld(cell);
                    unit.gameObject.SetActive(true); summons.Add(unit);
                    return;
                }
        }

        public void ReturnBattleSummons()
        {
            foreach(Unit unit in summons) pool?.Return(unit);
            summons.Clear();
        }

        public void ReturnBattleEnemies()
        {
            foreach(Unit unit in enemies) pool?.Return(unit);
            enemies.Clear();
        }

        public void RestorePreparation()
        {
            // 배치 복원 전에 결과 화면의 판매 드래그를 취소.
            drag?.CancelDrag();
            foreach(Unit unit in allies.Values) unit.RestorePreparation();
            Physics2D.SyncTransforms();
        }

        public void StopCombat()
        {
            foreach(Unit unit in allies.Values.Concat(enemies).Concat(summons))
            {
                unit.Move.Stop(); unit.Combat.ResetForRound();
            }
        }

        public void Clear()
        {
            foreach(Unit unit in allies.Values) pool?.Return(unit);
            ReturnBattleEnemies(); ReturnBattleSummons();
            allies.Clear(); state=null;
        }
    }
}
