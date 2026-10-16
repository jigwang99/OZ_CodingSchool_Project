using System;
using System.Threading;
using AutoBattler.Battle.Grid;
using AutoBattler.Battle.Flow;
using AutoBattler.Battle.Combat;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace AutoBattler.Battle.Units
{
    /// <summary>인접 셀 이동·시작 도약과 사망·단계 전환 시 이동 취소 담당.</summary>
    [DisallowMultipleComponent, RequireComponent(typeof(Unit), typeof(UnitHealth))]
    public sealed class UnitMove : MonoBehaviour
    {
        [Header("전투 시작 도약")]
        [SerializeField,Min(.01f)] private float jumpSeconds=.35f;
        [SerializeField,Min(0f)] private float jumpHeight=.8f;
        public bool IsMoving { get; private set; }
        public bool IsJumping { get; private set; }
        public event Action<Vector3> MoveStarted;
        public bool CanMove => isActiveAndEnabled && unit != null && unit.IsInCombat
            && health != null && health.isActiveAndEnabled && health.IsAlive
            && !unit.Effects.IsStunned && unit.Effects.SlowRate<1f
            && !unit.Skill.IsCasting
            && unit.Stats.MoveSpeed > 0f && unit.GridLayout != null && unit.CurrentCell.HasValue;

        private Unit unit;
        private UnitHealth health;
        private CancellationTokenSource movementCancellation;

        private void Awake()
        {
            unit = GetComponent<Unit>();
            health = GetComponent<UnitHealth>();
        }

        private void OnEnable()
        {
            if (health != null) health.Died += Stop;
            unit.Effects.Changed+=OnEffectsChanged;

        }

        private void OnDisable()
        {
            if (health != null) health.Died -= Stop;
            unit.Effects.Changed-=OnEffectsChanged;

            Stop();
        }

        private void OnEffectsChanged()
        { if(unit.Effects.IsStunned || unit.Effects.SlowRate>=1f) Stop(); }
        public void Stop()
        {
            var operation=movementCancellation;
            if(operation==null) return;
            movementCancellation=null; IsMoving=false; IsJumping=false;
            operation.Cancel(); SnapToCurrentCell();
        }

        /// <summary>
        /// 인접 셀까지 이동. 성공 시 전투 좌표만 갱신, 취소 시 현재 셀로 복귀.
        /// 경로 선택과 목적지 점유 검사는 이동 요청 전에 호출 측에서 처리.
        /// </summary>
        public UniTask<bool> MoveToCellAsync(HexCoord destination, CancellationToken cancellationToken = default)
            =>MoveAsync(destination,false,cancellationToken);

        /// <summary>예약한 비인접 셀까지 포물선 도약. 목적지 점유는 호출 측에서 관리.</summary>
        public UniTask<bool> JumpToCellAsync(HexCoord destination,CancellationToken cancellationToken=default)
            =>MoveAsync(destination,true,cancellationToken);

        private async UniTask<bool> MoveAsync(HexCoord destination,bool jump,CancellationToken cancellationToken)
        {
            if (!CanMove || IsMoving || cancellationToken.IsCancellationRequested
                || !unit.GridLayout.IsInsideBoard(destination)
                || (jump ? destination==unit.CurrentCell.Value || unit.Combat.IsAttacking
                    : unit.CurrentCell.Value.DistanceTo(destination)!=1))
                return false;

            var operation = CancellationTokenSource.CreateLinkedTokenSource(
                cancellationToken, this.GetCancellationTokenOnDestroy());
            movementCancellation = operation;
            IsMoving = true;
            IsJumping=jump;
            var owner=new CombatUnitReference(unit);
            bool arrived = false;
            try
            {
                Vector3 target = unit.GridLayout.CellToWorld(destination);
                target.z = transform.position.z;
                Vector3 start = transform.position;
                // 첫 이동 프레임 전에 표현 모듈에 목적지 전달.
                MoveStarted?.Invoke(target);
                float elapsed = 0f;
                float seconds=jump?Mathf.Max(.01f,jumpSeconds):unit.MoveSeconds;
                while (elapsed < seconds)
                {
                    operation.Token.ThrowIfCancellationRequested();
                    if (!owner.IsAlive || !ReferenceEquals(movementCancellation,operation) || !CanMove) return false;
                    elapsed += jump?Time.deltaTime:Time.deltaTime*unit.Stats.MoveSpeed*(1f-unit.Effects.SlowRate);
                    float progress=Mathf.Clamp01(elapsed/seconds);
                    Vector3 position=Vector3.Lerp(start,target,progress);
                    if(jump) position.y+=Mathf.Sin(progress*Mathf.PI)*jumpHeight*unit.GridLayout.CellSize;
                    transform.position=position;
                    await UniTask.Yield(PlayerLoopTiming.Update, operation.Token);
                }
                operation.Token.ThrowIfCancellationRequested();
                if (!owner.IsAlive || !ReferenceEquals(movementCancellation,operation) || !CanMove) return false;
                transform.position = target;
                unit.SetCombatCell(destination);
                Physics2D.SyncTransforms();
                arrived = true;
                return true;
            }
            catch (OperationCanceledException) { return false; }
            finally
            {
                // 이전 풀 사용의 finally가 새 이동의 위치·상태를 변경하지 않도록 소유권 확인.
                if(ReferenceEquals(movementCancellation,operation))
                {
                    if(!arrived && owner.IsCurrent) SnapToCurrentCell();
                    movementCancellation=null; IsMoving=false; IsJumping=false;
                }
                operation.Dispose();
            }
        }

        private void SnapToCurrentCell()
        {
            if (this == null || unit == null || unit.GridLayout == null || !unit.CurrentCell.HasValue)
                return;
            Vector3 position = unit.GridLayout.CellToWorld(unit.CurrentCell.Value);
            position.z = transform.position.z;
            transform.position = position;
            Physics2D.SyncTransforms();
        }
    }
}
