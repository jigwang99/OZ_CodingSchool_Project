using UnityEngine;

namespace AutoBattler.Utility
{
    // Domain Reload를 꺼도 플레이 진입마다 정적 참조와 종료 상태 초기화.
    internal static class SingletonSession
    {
        public static int Version { get; private set; }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Begin()=>Version++;
    }

    public abstract class Singleton<T> : MonoBehaviour where T : MonoBehaviour
    {
        private static T _instance;
        private static bool isQuitting;
        private static int session=-1;
        [SerializeField] protected bool isDontDestroy=false;

        public static T instance
        {
            get
            {
                RefreshSession();
                if(isQuitting) return _instance!=null ? _instance : null;
                if(_instance==null)
                {
                    _instance=FindFirstObjectByType<T>();
                    if(_instance==null)
                    {
                        var obj=new GameObject(typeof(T).Name);
                        _instance=obj.AddComponent<T>();
                    }
                }
                if(_instance is Singleton<T> singleton && singleton.isDontDestroy)
                    DontDestroyOnLoad(singleton.gameObject);
                return _instance;
            }
        }
        // 조회만 필요한 호출에서는 자동 생성 없이 현재 등록 여부 확인.
        public static bool TryGetExisting(out T value)
        { RefreshSession(); value=_instance; return value!=null; }
        private static void RefreshSession()
        {
            if(session==SingletonSession.Version) return;
            session=SingletonSession.Version; _instance=null; isQuitting=false;
        }
        protected virtual void Awake()
        {
            RefreshSession();
            if(_instance==null)
            {
                _instance=this as T;
                if(isDontDestroy) DontDestroyOnLoad(gameObject);
            }
            else if(_instance!=this) Destroy(gameObject);
        }
        protected virtual void OnDestroy() { if(_instance==this) _instance=null; }
        protected virtual void OnApplicationQuit()=>isQuitting=true;
    }
}
