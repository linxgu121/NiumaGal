using System;
using NiumaGal.Dialogue.Config;
using NiumaGal.Dialogue.Data;
using NiumaGal.Dialogue.RuntimeData;
using NiumaGal.Enum;
using UnityEngine;

namespace NiumaGal.Presenter
{
    /// <summary>
    /// 对话表现层总控
    /// 协调 TypewriterSystem 与 VoiceSystem 的生命周期
    /// 挂载在场景空物体上，需配置 AudioSource
    /// </summary>
    public class DialoguePresenter : MonoBehaviour, IDialoguePresenter
    {
        [Header("音频组件")]
        public AudioSource VoiceAudioSource;

        private NiumaGalBlackboard _blackboard;
        private NiumaGalSO _config;
        private DialogueAsset _currentAsset;

        private TypewriterSystem _typewriter;
        private VoiceSystem _voice;

        public void Initialize(NiumaGalBlackboard blackboard, NiumaGalSO config)
        {
            _blackboard = blackboard;
            _config = config;

            _typewriter = new TypewriterSystem(blackboard, config?.Core);
            _voice = new VoiceSystem(blackboard, config?.Audio, VoiceAudioSource);

            _blackboard.OnLineStateChanged += OnLineStateChanged;
            _blackboard.OnVoiceStateChanged += OnVoiceStateChanged;
        }

         private void Update()
        {
            if (_blackboard == null) return;

            _typewriter?.Update(Time.deltaTime);
            _voice?.Update();
        }

        private void OnDestroy()
        {
            if (_blackboard != null)
            {
                _blackboard.OnLineStateChanged -= OnLineStateChanged;
                _blackboard.OnVoiceStateChanged -= OnVoiceStateChanged;
            }
        }

        #region IDialoguePresenter 实现
        public void PlaySentence(int sentenceIndex)
        {
            if (_currentAsset == null || sentenceIndex < 0 || sentenceIndex >= _currentAsset.Sentences.Count)
                return;

            var sentence = _currentAsset.Sentences[sentenceIndex];

            _typewriter?.Start(sentence.Text);
            _voice?.Play(sentence.VoiceClip);

            OnRefreshUI?.Invoke(sentence.Speaker, sentence.Text);
        }

        public void SkipTypewriter()
        {
            _typewriter?.Skip();
        }

        public void StopVoice()
        {
            _voice?.Stop();
        }

        public void CloseDialogue()
        {
            _typewriter?.Skip();
            _voice?.Stop();
            OnCloseUI?.Invoke();
        }

        public void HideUI()
        {
            OnHideUI?.Invoke();
        }

        #endregion

        /// <summary>
        /// 由外部设置当前对话资产
        /// </summary>
        public void SetDialogueAsset(DialogueAsset asset)
        {
            _currentAsset = asset;
        }

        // === 黑板事件响应 ===

        private void OnLineStateChanged(LineState state)
        {
            if (state == LineState.Completed)
                OnSentenceTextCompleted?.Invoke();
        }

        private void OnVoiceStateChanged(VoiceState state)
        {
            if (state == VoiceState.Completed && _blackboard.LineState == LineState.Completed)
                OnSentenceFullyCompleted?.Invoke();
        }

        // === 对外事件 ===
        /// <summary>
        /// 刷新 UI：说话人、完整文本（UI自行读取 GetCurrentDisplayText 做逐字效果）
        /// </summary>
        public event Action<string, string> OnRefreshUI;

        /// <summary>
        /// 逐字显示事件，供音效订阅
        /// </summary>
        public event Action<char> OnCharacterTyped
        {
            add { if (_typewriter != null) _typewriter.OnCharacterTyped += value; }
            remove { if (_typewriter != null) _typewriter.OnCharacterTyped -= value;}
        }

        /// <summary>
        /// 当前句文字显示完毕
        /// </summary>
        public event Action OnSentenceTextCompleted;

        /// <summary>
        /// 当前句文字和音频都显示完毕(Auto模式推进条件)
        /// </summary>
        public event Action OnSentenceFullyCompleted;

        /// <summary>
        /// 关闭对话 UI
        /// </summary>
        public event Action OnCloseUI;

        /// <summary>
        /// 隐藏 / 显示 UI（不重置状态，供菜单等系统调用）
        /// </summary>
        public event Action OnHideUI;
    }
}
