using System;
using System.Threading;
using AutoBattler.Assets;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.AddressableAssets;

namespace AutoBattler.Audio
{
    /// <summary>필요한 곡의 로드·반복 재생·페이드 전환과 핸들 해제 담당.</summary>
    public sealed class MusicPlayback:IDisposable
    {
        private readonly AssetService assets=new AssetService();
        private readonly AudioSource source;
        private readonly float fadeSeconds;
        private readonly Action<string> report;
        private AssetService.Lease<AudioClip> current;
        private object currentKey,requestedKey;
        private CancellationTokenSource request;
        private UniTaskCompletionSource<bool> completion;
        private bool disposed;
        private float volume,gain;
        public int ActiveHandleCount=>assets.ActiveHandleCount;
        public bool IsLoading=>request!=null;
        public AudioClip CurrentClip=>source!=null?source.clip:null;
        public string LastError {get;private set;}
        public int PlayCount {get;private set;}

        public MusicPlayback(AudioSource channel,float initialVolume,float fadeDuration,Action<string> error)
        {source=channel;volume=initialVolume;fadeSeconds=Mathf.Max(0,fadeDuration);report=error;ApplyVolume();}
        public void SetVolume(float value){volume=Mathf.Clamp01(value);ApplyVolume();}
        private void ApplyVolume(){if(source!=null)source.volume=volume*gain;}
        public UniTask<bool> PlayAsync(AssetReferenceT<AudioClip> clip)
        {
            if(disposed || source==null || clip==null || !clip.RuntimeKeyIsValid())return UniTask.FromResult(false);
            object key=clip.RuntimeKey;
            if(request!=null && Equals(requestedKey,key))return completion.Task;
            request?.Cancel();
            CancellationTokenSource next=new CancellationTokenSource();UniTaskCompletionSource<bool> done=new UniTaskCompletionSource<bool>();
            request=next;requestedKey=key;completion=done;
            ChangeAsync(clip,key,next,done).Forget();return done.Task;
        }
        private async UniTask ChangeAsync(AssetReferenceT<AudioClip> reference,object key,CancellationTokenSource operation,UniTaskCompletionSource<bool> done)
        {
            AssetService.Lease<AudioClip> pending=null;bool success=false;CancellationToken token=operation.Token;
            try
            {
                LastError=null;
                if(current==null || !Equals(currentKey,key))
                {
                    // 새 곡이 준비될 때까지 현재 곡 유지.
                    pending=await assets.AcquireAsync<AudioClip>(reference,token);token.ThrowIfCancellationRequested();
                    await FadeAsync(0f,token);token.ThrowIfCancellationRequested();
                    source.Stop();source.clip=null;current?.Dispose();current=pending;pending=null;currentKey=key;
                    source.clip=current.Asset;source.loop=true;source.Play();PlayCount++;
                }
                else if(!source.isPlaying){source.Play();PlayCount++;}
                await FadeAsync(1f,token);success=true;
            }
            catch(OperationCanceledException){}
            catch(Exception exception)
            {
                if(!disposed && ReferenceEquals(request,operation)){LastError=exception.Message;report?.Invoke(exception.Message);}
            }
            finally
            {
                pending?.Dispose();
                if(ReferenceEquals(request,operation)){request=null;requestedKey=null;}
                operation.Dispose();done.TrySetResult(success);
            }
        }
        private async UniTask FadeAsync(float target,CancellationToken token)
        {
            float start=gain;
            if(fadeSeconds>0 && !Mathf.Approximately(start,target))
            {
                float elapsed=0;
                while(elapsed<fadeSeconds)
                {
                    token.ThrowIfCancellationRequested();elapsed+=Time.unscaledDeltaTime;
                    gain=Mathf.Lerp(start,target,Mathf.Clamp01(elapsed/fadeSeconds));ApplyVolume();
                    await UniTask.Yield(PlayerLoopTiming.Update,token);
                }
            }
            token.ThrowIfCancellationRequested();gain=target;ApplyVolume();
        }
        public void Dispose()
        {
            if(disposed)return;disposed=true;request?.Cancel();
            if(source!=null){source.Stop();source.clip=null;}
            current?.Dispose();current=null;currentKey=null;assets.Dispose();
        }
    }
}
