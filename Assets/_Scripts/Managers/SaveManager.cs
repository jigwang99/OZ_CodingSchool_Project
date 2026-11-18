using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Security.Cryptography;
using AutoBattler.Data.Snapshot;
using AutoBattler.Run;
using AutoBattler.Utility;
using UnityEngine;

namespace AutoBattler.Managers
{
    /// <summary>진행도 저장·복원과 저장 상태 알림 담당.</summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-120)]
    public sealed class SaveManager : Singleton<SaveManager>
    {
        private const int RunSaveLimit=4*1024*1024;
        private const int CampaignSaveLimit=256*1024;
        private string directory;
        [SerializeField] private GameNavigationSettings progressionSettings;
        public string SavePath=>Path.Combine(directory,"Run.sav");
        public string CampaignPath=>Path.Combine(directory,"Campaign.sav");
        private CampaignProgress campaign=new CampaignProgress();
        private bool campaignReady=true;
        private string rejectedRun;
        public CampaignProgress Campaign=>campaign;
        public IReadOnlyList<int> UnlockedUnitIds=>Array.AsReadOnly(campaign.UnlockedUnitIds
            .Concat(progressionSettings!=null?progressionSettings.InitialUnlockedUnitIds:Array.Empty<int>()).Distinct().OrderBy(id=>id).ToArray());
        public bool IsUnitUnlocked(int unitId)=>campaign.IsUnitUnlocked(unitId)
            || progressionSettings!=null && progressionSettings.InitialUnlockedUnitIds.Contains(unitId);
        public void ConfigureProgression(GameNavigationSettings settings)=>progressionSettings=settings;
        public int[] AvailableUnitsForStage(StageDefinition stage)
            =>progressionSettings!=null && IsStageUnlocked(progressionSettings,stage)
                ?UnlockedUnitIds.Concat(stage.PlannedUnlockUnitIds).Distinct().OrderBy(id=>id).ToArray():Array.Empty<int>();
        public bool TryEnterStage(StageDefinition stage)
        {
            if(progressionSettings==null || !IsStageUnlocked(progressionSettings,stage))
            {LastError="아직 잠긴 스테이지입니다.";return false;}
            return PersistUnlocks(UnlockedUnitIds.Concat(stage.PlannedUnlockUnitIds),true);
        }
        public bool IsStageCleared(int stageId)=>campaign.Contains(stageId);
        public bool IsStageUnlocked(GameNavigationSettings settings,StageDefinition stage)
        {
            if(settings==null || !settings.Contains(stage)) return false;
            if(stage.IsEndless) return campaign.Contains(stage.EndlessPrerequisite.Id);
            StageDefinition previous=settings.PreviousStage(stage);
            return stage==settings.FirstStage || campaign.Contains(stage.Id)
                || previous!=null && campaign.Contains(previous.Id) || HasSavedRun && SavedStageId==stage.Id;
        }
        public bool HasSavedRun { get; private set; }
        public int SavedStageId { get; private set; }
        public int SavedRound { get; private set; }
        public string LastError { get; private set; }
        public event Action Changed;

        protected override void Awake()
        {
            isDontDestroy=true;
            base.Awake();
            if(!TryGetExisting(out SaveManager registered) || registered!=this) return;
            directory=Path.Combine(Application.persistentDataPath,"Saves");
            Refresh();
        }

