using System;
using UnityEngine;

namespace AutoBattler.Battle.Flow
{
    [DisallowMultipleComponent]
    public sealed class BattleFlowController : MonoBehaviour
    {
        public BattlePhase CurrentPhase { get; private set; } = BattlePhase.Preparation;
        public bool IsAugmentWindowOpen { get; private set; }
        public bool CanInspectUnits => isActiveAndEnabled && (CanPlaceUnits || CurrentPhase == BattlePhase.Combat);
        public bool CanPlaceUnits => isActiveAndEnabled && (CurrentPhase==BattlePhase.Preparation
            || CurrentPhase==BattlePhase.AugmentSelection && !IsAugmentWindowOpen);
        public event Action<BattlePhase> PhaseChanged;
        private bool isChangingPhase;

        /// <summary>준비 단계의 배치를 확정하고 전투 시작.</summary>
        public bool TryStartCombat() => TryChangePhase(BattlePhase.Preparation, BattlePhase.Combat);

        /// <summary>전투 판정이 끝난 뒤 결과 단계로 전환.</summary>
        public bool TryFinishCombat() => TryChangePhase(BattlePhase.Combat, BattlePhase.Result);

        /// <summary>결과 처리가 끝난 뒤 다음 준비 단계로 전환.</summary>
        public bool TryBeginPreparation() => TryChangePhase(BattlePhase.Result, BattlePhase.Preparation);
        /// <summary>정산을 다시 실행하지 않고 저장된 결과 화면 복원.</summary>
        public bool TryRestoreResult()=>TryChangePhase(BattlePhase.Preparation,BattlePhase.Result);
        public bool TryBeginAugmentSelection()=>TryChangePhase(BattlePhase.Preparation,BattlePhase.AugmentSelection);
        public bool TryFinishAugmentSelection()=>TryChangePhase(BattlePhase.AugmentSelection,BattlePhase.Preparation);
        public bool SetAugmentWindowOpen(bool open)
        {
            if(!isActiveAndEnabled || isChangingPhase || CurrentPhase!=BattlePhase.AugmentSelection || IsAugmentWindowOpen==open) return false;
            isChangingPhase=true;
            try { IsAugmentWindowOpen=open; PhaseChanged?.Invoke(CurrentPhase); return true; }
            finally { isChangingPhase=false; }
        }

        private bool TryChangePhase(BattlePhase expected, BattlePhase next)
        {
            if (!isActiveAndEnabled || isChangingPhase || CurrentPhase != expected)
                return false;

            isChangingPhase = true;
            try
            {
                CurrentPhase = next;
                IsAugmentWindowOpen=next==BattlePhase.AugmentSelection;
                PhaseChanged?.Invoke(next);
                return true;
            }
            finally
            {
                isChangingPhase = false;
            }
        }
    }
}
