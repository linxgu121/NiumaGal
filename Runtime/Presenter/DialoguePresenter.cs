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

        [Tooltip("环境叙事专用语音源。建议单独绑定，避免环境语音打断正式对话语音；为空时会复用 VoiceAudioSource。")]
        public AudioSource AmbientAudioSource;

        [Tooltip("正式对话播放中是否允许环境叙事继续触发。关闭后可避免 NPC 气泡抢占剧情表现。")]
        public bool AllowAmbientDuringDialogue = false;

        [Tooltip("环境叙事逐字显示间隔（秒）。小于等于 0 时直接显示完整文本。")]
        public float AmbientTypewriterInterval = 0.04f;

        [Tooltip("气泡和近距离独白是否使用逐字显示。旁白字幕默认直接显示完整文本。")]
        public bool AmbientUseTypewriter = true;

        // 环境叙事状态
        private bool _isAmbientPlaying;
        private AmbientMode _currentAmbientMode;
        private float _ambientTimer;
        private Transform _ambientSourceTransform;
        private string _ambientSpeaker = string.Empty;
        private string _ambientFullText = string.Empty;
        private string _ambientDisplayText = string.Empty;
        private int _ambientDisplayLength;
        private float _ambientTypewriterTimer;
        private bool _ambientTextCompleted;
        private bool _ambientVoiceCompleted = true;
        private bool _ambientLineCompletedNotified;
        private AudioSource _activeAmbientAudioSource;

        /// <summary>
        /// 当前是否有环境叙事在播放
        /// </summary>
        public bool IsAmbientPlaying => _isAmbientPlaying;

        /// <summary>
        /// 当前环境叙事单句是否已经完成文字和语音。
        /// 近距离独白驱动器用它判断何时进入下一句。
        /// </summary>
        public bool IsAmbientLineCompleted => !_isAmbientPlaying || (_ambientTextCompleted && _ambientVoiceCompleted);

        public void Initialize(NiumaGalBlackboard blackboard, NiumaGalSO config)
        {
            UnbindBlackboardEvents();

            _blackboard = blackboard;
            _config = config;

            _typewriter = new TypewriterSystem(blackboard, config?.Core);
            _voice = new VoiceSystem(blackboard, config?.Audio, VoiceAudioSource);

            BindBlackboardEvents();
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
            UnbindBlackboardEvents();
        }

        #region IDialoguePresenter 实现
        public void PlaySentence(int sentenceIndex)
        {
            if (_currentAsset == null || sentenceIndex < 0 || sentenceIndex >= _currentAsset.Sentences.Count)
                return;

            var sentence = _currentAsset.Sentences[sentenceIndex];

            // 先设置语音状态，再启动打字机；空文本句会立刻完成，需避免早于语音状态触发“完全完成”。
            _voice?.Play(sentence.VoiceClip);
            _typewriter?.Start(sentence.Text);

            OnRefreshUI?.Invoke(sentence.Speaker, sentence.Text);
        }

        private void BindBlackboardEvents()
        {
            if (_blackboard == null)
                return;

            _blackboard.OnLineStateChanged += OnLineStateChanged;
            _blackboard.OnVoiceStateChanged += OnVoiceStateChanged;
        }

        private void UnbindBlackboardEvents()
        {
            if (_blackboard == null)
                return;

            _blackboard.OnLineStateChanged -= OnLineStateChanged;
            _blackboard.OnVoiceStateChanged -= OnVoiceStateChanged;
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
            PlayAmbient(line, mode, sourceTransform, 0f);
        }

        /// <summary>
        /// 播放一条环境叙事。
        /// 环境叙事不进入主对话状态机，也不触发正式对话 UI，只通过 Ambient 事件交给外部表现层。
        /// </summary>
        public bool PlayAmbient(DialogueSentence line, AmbientMode mode, Transform sourceTransform, float displayDuration)
        {
            if (line == null)
                return false;

            if (!AllowAmbientDuringDialogue &&
                _blackboard != null &&
                _blackboard.InteractionState != InteractionState.Idle)
            {
                return false;
            }

            StopAmbientVoice();

            _isAmbientPlaying = true;
            _currentAmbientMode = mode;
            _ambientSourceTransform = sourceTransform;
            _ambientSpeaker = line.Speaker ?? string.Empty;
            _ambientFullText = line.Text ?? string.Empty;
            _ambientDisplayLength = ShouldUseAmbientTypewriter(mode) ? 0 : _ambientFullText.Length;
            _ambientDisplayText = _ambientFullText.Substring(0, _ambientDisplayLength);
            _ambientTypewriterTimer = 0f;
            _ambientTextCompleted = _ambientDisplayLength >= _ambientFullText.Length;
            _ambientVoiceCompleted = true;
            _ambientLineCompletedNotified = false;
            _ambientTimer = displayDuration > 0f ? displayDuration : CalculateBubbleDuration(_ambientFullText);

            PlayAmbientVoice(line.VoiceClip);
            RaiseAmbientStarted();
            RaiseAmbientUpdated();

            if (_ambientTextCompleted && _ambientVoiceCompleted)
                NotifyAmbientLineCompletedIfNeeded();

            return true;
        }

        /// <summary>
        /// 关闭当前环境叙事表现
        /// </summary>
        public void CloseAmbient()
        {
            if(!_isAmbientPlaying) return;

            StopAmbientVoice();
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

            UpdateAmbientTypewriter(Time.deltaTime);
            UpdateAmbientVoice();
            NotifyAmbientLineCompletedIfNeeded();

            // Bubble / Subtitle 的定时关闭
            if (_currentAmbientMode == AmbientMode.Bubble || _currentAmbientMode == AmbientMode.Subtitle)
            {
                if (!_ambientTextCompleted || !_ambientVoiceCompleted)
                    return;

                _ambientTimer -= Time.deltaTime;
                if (_ambientTimer <= 0f)
                {
                    CloseAmbient();
                }
            }
        }

        private void UpdateAmbientTypewriter(float deltaTime)
        {
            if (_ambientTextCompleted)
                return;

            if (string.IsNullOrEmpty(_ambientFullText))
            {
                CompleteAmbientText();
                return;
            }

            if (AmbientTypewriterInterval <= 0f)
            {
                CompleteAmbientText();
                return;
            }

            _ambientTypewriterTimer += deltaTime;
            bool changed = false;

            while (_ambientTypewriterTimer >= AmbientTypewriterInterval &&
                   _ambientDisplayLength < _ambientFullText.Length)
            {
                _ambientTypewriterTimer -= AmbientTypewriterInterval;
                _ambientDisplayLength++;
                changed = true;
            }

            if (_ambientDisplayLength >= _ambientFullText.Length)
                _ambientTextCompleted = true;

            if (changed)
            {
                _ambientDisplayText = _ambientFullText.Substring(0, _ambientDisplayLength);
                RaiseAmbientUpdated();
            }
        }

        private void CompleteAmbientText()
        {
            _ambientDisplayLength = _ambientFullText.Length;
            _ambientDisplayText = _ambientFullText;
            _ambientTextCompleted = true;
            RaiseAmbientUpdated();
        }

        private void PlayAmbientVoice(AudioClip clip)
        {
            _activeAmbientAudioSource = AmbientAudioSource != null ? AmbientAudioSource : VoiceAudioSource;
            if (_activeAmbientAudioSource == null || clip == null)
            {
                _ambientVoiceCompleted = true;
                return;
            }

            _activeAmbientAudioSource.clip = clip;
            _activeAmbientAudioSource.Play();
            _ambientVoiceCompleted = false;
        }

        private void UpdateAmbientVoice()
        {
            if (_ambientVoiceCompleted)
                return;

            if (_activeAmbientAudioSource == null || !_activeAmbientAudioSource.isPlaying)
                _ambientVoiceCompleted = true;
        }

        private void StopAmbientVoice()
        {
            if (_activeAmbientAudioSource != null && _activeAmbientAudioSource.isPlaying)
                _activeAmbientAudioSource.Stop();

            _activeAmbientAudioSource = null;
            _ambientVoiceCompleted = true;
        }

        private bool ShouldUseAmbientTypewriter(AmbientMode mode)
        {
            if (!AmbientUseTypewriter)
                return false;

            return mode == AmbientMode.Bubble || mode == AmbientMode.ProximityMonologue;
        }

        private void RaiseAmbientStarted()
        {
            var data = BuildAmbientViewData();
            OnAmbientLineStarted?.Invoke(data);
        }

        private void RaiseAmbientUpdated()
        {
            var data = BuildAmbientViewData();
            OnAmbientLineUpdated?.Invoke(data);

            // 兼容旧版表现层事件：旧事件也只走环境 UI，不再打开正式对话框。
            if (_currentAmbientMode == AmbientMode.Bubble)
                OnAmbientBubble?.Invoke(data.Speaker, data.DisplayText, data.SourceTransform);
            else
                OnAmbientSubtitle?.Invoke(data.Speaker, data.DisplayText);
        }

        private void NotifyAmbientLineCompletedIfNeeded()
        {
            if (_ambientLineCompletedNotified)
                return;

            if (!_ambientTextCompleted || !_ambientVoiceCompleted)
                return;

            _ambientLineCompletedNotified = true;
            OnAmbientLineCompleted?.Invoke(BuildAmbientViewData());
        }

        private AmbientLineViewData BuildAmbientViewData()
        {
            float progress = string.IsNullOrEmpty(_ambientFullText)
                ? 1f
                : (float)_ambientDisplayLength / _ambientFullText.Length;

            return new AmbientLineViewData(
                _ambientSpeaker,
                _ambientFullText,
                _ambientDisplayText,
                _currentAmbientMode,
                _ambientSourceTransform,
                _ambientTextCompleted,
                _ambientVoiceCompleted,
                progress);
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
            if (state != LineState.Completed)
                return;

            OnSentenceTextCompleted?.Invoke();

            if (_blackboard == null || _blackboard.VoiceState != VoiceState.Playing)
                OnSentenceFullyCompleted?.Invoke();
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
        /// 环境叙事单句开始事件。
        /// UI 扩展层可以用它创建气泡或字幕表现。
        /// </summary>
        public event Action<AmbientLineViewData> OnAmbientLineStarted;

        /// <summary>
        /// 环境叙事单句刷新事件。
        /// 使用逐字显示时会多次触发。
        /// </summary>
        public event Action<AmbientLineViewData> OnAmbientLineUpdated;

        /// <summary>
        /// 环境叙事单句文字和语音都完成事件。
        /// 近距离独白驱动器用它自动进入下一句。
        /// </summary>
        public event Action<AmbientLineViewData> OnAmbientLineCompleted;

        /// <summary>
        /// 环境叙事关闭事件（参数为刚结束的模式）
        /// </summary>
        public event Action<AmbientMode> OnAmbientClosed;
    }
}
