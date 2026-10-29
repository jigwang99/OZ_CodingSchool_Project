using System;
using System.Threading;
using AutoBattler.Managers;
using AutoBattler.Presentation.Battle;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace AutoBattler.Run
{
    /// <summary>로딩 화면, 도전 정리, 씬 전환과 전투 초기화 완료 순서 관리.</summary>
    public static class GameNavigation
    {
        public static bool IsTransitioning { get; private set; }
        public static StageDefinition SelectedStage { get; private set; }
        public static bool ResumeRequested { get; private set; }
        private static int arrivingScene=-1;
        private static RunController arrivingBattle;
        private static SceneLoadingView loading;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Reset()
        { IsTransitioning=false; SelectedStage=null; ResumeRequested=false; arrivingScene=-1; arrivingBattle=null; loading=null; }
        public static UniTask<bool> StartStageAsync(GameNavigationSettings settings,StageDefinition stage,RunController run=null)
        {
            if(settings==null || !settings.Contains(stage)) return UniTask.FromResult(false);
            return LoadAsync(stage.SceneBuildIndex,stage,run,false);
        }
        public static UniTask<bool> ResumeStageAsync(GameNavigationSettings settings)
        {
            SaveManager saves=SaveManager.instance;
            if(settings==null || !saves.HasSavedRun || saves.SavedStageId!=settings.DefaultStage.Id) return UniTask.FromResult(false);
            return LoadAsync(settings.DefaultStage.SceneBuildIndex,settings.DefaultStage,null,true);
        }
        public static UniTask<bool> RetryAsync(GameNavigationSettings settings,RunController run)
            =>StartStageAsync(settings,SelectedStage!=null?SelectedStage:settings!=null?settings.DefaultStage:null,run);
        public static UniTask<bool> ReturnToTitleAsync(GameNavigationSettings settings,RunController run)
            =>settings==null?UniTask.FromResult(false):LoadAsync(settings.TitleSceneBuildIndex,null,run,false);
        public static void RegisterBattleController(RunController controller)
        {
            if(IsTransitioning && controller.gameObject.scene.buildIndex==arrivingScene) arrivingBattle=controller;
        }
        public static void ReportBattleLoading(RunController controller,float progress,string message)
        {
            if(IsTransitioning && controller==arrivingBattle && loading!=null) loading.SetProgress(.35f+.65f*progress,message);
        }
        private static async UniTask<bool> LoadAsync(int buildIndex,StageDefinition stage,RunController run,bool resume)
        {
            if(IsTransitioning || buildIndex<0 || !Application.CanStreamedLevelBeLoaded(buildIndex)) return false;
            IsTransitioning=true; arrivingScene=stage!=null?buildIndex:-1; arrivingBattle=null;
            bool previousResume=ResumeRequested; StageDefinition previousStage=SelectedStage;
            bool arrived=false,completed=false;
            CancellationToken token=Application.exitCancellationToken;
            loading=GameManager.instance.LoadingScreen;
            try
            {
                Time.timeScale=1f;
                GameManager.instance.Settings.Close();
                loading.Show(stage!=null?$"스테이지 {stage.DisplayName}":"타이틀 화면");
                // 로딩 화면을 한 프레임 표시한 뒤 무거운 씬 작업 시작.
                await UniTask.NextFrame(cancellationToken:token);
                if(run!=null) { loading.SetProgress(0f,"이전 전투 정리 중..."); await run.ShutdownAsync(); }
                ResumeRequested=resume; SelectedStage=stage;
                AsyncOperation operation=SceneManager.LoadSceneAsync(buildIndex,LoadSceneMode.Single);
                if(operation==null) throw new InvalidOperationException("장면 로드 요청 실패.");
                float sceneWeight=stage!=null?.35f:1f;
                await operation.ToUniTask(progress:Cysharp.Threading.Tasks.Progress.Create<float>(value=>
                    loading.SetProgress(sceneWeight*value)),cancellationToken:token);
                arrived=true;
                if(stage!=null)
                {
                    if(arrivingBattle==null) throw new InvalidOperationException("전투 씬의 RunController 등록 필요.");
                    completed=await arrivingBattle.WaitForInitializationAsync(token);
                }
                else completed=true;
                return true;
            }
            catch
            {
                if(!arrived) { ResumeRequested=previousResume; SelectedStage=previousStage; }
                throw;
            }
            finally
            {
                try
                {
                    if(loading!=null)
                    {
                        if(token.IsCancellationRequested) loading.HideImmediately();
                        else await loading.HideAsync(completed,token);
                    }
                }
                finally { loading=null; arrivingBattle=null; arrivingScene=-1; IsTransitioning=false; }
            }
        }
    }
}
