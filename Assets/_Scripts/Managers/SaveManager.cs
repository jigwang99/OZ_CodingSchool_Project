using System;
using System.IO;
using System.Text;
using AutoBattler.Data.Snapshot;
using AutoBattler.Run;
using AutoBattler.Utility;
using UnityEngine;

namespace AutoBattler.Managers
{
    /// <summary>JSON 진행도 파일의 저장·읽기·교체와 저장 상태 알림 담당.</summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-120)]
    public sealed class SaveManager : Singleton<SaveManager>
    {
        private string directory;
        public string SavePath=>Path.Combine(directory,"Run.json");
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
            if(!File.Exists(SavePath)) return;
            try { ReadHeader(ReadFile()); }
            catch(Exception exception) when(IsFileError(exception))
            { LastError="저장 파일을 읽을 수 없습니다. 새 도전을 시작할 수 있습니다."; }
        }

        public bool TrySave(RunState state,int stageId,bool augmentWindowOpen)
        {
            string temporary=SavePath+".tmp";
            try
            {
                if(stageId<=0 || state==null || string.IsNullOrEmpty(state.Data.Version)) throw new ArgumentException("저장할 도전 정보 없음.");
                var saved=state.Capture(stageId,augmentWindowOpen);
                string json=JsonUtility.ToJson(saved,true);
                Directory.CreateDirectory(directory);
                // 같은 폴더에 완성된 파일을 쓴 뒤 교체하여 기존 정상 파일 보호.
                using(var stream=new FileStream(temporary,FileMode.Create,FileAccess.Write,FileShare.None))
                {
                    byte[] bytes=new UTF8Encoding(false).GetBytes(json);
                    stream.Write(bytes,0,bytes.Length); stream.Flush(true);
                }
                if(File.Exists(SavePath)) File.Replace(temporary,SavePath,SavePath+".bak");
                else File.Move(temporary,SavePath);
                ReadHeader(saved); LastError=null;
            }
            catch(Exception exception) when(IsFileError(exception))
            {
                LastError="진행도 저장에 실패했습니다. 기존 저장은 유지됩니다. 다시 시도해 주세요.";
                return false;
            }
            Changed?.Invoke(); return true;
        }

        public bool TryLoad(DataRepository data,int stageId,out RunState state,out bool augmentWindowOpen)
        {
            state=null; augmentWindowOpen=true;
            try
            {
                var saved=ReadFile(); ReadHeader(saved);
                if(!HasSavedRun || saved.stageId!=stageId) throw new ArgumentException("이어할 수 없는 스테이지.");
                state=RunState.Restore(data,saved); augmentWindowOpen=saved.augmentWindowOpen;
                LastError=null; return true;
            }
            catch(Exception exception) when(IsFileError(exception))
            {
                LastError="이어하기에 실패했습니다. 저장 파일이나 데이터 버전을 확인하고 새 도전을 시작해 주세요.";
                Debug.LogWarning($"이어하기 실패 원인: {exception.Message}\n저장 위치: {SavePath}",this);
                return false;
            }
        }

        private RunSaveData ReadFile()
        {
            var file=new FileInfo(SavePath);
            if(!file.Exists || file.Length==0 || file.Length>4*1024*1024) throw new IOException("저장 파일 크기 오류.");
            return JsonUtility.FromJson<RunSaveData>(File.ReadAllText(SavePath,Encoding.UTF8));
        }
        private void ReadHeader(RunSaveData saved)
        {
            if(saved==null || saved.saveVersion!=1 || saved.gridLayoutVersion!=1 || saved.stageId<=0
                || saved.round<=0 || string.IsNullOrEmpty(saved.dataVersion)
                || saved.shop==null || saved.stock==null || saved.units==null || saved.augments==null
                || saved.phase!=RunSavePhase.Preparation && saved.phase!=RunSavePhase.Result)
                throw new ArgumentException("지원하지 않는 저장 형식.");
            SavedStageId=saved.stageId; SavedRound=saved.round; HasSavedRun=!saved.isOver;
        }
        private static bool IsFileError(Exception exception)=>exception is IOException || exception is UnauthorizedAccessException
            || exception is ArgumentException || exception is InvalidOperationException || exception is NotSupportedException;
#if UNITY_EDITOR
        // 검사 파일을 실제 사용자 저장과 분리하는 에디터 전용 진입점.
        public void ConfigureStorage(string folder) { directory=Path.GetFullPath(folder); Refresh(); }
#endif
    }
}
