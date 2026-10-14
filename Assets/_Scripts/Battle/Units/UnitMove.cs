using System;
using System.Threading;
using AutoBattler.Battle.Grid;
using AutoBattler.Battle.Flow;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace AutoBattler.Battle.Units
{
    /// <summary>전투 중 한 셀 이동과 사망·단계 전환 시 이동 취소 담당.</summary>
    [DisallowMultipleComponent, RequireComponent(typeof(Unit), typeof(UnitHealth))]
    public sealed class UnitMove : MonoBehaviour
    {
        public bool IsMoving { get; private set; }
        public event Action<Vector3> MoveStarted;
        public bool CanMove => isActiveAndEnabled && unit != null && unit.IsInCombat
            && health != null && health.isActiveAndEnabled && health.IsAlive
            && !unit.Effects.IsStunned && unit.Effects.SlowRate<1f
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
        public void Stop() => movementCancellation?.Cancel();

        /// <summary>
        /// 인접 셀까지 이동. 성공 시 전투 좌표만 갱신, 취소 시 현재 셀로 복귀.
        /// 경로 선택과 목적지 점유 검사는 이동 요청 전에 호출 측에서 처리.
        /// </summary>
        public async UniTask<bool> MoveToCellAsync(HexCoord destination, CancellationToken cancellationToken = default)
        {
            if (!CanMove || IsMoving || cancellationToken.IsCancellationRequested
                || !unit.GridLayout.IsInsideBoard(destination)
                || unit.CurrentCell.Value.DistanceTo(destination) != 1)
                return false;

            var operation = CancellationTokenSource.CreateLinkedTokenSource(
                cancellationToken, this.GetCancellationTokenOnDestroy());
            movementCancellation = operation;
            IsMoving = true;
            bool arrived = false;
            try
            {
                Vector3 target = unit.GridLayout.CellToWorld(destination);
                target.z = transform.position.z;
                Vector3 start = transform.position;
                // 첫 이동 프레임 전에 표현 모듈에 목적지 전달.
                MoveStarted?.Invoke(target);
                float elapsed = 0f;
                while (elapsed < unit.MoveSeconds)
                {
                    operation.Token.ThrowIfCancellationRequested();
                    if (!CanMove) return false;
                    elapsed += Time.deltaTime*unit.Stats.MoveSpeed*(1f-unit.Effects.SlowRate);
                    transform.position = Vector3.Lerp(start, target, Mathf.Clamp01(elapsed / unit.MoveSeconds));
                    await UniTask.Yield(PlayerLoopTiming.Update, operation.Token);
                }
                operation.Token.ThrowIfCancellationRequested();
                if (!CanMove) return false;
                transform.position = target;
                unit.SetCombatCell(destination);
                Physics2D.SyncTransforms();
                arrived = true;
                return true;
            }
            catch (OperationCanceledException) { return false; }
            finally
            {
                if (!arrived) SnapToCurrentCell();
                movementCancellation = null;
                IsMoving = false;
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
