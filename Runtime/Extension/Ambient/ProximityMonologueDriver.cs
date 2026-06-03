using System.Collections;
using NiumaGal.Dialogue.Data;
using NiumaGal.Presenter;
using NiumaGal.Save;
using UnityEngine;

namespace NiumaGal.Extension.Ambient
{
    /// <summary>
    /// 近距离独白驱动器
    /// 挂载在触发区域（Collider + IsTrigger）上，玩家进入后自动播放多句独白
    /// 复用 DialogueAsset 作为剧本，自动推进，不阻塞玩家操作
    /// </summary>
    public class ProximityMonologueDriver : MonoBehaviour
    {
        [Header("剧本资产")]
        [Tooltip("近距离独白使用的 DialogueAsset。进入触发区后会按 Sentences 顺序自动播放。用于一次性触发存档时会读取 DialogueId，建议使用 monologue_ 前缀，避免和 ambient_ ID 冲突。")]
        public DialogueAsset MonologueAsset;

        [Tooltip("可选的环境叙事配置资产。绑定后会读取其中的 MonologueLineInterval，避免策划改了 Asset 间隔但运行时不生效。")]
        public AmbientAsset AmbientSettings;

        [Header("表现层引用")]
        [Tooltip("场景中的 DialoguePresenter。为空时会自动查找。")]
        public DialoguePresenter Presenter;

        [Header("进度记录（可选）")]
        [Tooltip("Gal 进度事实仓库。用于记录已触发的一次性近距离独白；为空时会自动查找。")]
        public NiumaGalProgressStore ProgressStore;

        [Header("播放设置")]
        [Tooltip("进入触发区域后延迟多久开始播放（秒）")]
        public float StartDelay = 0.3f;

        [Tooltip("句间停顿（秒）。未绑定 AmbientSettings 时使用；绑定 AmbientSettings 后作为兜底值。")]
        public float LineInterval = 0.5f;

        [Tooltip("是否只播放一次。适合玩家第一次靠近地点时触发的内心独白。")]
        public bool PlayOnce = true;

        [Tooltip("玩家离开触发区时是否立刻中断独白。关闭后会继续播完整段。")]
        public bool StopWhenExit = true;

        private int _currentIndex;
        private bool _isPlaying;
        private bool _hasPlayed;
        private Coroutine _playCoroutine;

        private void Awake()
        {
            if (Presenter == null)
                Presenter = FindSceneObject<DialoguePresenter>();

            if (ProgressStore == null)
                ProgressStore = NiumaGalProgressStore.Active ?? FindSceneObject<NiumaGalProgressStore>();
        }

         private void OnTriggerEnter(Collider other)
        {
            if (_isPlaying) return;
            if (PlayOnce && IsOneShotConsumed()) return;
            if (!other.CompareTag("Player")) return;
            if (MonologueAsset == null || MonologueAsset.Sentences.Count == 0) return;
            if (Presenter == null) return;

            _playCoroutine = StartCoroutine(RunMonologue());
        }

        private void OnTriggerExit(Collider other)
        {
            if (!other.CompareTag("Player")) return;
            if (!StopWhenExit) return;

            if (_playCoroutine != null)
            {
                StopCoroutine(_playCoroutine);
                _playCoroutine = null;
            }
            EndMonologue(false);
        }

        private IEnumerator RunMonologue()
        {
            _isPlaying = true;
            _currentIndex = 0;

            yield return new WaitForSeconds(StartDelay);

            if (Presenter == null)
            {
                _isPlaying = false;
                _currentIndex = 0;
                yield break;
            }

            while (_currentIndex < MonologueAsset.Sentences.Count)
            {
                if (Presenter == null)
                {
                    _isPlaying = false;
                    _currentIndex = 0;
                    yield break;
                }

                var line = MonologueAsset.Sentences[_currentIndex];
                if (!Presenter.PlayAmbient(line, AmbientMode.ProximityMonologue, null, 0f))
                {
                    _isPlaying = false;
                    _currentIndex = 0;
                    yield break;
                }

                // 等待环境叙事自己的逐字和语音完成，不依赖主对话黑板状态。
                yield return new WaitUntil(() => Presenter == null || Presenter.IsAmbientLineCompleted);

                if (Presenter == null)
                {
                    _isPlaying = false;
                    _currentIndex = 0;
                    yield break;
                }

                // 句间停顿
                yield return new WaitForSeconds(GetLineInterval());

                _currentIndex++;
            }

            EndMonologue(true);
        }

        /// <summary>
        /// 结束独白，重置状态
        /// </summary>
        private void EndMonologue(bool completed)
        {
            _isPlaying = false;
            _currentIndex = 0;
            _playCoroutine = null;
            if (completed)
            {
                _hasPlayed = true;
                MarkMonologueTriggeredIfNeeded();
            }

            if (Presenter != null)
                Presenter.CloseAmbient();
        }

        /// <summary>
        /// 判断一次性独白是否已经被本次运行或存档进度消费。
        /// </summary>
        private bool IsOneShotConsumed()
        {
            if (_hasPlayed)
                return true;

            var monologueId = ResolveMonologueId();
            return !string.IsNullOrWhiteSpace(monologueId)
                   && ProgressStore != null
                   && ProgressStore.IsAmbientTriggered(monologueId);
        }

        /// <summary>
        /// 独白完整播放后写入环境叙事进度事实。
        /// </summary>
        private void MarkMonologueTriggeredIfNeeded()
        {
            if (!PlayOnce)
                return;

            var monologueId = ResolveMonologueId();
            if (string.IsNullOrWhiteSpace(monologueId))
                return;

            if (ProgressStore == null)
                ProgressStore = NiumaGalProgressStore.Active ?? FindSceneObject<NiumaGalProgressStore>();

            ProgressStore?.MarkAmbientTriggered(monologueId);
        }

        private string ResolveMonologueId()
        {
            return MonologueAsset == null ? null : MonologueAsset.DialogueId;
        }

        private float GetLineInterval()
        {
            if (AmbientSettings != null)
                return Mathf.Max(0f, AmbientSettings.MonologueLineInterval);

            return Mathf.Max(0f, LineInterval);
        }

        private void StopRunningMonologue()
        {
            if (_playCoroutine != null)
            {
                StopCoroutine(_playCoroutine);
                _playCoroutine = null;
            }

            if (_isPlaying)
                EndMonologue(false);
        }

        private static T FindSceneObject<T>() where T : Object
        {
#if UNITY_2023_1_OR_NEWER
            return FindFirstObjectByType<T>();
#else
            return FindObjectOfType<T>();
#endif
        }

        /// <summary>
        /// 组件禁用时中断独白，避免 UI 字幕或气泡残留在场景中。
        /// </summary>
        private void OnDisable()
        {
            StopRunningMonologue();
        }

        /// <summary>
        /// 确保在对象销毁时停止协程，避免潜在的错误。
        /// </summary>
        private void OnDestroy()
        {
            StopRunningMonologue();
        }

    }
}
