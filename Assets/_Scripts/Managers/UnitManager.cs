using AutoBattler.Data.Snapshot;
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
        private readonly Dictionary<int,Unit> allies=new Dictionary<int,Unit>();
        private readonly List<Unit> enemies=new List<Unit>();
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
        public Unit[] Combatants=>allies.Values.Where(unit=>unit.IsOnBoard)
            .Concat(enemies).OrderBy(unit=>unit.InstanceId).ToArray();

        public void Initialize(UnitPrefabPool unitPool,AssetCatalog assetsCatalog,BattleFlowController phases,
            UnitDragController input,HexGridLayout layout,BenchLayout seats)
        {
            pool=unitPool; catalog=assetsCatalog; flow=phases; drag=input; grid=layout; bench=seats;
        }

        public void Bind(RunState runState)=>state=runState;

        public void SyncAllies()
        {
            if(state==null) return;
            drag.ClearSelection();
            foreach(KeyValuePair<int, Unit> pair in allies.ToArray())
            {
                if(state.Units.Any(owned=>owned.Placement.InstanceId==pair.Key)) continue;
                pool.Return(pair.Value); allies.Remove(pair.Key);
            }
            foreach(OwnedUnit owned in state.Units)
            {
                int id=owned.Placement.InstanceId;
                if(!allies.TryGetValue(id,out Unit unit))
                {
                    unit=pool.Rent(catalog.VisualPrefab(state.Data.Units[owned.DefinitionId].VisualId));
                    ConfigureUnit(unit,owned.DefinitionId,owned.Star,id,UnitTeam.Ally);
                    drag.BindView(unit.View,owned.Placement);
                    unit.gameObject.SetActive(true); allies.Add(id,unit);
                }
                else if(unit.Star!=owned.Star)
                    ConfigureUnit(unit,owned.DefinitionId,owned.Star,id,UnitTeam.Ally);
                unit.Equipment.Apply(state.Items.Equipped(id),state.Data,itemIcon);
                AugmentApplicator.ApplyPersistent(unit,state.Augments.Selected);
            }
            Physics2D.SyncTransforms();
        }

        public void CreateEnemies()
        {
            if(state==null) return;
            foreach(Unit unit in enemies) pool.Return(unit);
            enemies.Clear();
            int id=100000;
            foreach(EnemyDefinition definition in state.Data.Rounds[state.Round].Enemies)
            {
                HexCoord cell=HexCoord.FromOffset(definition.Col,definition.Row);
                Unit unit=pool.Rent(catalog.VisualPrefab(state.Data.Units[definition.UnitId].VisualId));
                ConfigureUnit(unit,definition.UnitId,definition.Star,id++,UnitTeam.Enemy,cell,definition.BossProfileId);
                UnitState displayState=new UnitState(unit.InstanceId) { Location=UnitLocation.OnBoard(cell) };
                unit.View.Bind(displayState,null);
                unit.transform.position=grid.CellToWorld(cell);
                unit.gameObject.SetActive(true); enemies.Add(unit);
            }
        }

        private void ConfigureUnit(Unit unit,int definitionId,int star,int id,UnitTeam team,HexCoord? cell=null,int boss=0)
        {
            unit.Configure(state.Data.Units[definitionId],star,id,team,state.Data,flow,grid,bench,cell,boss);
            unit.Identity.Apply(unit);
        }

        public void RestorePreparation()
        {
            foreach(Unit unit in allies.Values) unit.RestorePreparation();
        }

        public void StopCombat()
        {
            foreach(Unit unit in allies.Values.Concat(enemies))
            {
                unit.Move.Stop(); unit.Combat.ResetForRound();
            }
        }

        public void Clear()
        {
            foreach(Unit unit in allies.Values) pool?.Return(unit);
            foreach(Unit unit in enemies) pool?.Return(unit);
            allies.Clear(); enemies.Clear(); state=null;
        }
    }
}
