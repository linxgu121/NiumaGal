using System;
using NiumaGal.Dialogue.Data;
using NiumaGal.Enum;

namespace NiumaGal.Dialogue.RuntimeData
{
    public class NiumaGalBlackboard
    {
        // 状态快照
        public InteractionState InteractionState { get; private set; } = InteractionState.Idle;
        public DialogueScriptState ScriptState { get; private set; } = DialogueScriptState.Idle;
        public LineState LineState { get; private set; } = LineState.Idle;
        public VoiceState VoiceState { get; private set; } = VoiceState.Idle;
        public PlaybackMode PlaybackMode { get; private set; } = PlaybackMode.Manual;

        // 外部暂停标志（由 PauseManager/MenuManager 写入）
        public bool IsPaused { get; set; } = false;

        // 对话上下文
        public DialogueAsset CurrentDialogue { get; set; }
        public int CurrentSentenceIndex { get; set; } = 0;
        public string CurrentSpeaker { get; set; }
        public string CurrentText { get; set; }

        // 状态变更事件
        public event Action<InteractionState> OnInteractionStateChanged;
        public event Action<DialogueScriptState> OnScriptStateChanged;
        public event Action<LineState> OnLineStateChanged;
        public event Action<VoiceState> OnVoiceStateChanged;
        public event Action<PlaybackMode> OnPlaybackModeChanged;

        public void SetInteractionState(InteractionState state)
        {
            if (InteractionState == state) return;
            InteractionState = state;
            OnInteractionStateChanged?.Invoke(state);
        }

        public void SetScriptState(DialogueScriptState state)
        {
            if (ScriptState == state) return;
            ScriptState = state;
            OnScriptStateChanged?.Invoke(state);
        }

        public void SetLineState(LineState state)
        {
            if (LineState == state) return;
            LineState = state;
            OnLineStateChanged?.Invoke(state);
        }

        public void SetVoiceState(VoiceState state)
        {
            if (VoiceState == state) return;
            VoiceState = state;
            OnVoiceStateChanged?.Invoke(state);
        }

        public void SetPlaybackMode(PlaybackMode mode)
        {
            if (PlaybackMode == mode) return;
            PlaybackMode = mode;
            OnPlaybackModeChanged?.Invoke(mode);
        }

        public void Reset()
        {
            SetInteractionState(InteractionState.Idle);
            SetScriptState(DialogueScriptState.Idle);
            SetLineState(LineState.Idle);
            SetVoiceState(VoiceState.Idle);
            SetPlaybackMode(PlaybackMode.Manual);
            CurrentDialogue = null;
            CurrentSentenceIndex = 0;
            CurrentSpeaker = null;
            CurrentText = null;
            IsPaused = false;
        }
    }
}
