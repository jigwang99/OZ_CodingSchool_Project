using System;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace AutoBattler.Run
{
    /// <summary>중복 전환 차단과 도전 정리 후 장면 이동. 영속 게임 오브젝트 없이 선택만 유지.</summary>
    public static class GameNavigation
    {
        public static bool IsTransitioning { get; private set; }
        public static StageDefinition SelectedStage { get; private set; }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Reset() { IsTransitioning=false; SelectedStage=null; }
        public static UniTask<bool> StartStageAsync(GameNavigationSettings settings,StageDefinition stage,RunController run=null)
        {
            if(settings==null || !settings.Contains(stage)) return UniTask.FromResult(false);
            return LoadAsync(stage.SceneBuildIndex,stage,run);
        }
        public static UniTask<bool> RetryAsync(GameNavigationSettings settings,RunController run)
            =>StartStageAsync(settings,SelectedStage!=null ? SelectedStage : settings!=null ? settings.DefaultStage : null,run);
        public static UniTask<bool> ReturnToTitleAsync(GameNavigationSettings settings,RunController run)
            =>settings==null ? UniTask.FromResult(false) : LoadAsync(settings.TitleSceneBuildIndex,null,run);
        private static async UniTask<bool> LoadAsync(int buildIndex,StageDefinition stage,RunController run)
        {
            if(IsTransitioning || buildIndex<0 || !Application.CanStreamedLevelBeLoaded(buildIndex)) return false;
            IsTransitioning=true;
            try
            {
                Time.timeScale=1f;
                if(run!=null) await run.ShutdownAsync();
                var operation=SceneManager.LoadSceneAsync(buildIndex,LoadSceneMode.Single);
                if(operation==null) throw new InvalidOperationException("장면 로드 요청 실패.");
                SelectedStage=stage;
                // 출발 장면이 사라져도 전환 완료와 잠금 해제까지 유지.
                await operation.ToUniTask();
                return true;
            }
            finally { IsTransitioning=false; }
        }
    }
}
