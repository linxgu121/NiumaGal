using System;
using NiumaGal.Dialogue.Config;
using NiumaGal.Dialogue.Data;
using NiumaGal.Dialogue.RuntimeData;
using NiumaGal.Enum;
using NiumaGal.Extension.Ambient;
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

        [Header("环境叙事设置")]
        [Tooltip("环境气泡默认显示时长（秒）")]
        public float DefaultBubbleDuration = 3f;

        // 环境叙事状态
        private bool _isAmbientPlaying;
        private AmbientMode _currentAmbientMode;
        private float _ambientTimer;
        private Transform _ambientSourceTransform;

        /// <summary>
        /// 当前是否有环境叙事在播放
        /// </summary>
        public bool IsAmbientPlaying => _isAmbientPlaying;

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

            //环境叙事独立更新
            UpdateAmbient();
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

        #region 环境叙事

        public void PlayAmbient(DialogueSentence line, AmbientMode mode, Transform sourceTransform)
        {
            _isAmbientPlaying = true;
            _currentAmbientMode = mode;
            _ambientSourceTransform = sourceTransform;

            switch (mode)
            {
                case AmbientMode.Bubble:
                    // 头顶气泡：启动打字机但不阻塞，用独立事件驱动 UI
                    _typewriter?.Start(line.Text);
                    OnAmbientBubble?.Invoke(line.Speaker, line.Text, sourceTransform);
                    _ambientTimer = CalculateBubbleDuration(line.Text);
                    break;
                case AmbientMode.Subtitle:
                    // 屏幕旁白：直接显示完整文本，不逐字
                    OnAmbientSubtitle?.Invoke(line.Speaker, line.Text);
                    _ambientTimer = DefaultBubbleDuration;
                    break;
                case AmbientMode.ProximityMonologue:
                    // 近距离独白：复用逐字+语音，但自动推进
                    _typewriter?.Start(line.Text);
                    _voice?.Play(line.VoiceClip);
                    OnAmbientSubtitle?.Invoke(line.Speaker, line.Text);
                    break;
            }

            _voice?.Play(line.VoiceClip);
            OnRefreshUI?.Invoke(line.Speaker, line.Text);
        }

        /// <summary>
        /// 关闭当前环境叙事表现
        /// </summary>
        public void CloseAmbient()
        {
            if(!_isAmbientPlaying) return;

            _typewriter?.Skip();
            _voice?.Stop();
            _isAmbientPlaying = false;
            _ambientSourceTransform = null;

            OnAmbientClosed?.Invoke(_currentAmbientMode);
        }

        /// <summary>
        /// 环境叙事 Update 驱动（由 DialoguePresenter.Update 调用）
        /// </summary>
        private void UpdateAmbient()
        {
            if (!_isAmbientPlaying) return;

            // Bubble / Subtitle 的定时关闭
            if (_currentAmbientMode == AmbientMode.Bubble || _currentAmbientMode == AmbientMode.Subtitle)
            {
                _ambientTimer -= Time.deltaTime;
                if (_ambientTimer <= 0f)
                {
                    CloseAmbient();
                }
            }
        }

        private float CalculateBubbleDuration(string text)
        {
            if (string.IsNullOrEmpty(text)) return DefaultBubbleDuration;
            // 基础 2 秒 + 每字 0.15 秒
            return Mathf.Max(DefaultBubbleDuration, 2f + text.Length * 0.15f);
        }

        #endregion

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

        public Func<string> GetTypewriterDisplayText => () => _typewriter?.GetCurrentDisplayText();

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

        /// <summary>
        /// 头顶气泡事件（Speaker, Text, 世界空间目标）
        /// </summary>
        public event Action<string, string, Transform> OnAmbientBubble;

        /// <summary>
        /// 屏幕旁白字幕事件
        /// </summary>
        public event Action<string, string> OnAmbientSubtitle;

        /// <summary>
        /// 环境叙事关闭事件（参数为刚结束的模式）
        /// </summary>
        public event Action<AmbientMode> OnAmbientClosed;
    }
}