        public void Refresh()
        {
            HasSavedRun=false; SavedStageId=0; SavedRound=0; LastError=null;
            campaignReady=true;
            try
            {
                if(EncryptedSaveFile.Exists(CampaignPath))
                {
                    string json=EncryptedSaveFile.ReadJson(CampaignPath,SaveFileKind.Campaign,CampaignSaveLimit,out bool legacy);
                    campaign=new CampaignProgress(JsonUtility.FromJson<CampaignSaveData>(json));
                    TryMigrateLegacy(CampaignPath,SaveFileKind.Campaign,CampaignSaveLimit,json,legacy);
                }
                else campaign=new CampaignProgress();
            }
            catch(Exception exception) when(IsFileError(exception))
            {campaignReady=false; LastError="스테이지 클리어 기록을 읽을 수 없습니다. 기존 기록은 유지됩니다.";}
            if(campaignReady && progressionSettings!=null)
            {
                // 이전 클리어 기록은 해당 스테이지에 이미 진입한 이력으로 간주.
                IEnumerable<int> prior=campaign.ClearedStageIds.SelectMany(id=>progressionSettings.TryGetStage(id,out StageDefinition stage)
                    ?stage.PlannedUnlockUnitIds:Array.Empty<int>());
                PersistUnlocks(progressionSettings.InitialUnlockedUnitIds.Concat(prior),false);
            }
            if(!EncryptedSaveFile.Exists(SavePath)) return;
            try
            {
                RunSaveData saved=ReadFile(out string json,out bool legacy); ReadHeader(saved);
                if(rejectedRun!=null && rejectedRun==JsonUtility.ToJson(saved))
                {HasSavedRun=false; LastError="이전 도전을 복원할 수 없습니다. 스테이지를 선택해 새 도전을 시작하세요."; return;}
                if(!HasSavedRun) TryMigrateLegacy(SavePath,SaveFileKind.Run,RunSaveLimit,json,legacy);
                if(saved.isOver && saved.wonRun && !saved.hasPendingRoundReward) RecordClear(saved.stageId);
            }
            catch(Exception exception) when(IsFileError(exception))
            { LastError="저장 파일을 읽을 수 없습니다. 새 도전을 시작할 수 있습니다."; }
        }

        public bool TrySave(RunState state,int stageId,bool augmentWindowOpen,bool replayCombat=false)
        {
            try
            {
                if(stageId<=0 || state==null || string.IsNullOrEmpty(state.Data.Version)) throw new ArgumentException("저장할 도전 정보 없음.");
                RunSaveData saved=state.Capture(stageId,augmentWindowOpen,replayCombat);
                string json=JsonUtility.ToJson(saved,true);
                EncryptedSaveFile.WriteJson(SavePath,SaveFileKind.Run,RunSaveLimit,json);
                ReadHeader(saved);
                rejectedRun=null;
                if(saved.isOver && saved.wonRun && !saved.hasPendingRoundReward && !RecordClear(stageId)) return false;
                LastError=null;
            }
            catch(Exception exception) when(IsFileError(exception))
            {
                LastError="진행도 저장에 실패했습니다. 기존 저장은 유지됩니다. 다시 시도해 주세요.";
                return false;
            }
            Changed?.Invoke(); return true;
        }

        private bool RecordClear(int stageId)
        {
            if(!campaignReady) {LastError="스테이지 클리어 기록을 저장할 수 없습니다. 기존 기록은 유지됩니다."; return false;}
            if(campaign.Contains(stageId)) return true;
            try
            {
                CampaignSaveData next=campaign.WithClear(stageId);
                if(progressionSettings!=null)
                {
                    IEnumerable<int> units=progressionSettings.InitialUnlockedUnitIds;
                    if(progressionSettings.TryGetStage(stageId,out StageDefinition stage)) units=units.Concat(stage.PlannedUnlockUnitIds);
                    next=new CampaignProgress(next).WithUnlocked(units);
                }
                CampaignProgress updated=new CampaignProgress(next);
                EncryptedSaveFile.WriteJson(CampaignPath,SaveFileKind.Campaign,CampaignSaveLimit,JsonUtility.ToJson(next,true));
                campaign=updated; return true;
            }
            catch(Exception exception) when(IsFileError(exception))
            {LastError="클리어 기록 저장에 실패했습니다. 타이틀에서 다시 시도해 주세요."; return false;}
        }
        private bool PersistUnlocks(IEnumerable<int> unitIds,bool notify)
        {
            if(!campaignReady) {LastError="유닛 해금 기록을 저장할 수 없습니다.";return false;}
            CampaignSaveData next=campaign.WithUnlocked(unitIds);
            if(next.unlockedUnitIds.SequenceEqual(campaign.UnlockedUnitIds)) return true;
            try
            {
                CampaignProgress updated=new CampaignProgress(next);
                EncryptedSaveFile.WriteJson(CampaignPath,SaveFileKind.Campaign,CampaignSaveLimit,JsonUtility.ToJson(next,true));
                campaign=updated;LastError=null;if(notify) Changed?.Invoke();return true;
            }
            catch(Exception exception) when(IsFileError(exception))
            {LastError="유닛 해금 기록 저장에 실패했습니다. 다시 시도해 주세요.";return false;}
        }
        private void TryMigrateLegacy(string path,SaveFileKind kind,int limit,string json,bool legacy)
        {
            try { EncryptedSaveFile.MigrateLegacy(path,kind,limit,json,legacy); }
            catch(Exception exception) when(IsFileError(exception))
            {
                Debug.LogWarning($"기존 저장 파일 이전 또는 정리 실패: {exception.Message}",this);
            }
        }

