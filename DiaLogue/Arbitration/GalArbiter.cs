using System;
using NiumaGal.Dialogue.Data;
using NiumaGal.Dialogue.RuntimeData;
using NiumaGal.Enum;
using NiumaGal.State;

namespace NiumaGal.Dialogue.Arbitration
{
    public class GalArbiter
    {
        private readonly NiumaGalBlackboard _blackboard;
        /// <summary>
        /// 交互状态机
        /// </summary>
        private readonly StateMachine _interactionSM;
        /// <summary>
        /// 剧本状态机
        /// </summary>
        private readonly StateMachine _scriptSM;

        public GalArbiter(NiumaGalBlackboard blackboard, StateMachine interactionSM, StateMachine scriptSM)
        {
            _blackboard = blackboard;
            _interactionSM = interactionSM;
            _scriptSM = scriptSM;
        }

        // 事件：由 NiumaDialogueController 订阅并转发给 Presenter/外部系统
        public event Action OnSkipTypewriter;
        public event Action OnStopVoice;
        public event Action OnDialogueClosed;
        public event Action OnOpenMenu;
        public event Action OnOpenLog;
        public event Action OnHideUI;
        public event Action<object> OnSave;
        public event Action<object> OnLoad;

         public bool ProcessInput(in InputRequest request)
        {
            if (_blackboard.IsPaused) return false;

            return request.Command switch
            {
                InputCommand.Advance => HandleAdvance(),
                InputCommand.FastForward => HandleFastForward(),
                InputCommand.SkipUnit => HandleSkipUnit(),
                InputCommand.ToggleAuto => HandleToggleAuto(),
                InputCommand.Menu => HandleMenu(),
                InputCommand.Log => HandleLog(),
                InputCommand.HideUI => HandleHideUI(),
                InputCommand.Save => HandleSave(request.Context),
                InputCommand.Load => HandleLoad(request.Context),
                _ => false
            };
        }

        private bool HandleAdvance()
        {
            var interaction = _blackboard.InteractionState;
            var script = _blackboard.ScriptState;
            var line = _blackboard.LineState;
            var voice = _blackboard.VoiceState;

            if (interaction == InteractionState.Idle) return false;

            // 当前句正在播放中 -> Skip
            if (line == LineState.Playing)
            {
                OnSkipTypewriter?.Invoke();
                return true;
            }

            // 当前句已结束，等待推进
            if (script == DialogueScriptState.BetweenSentences)
            {
                if (voice == VoiceState.Playing)
                    OnStopVoice?.Invoke();

                _blackboard.CurrentSentenceIndex++;
                if (_blackboard.CurrentSentenceIndex >= _blackboard.CurrentDialogue.Sentences.Count)
                    _scriptSM.ChangeState(new ScriptUnitEndedState(_blackboard));
                else
                    _scriptSM.ChangeState(new ScriptRunningState(_blackboard));

                return true;
            }

            // 单元已结束 -> 关闭对话
            if (script == DialogueScriptState.UnitEnded)
            {
                CloseDialogue();
                return true;
            }

            return false;
        }

        private bool HandleFastForward()
        {
            var script = _blackboard.ScriptState;
            if (script != DialogueScriptState.Running && script != DialogueScriptState.BetweenSentences)
                return false;

            if (_blackboard.LineState == LineState.Playing)
                OnSkipTypewriter?.Invoke();

            if (script == DialogueScriptState.BetweenSentences)
                return HandleAdvance();

            return true;
        }

        private bool HandleSkipUnit()
        {
            if (_blackboard.InteractionState == InteractionState.Idle) return false;

            OnSkipTypewriter?.Invoke();
            OnStopVoice?.Invoke();
            _scriptSM.ChangeState(new ScriptUnitEndedState(_blackboard));
            return true;
        }

        private bool HandleToggleAuto()
        {
            if (_blackboard.InteractionState == InteractionState.Idle) return false;
            if (_blackboard.ScriptState == DialogueScriptState.Idle) return false;

            _blackboard.SetPlaybackMode(
                _blackboard.PlaybackMode == PlaybackMode.Manual ? PlaybackMode.Auto : PlaybackMode.Manual
            );
            return true;
        }

         private bool HandleMenu() { OnOpenMenu?.Invoke(); return true; }
        private bool HandleLog() { OnOpenLog?.Invoke(); return true; }
        private bool HandleHideUI() { OnHideUI?.Invoke(); return true; }
        private bool HandleSave(object ctx) { OnSave?.Invoke(ctx); return true; }
        private bool HandleLoad(object ctx) { OnLoad?.Invoke(ctx); return true; }

        /// <summary>
        /// 由外部交互系统调用以启动对话
        /// </summary>
        public void StartDialogue(DialogueAsset asset)
        {
            if (asset == null || asset.Sentences == null || asset.Sentences.Count == 0) return;
            if (_blackboard.InteractionState != InteractionState.Idle) return;

            _blackboard.CurrentDialogue = asset;
            _blackboard.CurrentSentenceIndex = 0;

            _interactionSM.ChangeState(new InteractionInteractingState(_blackboard));
            _scriptSM.ChangeState(new ScriptRunningState(_blackboard));
        }

        /// <summary>
        /// 关闭当前对话
        /// </summary>
        public void CloseDialogue()
        {
            _scriptSM.ChangeState(new ScriptIdleState(_blackboard));
            _interactionSM.ChangeState(new InteractionIdleState(_blackboard));
            OnDialogueClosed?.Invoke();
        }

        /// <summary>
        /// 自动播放心跳（由 AutoPlayDriver 调用）
        /// </summary>
        public void AutoPlayTick()
        {
            if (_blackboard.PlaybackMode != PlaybackMode.Auto) return;
            if (_blackboard.ScriptState != DialogueScriptState.BetweenSentences) return;
            if (_blackboard.LineState != LineState.Completed) return;
            if (_blackboard.VoiceState == VoiceState.Playing) return;

            HandleAdvance();
        }

    }
}
