namespace AutoBattler.Battle.AI.States
{
    /// <summary>현재 상태의 수명과 실행 관리. null 전환으로 상태 종료.</summary>
    public sealed class StateMachine
    {
        public IState CurrentState { get; private set; }

        public void ChangeState(IState state)
        {
            // 반복 판단에서 같은 상태의 진입·종료 중복 방지.
            if (ReferenceEquals(CurrentState, state))
                return;

            CurrentState?.Exit();
            CurrentState = state;
            CurrentState?.Enter();
        }

        public void Update() => CurrentState?.Update();

        public void FixedUpdate() => CurrentState?.FixedUpdate();
    }
}
