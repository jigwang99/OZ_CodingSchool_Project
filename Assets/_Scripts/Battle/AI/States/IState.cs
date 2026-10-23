namespace AutoBattler.Battle.AI.States
{
    /// <summary>상태 진입·종료와 프레임별 실행 규약.</summary>
    public interface IState
    {
        void Enter();
        void Exit();
        void Update();
        void FixedUpdate();
    }
}