        public bool TryLoad(DataRepository data,int stageId,out RunState state,out bool augmentWindowOpen)
        {
            state=null; augmentWindowOpen=true;
            RunSaveData candidate=null;
            try
            {
                RunSaveData saved=ReadFile(out string json,out bool legacy); ReadHeader(saved);
                if(!HasSavedRun || saved.stageId!=stageId) throw new ArgumentException("이어할 수 없는 스테이지.");
                candidate=saved;
                int[] available=progressionSettings!=null && progressionSettings.TryGetStage(stageId,out StageDefinition stage)
                    ?AvailableUnitsForStage(stage):null;
                state=RunState.Restore(data,saved,available); augmentWindowOpen=saved.augmentWindowOpen;
                TryMigrateLegacy(SavePath,SaveFileKind.Run,RunSaveLimit,json,legacy);
                LastError=null; return true;
            }
            catch(Exception exception) when(IsFileError(exception))
            {
                // 복원에 실패한 저장 파일을 새 저장 전까지 보존.
                if(candidate!=null) {rejectedRun=JsonUtility.ToJson(candidate); HasSavedRun=false;}
                else if(exception is IOException || exception is CryptographicException) {HasSavedRun=false;}
                LastError="이어하기에 실패했습니다. 저장 파일이나 데이터 버전을 확인하고 새 도전을 시작해 주세요.";
                Debug.LogWarning($"이어하기 실패 원인: {exception.Message}\n저장 위치: {SavePath}",this);
                return false;
            }
        }

        private RunSaveData ReadFile(out string json,out bool legacy)
        {
            json=EncryptedSaveFile.ReadJson(SavePath,SaveFileKind.Run,RunSaveLimit,out legacy);
            return JsonUtility.FromJson<RunSaveData>(json);
        }
        private void ReadHeader(RunSaveData saved)
        {
            if(saved==null || saved.saveVersion!=1 || saved.gridLayoutVersion!=1 || saved.stageId<=0
                || saved.round<=0 || string.IsNullOrEmpty(saved.dataVersion)
                || saved.shop==null || saved.stock==null || saved.units==null || saved.augments==null
                || saved.phase!=RunSavePhase.Preparation && saved.phase!=RunSavePhase.Result)
                throw new ArgumentException("지원하지 않는 저장 형식.");
            SavedStageId=saved.stageId; SavedRound=saved.round; HasSavedRun=!saved.isOver || saved.hasPendingRoundReward;
        }
        private static bool IsFileError(Exception exception)=>exception is IOException || exception is UnauthorizedAccessException
            || exception is ArgumentException || exception is InvalidOperationException || exception is NotSupportedException
            || exception is CryptographicException;
#if UNITY_EDITOR
        // 검사 파일을 실제 사용자 저장과 분리하는 에디터 전용 진입점.
        public void ConfigureStorage(string folder) { directory=Path.GetFullPath(folder); rejectedRun=null; Refresh(); }
#endif
    }
}
