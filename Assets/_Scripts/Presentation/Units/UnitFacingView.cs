using System;
using System.Collections.Generic;
using AutoBattler.Battle.Flow;
using AutoBattler.Battle.Units;
using UnityEngine;

namespace AutoBattler.Presentation.Units
{
    /// <summary>이동 목적지와 공격 대상에 맞춰 외형의 좌우 방향만 변경.</summary>
    [DisallowMultipleComponent, RequireComponent(typeof(Unit))]
    public sealed class UnitFacingView : MonoBehaviour
    {
        [Header("방향을 변경할 외형")]
        [SerializeField] private SpriteRenderer[] spriteRenderers=Array.Empty<SpriteRenderer>();
        [Tooltip("프리팹의 기본 외형이 오른쪽을 바라보는 경우 선택. PIXEM 기본 외형은 왼쪽.")]
        [SerializeField] private bool defaultFacesRight=false;

        private Unit unit;
        private BattleFlowController boundFlow;
        private bool[] originalFlipX;
        public bool IsFacingRight { get; private set; }
        public IReadOnlyList<SpriteRenderer> SpriteRenderers => Array.AsReadOnly(spriteRenderers);

        private void Awake()
        {
            unit=GetComponent<Unit>();
            if(spriteRenderers.Length==0)
                spriteRenderers=GetComponentsInChildren<SpriteRenderer>(true);
            originalFlipX=new bool[spriteRenderers.Length];
            for(int i=0;i<spriteRenderers.Length;i++)
                originalFlipX[i]=spriteRenderers[i]!=null && spriteRenderers[i].flipX;
        }
        private void OnEnable()
        {
            if(unit.Move!=null) unit.Move.MoveStarted+=FacePosition;
            if(unit.Combat!=null) unit.Combat.AttackStarted+=OnAttack;
            boundFlow=unit.BattleFlow;
            if(boundFlow!=null) boundFlow.PhaseChanged+=OnPhaseChanged;
            RestoreDefaultFacing();
        }
        public void FacePosition(Vector3 worldPosition)
        {
            if(!isActiveAndEnabled || !unit.IsInCombat || !unit.Health.IsAlive) return;
            float horizontal=worldPosition.x-transform.position.x;
            // 수직 이동이나 같은 X 위치의 대상은 마지막 좌우 방향 유지.
            if(Mathf.Abs(horizontal)<=0.0001f) return;
            ApplyFacing(horizontal>0f);
        }
        private void OnAttack(UnitCombat target)
        {
            if(target!=null) FacePosition(target.transform.position);
        }
        private void OnPhaseChanged(BattlePhase phase)
        {
            if(phase==BattlePhase.Preparation || phase==BattlePhase.Combat)
                RestoreDefaultFacing();
        }
        public void RestoreDefaultFacing() => ApplyFacing(unit.Team==UnitTeam.Ally);
        private void ApplyFacing(bool facesRight)
        {
            IsFacingRight=facesRight;
            bool reverse=facesRight!=defaultFacesRight;
            for(int i=0;i<spriteRenderers.Length;i++)
                if(spriteRenderers[i]!=null)
                    spriteRenderers[i].flipX=originalFlipX[i]^reverse;
        }
        private void OnDisable()
        {
            if(unit.Move!=null) unit.Move.MoveStarted-=FacePosition;
            if(unit.Combat!=null) unit.Combat.AttackStarted-=OnAttack;
            if(boundFlow!=null) boundFlow.PhaseChanged-=OnPhaseChanged;
            boundFlow=null;
            RestoreDefaultFacing();
        }
#if UNITY_EDITOR
        public void ConfigureRenderers(SpriteRenderer[] renderers) => spriteRenderers=renderers;
#endif
    }
}
