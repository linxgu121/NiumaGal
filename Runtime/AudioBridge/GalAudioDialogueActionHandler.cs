using System;
using NiumaAudio.Bridge;
using NiumaAudio.Controller;
using NiumaAudio.Data;
using NiumaAudio.Service;
using NiumaGal.Dialogue;
using NiumaGal.Dialogue.Data;
using NiumaGal.Dialogue.Service;
using NiumaGal.Enum;
using UnityEngine;

namespace NiumaGal.AudioBridge
{
    /// <summary>
    /// Gal 对话音频行为桥接脚本。
    /// 挂在对话所在场景的 DialogueRoot 或 AudioRoot 上，用于把 DialogueActionType.PlayAudioCue 转发给 NiumaAudio。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class GalAudioDialogueActionHandler : MonoBehaviour, IDialogueActionHandler
    {
        private const string CueIdKey = "cueId";
        private const string AddressKeyKey = "addressKey";
        private const string VolumeScaleKey = "volumeScale";
        private const string FadeSecondsKey = "fadeSeconds";
        private const string OverrideBusKey = "overrideBus";
        private const string BusKey = "bus";

        [Header("对话服务")]
        [Tooltip("当前场景里的 NiumaDialogueController。启用时会把本脚本注册为对话行为处理器，用于处理 PlayAudioCue 行为。")]
        [SerializeField] private NiumaDialogueController dialogueController;

        [Tooltip("备用对话行为处理脚本。请拖 MiniGameDialogueActionHandler、任务/剧情等其它 Dialogue 行为桥接脚本；本脚本不认识的行为会交给它继续处理。不要配置成 A 指向 B、B 又指回 A 的循环链。")]
        [SerializeField] private MonoBehaviour fallbackActionHandlerProvider;

        [Tooltip("启用组件时是否自动注册到 NiumaDialogueController 的 DialogueConfigurationService。正式场景建议开启。")]
        [SerializeField] private bool registerOnEnable = true;

        [Tooltip("禁用组件时是否把对话行为处理器恢复为备用脚本。没有备用脚本时会清空行为处理器。")]
        [SerializeField] private bool restoreFallbackOnDisable = true;

        [Header("音频服务")]
        [Tooltip("全局音频控制器。请拖 AudioRoot 上的 NiumaAudioController；为空时可自动查找。")]
        [SerializeField] private NiumaAudioController audioController;

        [Tooltip("未手动绑定控制器时是否自动查找场景里的 NiumaDialogueController / NiumaAudioController。正式场景建议手动绑定。")]
        [SerializeField] private bool autoFindReferences = true;

        [Header("播放规则")]
        [Tooltip("默认音量倍率。DialogueAction.FloatValue 大于 0 时会覆盖该值；也可在 CustomData 填 volumeScale 精确控制。")]
        [Min(0f)]
        [SerializeField] private float defaultVolumeScale = 1f;

        [Tooltip("默认淡入淡出时间。也可在 DialogueAction.CustomData 填 fadeSeconds 覆盖。普通 UI/SFX 通常保持 0。")]
        [Min(0f)]
        [SerializeField] private float defaultFadeSeconds;

        [Tooltip("播放失败时是否让本次对话行为失败。关闭时只输出警告并继续对话，避免缺失 CueId 阻断剧情。")]
        [SerializeField] private bool failActionWhenAudioFails;

        [Tooltip("遇到本脚本不支持的 Action 类型时是否直接视为成功。没有统一 ActionRouter 前建议开启，避免阻塞任务、剧情、小游戏等其它行为。")]
        [SerializeField] private bool passUnsupportedActions = true;

        [Header("调试")]
        [Tooltip("配置缺失、CueId 缺失或播放失败时是否输出警告。")]
        [SerializeField] private bool logWarnings = true;

        private IAudioCommand _runtimeAudioCommand;
        private IDialogueActionHandler _fallbackActionHandler;
        private bool _isExecutingFallback;

        public AudioOperationResult LastAudioResult { get; private set; }

        public void SetAudioCommand(IAudioCommand command)
        {
            _runtimeAudioCommand = command;
        }

        public void SetDialogueController(NiumaDialogueController controller)
        {
            dialogueController = controller;
        }

        public void SetAudioController(NiumaAudioController controller)
        {
            audioController = controller;
        }

        private void OnEnable()
        {
            ResolveReferences(logWarnings);

            if (registerOnEnable)
            {
                RegisterToDialogueService();
            }
        }

        private void OnDisable()
        {
            if (!restoreFallbackOnDisable || dialogueController?.DialogueConfigurationService == null)
            {
                return;
            }

            ResolveFallbackHandler(false);
            dialogueController.DialogueConfigurationService.SetActionHandler(_fallbackActionHandler);
        }

        /// <summary>
        /// 执行对话行为。只处理 PlayAudioCue，其它行为交给备用处理器或按配置放行。
        /// </summary>
        public DialogueOperationResult Execute(in DialogueActionContext context)
        {
            var action = context.Action;
            if (action == null)
            {
                return Fail(context, "对话行为为空，无法播放音频。");
            }

            if (action.Type != DialogueActionType.PlayAudioCue)
            {
                return ExecuteUnsupported(in context);
            }

            return ExecutePlayAudioCue(in context);
        }

        [ContextMenu("NiumaGal/AudioBridge/注册为对话音频行为处理器")]
        private void RegisterToDialogueService()
        {
            if (!ResolveReferences(logWarnings) || dialogueController?.DialogueConfigurationService == null)
            {
                LogWarning("未找到 NiumaDialogueController 或 DialogueConfigurationService，无法注册 Gal 音频行为处理器。", true);
                return;
            }

            ResolveFallbackHandler(false);
            dialogueController.DialogueConfigurationService.SetActionHandler(this);
        }

        private DialogueOperationResult ExecutePlayAudioCue(in DialogueActionContext context)
        {
            var binding = BuildCueBinding(context.Action);
            if (!binding.HasPlayableKey)
            {
                return HandleAudioFailure(in context, "PlayAudioCue 未配置 CueId 或 AddressKey。TargetId 应填写 AudioCueDefinition.CueId。");
            }

            if (!TryResolveAudioCommand(out var command))
            {
                return HandleAudioFailure(in context, "未找到 NiumaAudioController 或 IAudioCommand，无法播放对话音频。");
            }

            LastAudioResult = command.PlayCue(binding.ToPlayRequest("NiumaGal"));
            if (LastAudioResult == null || !LastAudioResult.Succeeded)
            {
                var reason = LastAudioResult != null ? LastAudioResult.FailureReason.ToString() : "NullResult";
                var message = LastAudioResult != null ? LastAudioResult.Message : null;
                return HandleAudioFailure(in context, $"对话音频播放失败：{reason} {message}");
            }

            return Success(context);
        }

        private DialogueOperationResult ExecuteUnsupported(in DialogueActionContext context)
        {
            ResolveFallbackHandler(false);
            if (_fallbackActionHandler != null && !ReferenceEquals(_fallbackActionHandler, this))
            {
                if (_isExecutingFallback)
                {
                    var message = "检测到 DialogueActionHandler 备用链循环。请检查 GalAudioDialogueActionHandler 与其它 Dialogue 行为桥接脚本的 fallback 配置。";
                    LogWarning(message, true);
                    return passUnsupportedActions ? Success(context) : Fail(context, message);
                }

                _isExecutingFallback = true;
                try
                {
                    return _fallbackActionHandler.Execute(in context);
                }
                finally
                {
                    _isExecutingFallback = false;
                }
            }

            if (passUnsupportedActions)
            {
                return Success(context);
            }

            return Fail(context, $"GalAudioDialogueActionHandler 不支持该行为类型：{context.Action.Type}");
        }

        private DialogueOperationResult HandleAudioFailure(in DialogueActionContext context, string message)
        {
            LogWarning(message, true);
            return failActionWhenAudioFails ? Fail(context, message) : Success(context);
        }

        private AudioCueBinding BuildCueBinding(DialogueActionData action)
        {
            var binding = new AudioCueBinding
            {
                CueId = FirstNonEmpty(GetCustomString(action, CueIdKey), action.TargetId),
                AddressKey = FirstNonEmpty(GetCustomString(action, AddressKeyKey), action.StringValue),
                VolumeScale = ResolveFloat(action, VolumeScaleKey, action.FloatValue > 0f ? action.FloatValue : defaultVolumeScale),
                FadeSeconds = ResolveFloat(action, FadeSecondsKey, defaultFadeSeconds),
                SourceModule = "NiumaGal"
            };

            if (ResolveBool(action, OverrideBusKey, false))
            {
                binding.OverrideBus = true;
                binding.Bus = ResolveBus(action, AudioBus.Sfx);
            }

            return binding;
        }

        private bool ResolveReferences(bool warn)
        {
            if (dialogueController == null && autoFindReferences)
            {
#if UNITY_2023_1_OR_NEWER
                dialogueController = FindFirstObjectByType<NiumaDialogueController>();
#else
                dialogueController = FindObjectOfType<NiumaDialogueController>();
#endif
            }

            ResolveFallbackHandler(false);

            if (dialogueController == null)
            {
                LogWarning("未绑定 NiumaDialogueController，无法注册对话音频行为处理器。", warn);
                return false;
            }

            return true;
        }

        private void ResolveFallbackHandler(bool warn)
        {
            _fallbackActionHandler = fallbackActionHandlerProvider as IDialogueActionHandler;
            if (fallbackActionHandlerProvider != null && _fallbackActionHandler == null)
            {
                LogWarning("备用行为处理脚本绑定不正确。请拖 MiniGameDialogueActionHandler、任务/剧情等 Dialogue 行为桥接脚本；没有其它行为时可留空。", warn);
            }
        }

        private bool TryResolveAudioCommand(out IAudioCommand command)
        {
            var resolved = AudioBridgeResolver.TryResolveCommand(
                _runtimeAudioCommand,
                null,
                audioController,
                autoFindReferences,
                out command,
                out var resolvedController);

            if (resolvedController != null)
            {
                audioController = resolvedController;
            }

            return resolved;
        }

        private float ResolveFloat(DialogueActionData action, string key, float defaultValue)
        {
            var customValue = GetCustomString(action, key);
            if (string.IsNullOrWhiteSpace(customValue))
            {
                return Mathf.Max(0f, defaultValue);
            }

            return float.TryParse(customValue, out var result)
                ? Mathf.Max(0f, result)
                : Mathf.Max(0f, defaultValue);
        }

        private bool ResolveBool(DialogueActionData action, string key, bool defaultValue)
        {
            var customValue = GetCustomString(action, key);
            if (string.IsNullOrWhiteSpace(customValue))
            {
                return defaultValue;
            }

            if (bool.TryParse(customValue, out var result))
            {
                return result;
            }

            if (int.TryParse(customValue, out var intResult))
            {
                return intResult != 0;
            }

            return defaultValue;
        }

        private AudioBus ResolveBus(DialogueActionData action, AudioBus defaultBus)
        {
            var customValue = GetCustomString(action, BusKey);
            if (string.IsNullOrWhiteSpace(customValue))
            {
                return defaultBus;
            }

            return System.Enum.TryParse(customValue, true, out AudioBus result)
                ? result
                : defaultBus;
        }

        private static string GetCustomString(DialogueActionData action, string key)
        {
            if (action?.CustomData == null || string.IsNullOrWhiteSpace(key))
            {
                return null;
            }

            for (var i = 0; i < action.CustomData.Length; i++)
            {
                var entry = action.CustomData[i];
                if (entry != null && string.Equals(entry.Key, key, StringComparison.OrdinalIgnoreCase))
                {
                    return entry.Value;
                }
            }

            return null;
        }

        private static string FirstNonEmpty(string first, string second)
        {
            if (!string.IsNullOrWhiteSpace(first))
            {
                return first.Trim();
            }

            return !string.IsNullOrWhiteSpace(second) ? second.Trim() : null;
        }

        private static DialogueOperationResult Success(in DialogueActionContext context)
        {
            return DialogueOperationResult.Success(
                ResolveDialogueId(context.DialogueAsset),
                ResolveSentenceId(context.Sentence),
                context.Choice?.ChoiceId);
        }

        private DialogueOperationResult Fail(in DialogueActionContext context, string message)
        {
            LogWarning(message, true);
            return DialogueOperationResult.Fail(
                DialogueOperationFailureReason.ActionFailed,
                message,
                ResolveDialogueId(context.DialogueAsset),
                ResolveSentenceId(context.Sentence),
                context.Choice?.ChoiceId);
        }

        private static string ResolveDialogueId(DialogueAsset asset)
        {
            return asset != null ? asset.DialogueId : null;
        }

        private static string ResolveSentenceId(DialogueSentence sentence)
        {
            return sentence != null ? sentence.SentenceId : null;
        }

        private void LogWarning(string message, bool enabled)
        {
            if (enabled && logWarnings)
            {
                Debug.LogWarning($"[GalAudioDialogueActionHandler] {message}", this);
            }
        }
    }
}
