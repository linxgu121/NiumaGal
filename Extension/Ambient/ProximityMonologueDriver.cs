using System.Collections;
using NiumaGal.Dialogue.Data;
using NiumaGal.Dialogue.RuntimeData;
using NiumaGal.Enum;
using NiumaGal.Presenter;
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
        public DialogueAsset MonologueAsset;

        [Header("表现层引用")]
        public DialoguePresenter Presenter;

        [Header("黑板引用（用于检测 LineState/VoiceState）")]
        public NiumaGalBlackboard Blackboard;

        [Header("播放设置")]
        [Tooltip("进入触发区域后延迟多久开始播放（秒）")]
        public float StartDelay = 0.3f;

        [Tooltip("句间停顿（秒），覆盖 Asset 默认值")]
        public float LineInterval = 0.5f;

        private int _currentIndex;
        private bool _isPlaying;
        private Coroutine _playCoroutine;

        private void Awake()
        {
            if (Presenter == null)
                Presenter = FindObjectOfType<DialoguePresenter>();
        }

         private void OnTriggerEnter(Collider other)
        {
            if (_isPlaying) return;
            if (!other.CompareTag("Player")) return;
            if (MonologueAsset == null || MonologueAsset.Sentences.Count == 0) return;

            _playCoroutine = StartCoroutine(RunMonologue());
        }

        private void OnTriggerExit(Collider other)
        {
            if (!other.CompareTag("Player")) return;
            if (_playCoroutine != null)
            {
                StopCoroutine(_playCoroutine);
                _playCoroutine = null;
            }
            EndMonologue();
        }

        private IEnumerator RunMonologue()
        {
            _isPlaying = true;
            _currentIndex = 0;

            yield return new WaitForSeconds(StartDelay);

            while (_currentIndex < MonologueAsset.Sentences.Count)
            {
                var line = MonologueAsset.Sentences[_currentIndex];
                Presenter.PlayAmbient(line, AmbientMode.ProximityMonologue, null);

                // 等待 Typewriter 完成
                yield return new WaitUntil(() => Blackboard.LineState == LineState.Completed);

                // 等待语音完成（如果有）
                yield return new WaitUntil(() => Blackboard.VoiceState != VoiceState.Playing);

                // 句间停顿
                yield return new WaitForSeconds(LineInterval);

                _currentIndex++;
            }

            EndMonologue();
        }

        /// <summary>
        /// 结束独白，重置状态
        /// </summary>
        private void EndMonologue()
        {
            _isPlaying = false;
            _currentIndex = 0;
            Presenter.CloseAmbient();
        }

        /// <summary>
        /// 确保在对象销毁时停止协程，避免潜在的错误
        /// </summary>
        private void OnDestroy()
        {
            if (_playCoroutine != null)
                StopCoroutine(_playCoroutine);
        }

    }
}
