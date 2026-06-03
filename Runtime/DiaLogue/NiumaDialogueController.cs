using System;
using NiumaGal.Dialogue.Arbitration;
using NiumaGal.Dialogue.Config;
using NiumaGal.Dialogue.Data;
using NiumaGal.Dialogue.Driver;
using NiumaGal.Dialogue.Input;
using NiumaGal.Dialogue.RuntimeData;
using NiumaGal.Dialogue.Service;
using NiumaGal.Enum;
using NiumaGal.Presenter;
using NiumaGal.Save;
using NiumaGal.State;
using UnityEngine;

namespace NiumaGal.Dialogue
{
    /// <summary>
    /// NiumaGal 对话系统总控制器
    /// 负责子系统初始化与驱动
    /// </summary>
    public class NiumaDialogueController : MonoBehaviour
    {
        [Header("系统配置")]
        public NiumaGalSO Config;

        [Header("对话资产注册表（可选）")]
        [Tooltip("供 DialogueService 通过 DialogueId 启动对话。为空时仍可直接传入 DialogueAsset。")]
        public DialogueAsset[] DialogueAssets = Array.Empty<DialogueAsset>();

        [Header("输入源")]
        public GalInputSource InputSourceRef;

        [Header("自动播放驱动（可选，未赋值则自动创建）")]
        public AutoPlayDriver AutoPlayDriver;

        [Header("TPC 输入阻塞（可选）")]
        [Tooltip("对话中时，阻塞该输入源的输入")]
        public NiumaTPC.Character.Input.Base.InputSourceBase TPCInputSource;

        [Header("进度记录（可选）")]
        [Tooltip("Gal 进度事实仓库。用于记录已读对话 ID；为空时会自动查找场景中的 NiumaGalProgressStore。")]
        public NiumaGalProgressStore ProgressStore;

        // 核心系统
        public NiumaGalBlackboard Blackboard { get; private set; }
        public GalInputPipeline InputPipeline { get; private set; }
        public StateMachine InteractionSM { get; private set; }
        public StateMachine ScriptSM { get; private set; }
        public GalArbiter Arbiter { get; private set; }
        public IDialogueService DialogueService { get; private set; }
        public IDialogueConfigurationService DialogueConfigurationService { get; private set; }

        // 表现层（由场景挂载或自动查找）
        public IDialoguePresenter Presenter { get; set; }

        private bool _booted;

        private void Awake()
        {
            if (Config == null)
            {
                Debug.LogError("[NiumaDialogueController] 未配置 NiumaGalSO,请在 Inspector 中赋值");
            }

            Blackboard = new NiumaGalBlackboard();
            InteractionSM = new StateMachine();
            ScriptSM = new StateMachine();

            Arbiter = new GalArbiter(Blackboard, InteractionSM, ScriptSM);
            InputPipeline = new GalInputPipeline(InputSourceRef);

            //自动播放
            if (AutoPlayDriver == null)
                AutoPlayDriver = gameObject.AddComponent<AutoPlayDriver>();
            AutoPlayDriver.Initialize(Arbiter, Blackboard, Config?.Core);

            // 表现层初始化
            if (Presenter == null)
                Presenter = GetComponent<DialoguePresenter>() ?? FindObjectOfType<DialoguePresenter>();

            if (Presenter is DialoguePresenter concretePresenter)
                concretePresenter.Initialize(Blackboard, Config);

            if (ProgressStore == null)
                ProgressStore = GetComponent<NiumaGalProgressStore>() ?? FindObjectOfType<NiumaGalProgressStore>();

            var dialogueService = new DialogueService(
                Blackboard,
                Arbiter,
                ProgressStore,
                DialogueAssets,
                BootIfNeeded,
                OnDialogueServiceStarting);
            DialogueService = dialogueService;
            DialogueConfigurationService = dialogueService;

            BindArbiterEvents();
            Blackboard.OnScriptStateChanged += OnScriptStateChanged;
        }

        private void Start() => BootIfNeeded();

        /// <summary>
        /// 延迟启动，确保在 Presenter 等外部系统准备就绪后再初始化状态机和黑板
        /// </summary>
        private void BootIfNeeded()
        {
            if (_booted) return;
            InteractionSM.Initialize(new InteractionIdleState(Blackboard));
            ScriptSM.Initialize(new ScriptIdleState(Blackboard));
            _booted = true;
        }

        private void Update()
        {
            if (!_booted) return;
            //1.输入采样
            InputPipeline.Update();
            //2. 消费输入-仲裁器
            ConsumeInput();

            //3.状态机逻辑更新
            InteractionSM.CurrentState?.LogicUpdate();
            ScriptSM.CurrentState?.LogicUpdate();
        }

        /// <summary>
        /// 由外部交互系统调用以启动对话
        /// </summary>
        public void StartDialogue(DialogueAsset asset)
        {
            DialogueService?.StartDialogue(new DialogueStartRequest
            {
                DialogueAsset = asset,
                SourceModule = nameof(NiumaDialogueController)
            });
        }

        /// <summary>
        /// 通过稳定 DialogueId 启动对话。
        /// 用于任务、剧情、MiniGame 入口等外部模块，不直接依赖 ScriptableObject 引用。
        /// </summary>
        public DialogueOperationResult StartDialogueById(string dialogueId, string actorId = null, string sourceModule = null)
        {
            return DialogueService?.StartDialogue(new DialogueStartRequest
            {
                DialogueId = dialogueId,
                ActorId = actorId,
                SourceModule = sourceModule ?? nameof(NiumaDialogueController)
            }) ?? DialogueOperationResult.Fail(DialogueOperationFailureReason.ServiceNotReady, "DialogueService 尚未初始化。", dialogueId);
        }

        /// <summary>
        /// 选择当前句子的对话选项。
        /// UI 桥接层后续会调用该入口完成“进入你画我猜 / 下次再说”等分支。
        /// </summary>
        public DialogueOperationResult SelectChoice(string choiceId, string actorId = null, string sourceModule = null)
        {
            return DialogueService?.SelectChoice(new DialogueChoiceSelectRequest
            {
                ChoiceId = choiceId,
                ActorId = actorId,
                SourceModule = sourceModule ?? nameof(NiumaDialogueController)
            }) ?? DialogueOperationResult.Fail(DialogueOperationFailureReason.ServiceNotReady, "DialogueService 尚未初始化。", null, null, choiceId);
        }

        private void OnDialogueServiceStarting(DialogueAsset asset)
        {
            if (Presenter is DialoguePresenter dp)
                dp.SetDialogueAsset(asset);

            TPCInputSource?.SetBlocked(true);
        }

        private void OnDialogueClosedInternal()
        {
            MarkCurrentDialogueReadIfCompleted();
            Presenter?.CloseDialogue();

            TPCInputSource?.SetBlocked(false);
        }

        /// <summary>
        /// 当前对话完整播完时，记录已读对话 ID。
        /// 强制关闭或中途跳出不会标记，避免把未读完的剧情写进存档。
        /// </summary>
        private void MarkCurrentDialogueReadIfCompleted()
        {
            var dialogue = Blackboard?.CurrentDialogue;
            if (dialogue == null || dialogue.Sentences == null)
                return;

            if (Blackboard.CurrentSentenceIndex < dialogue.Sentences.Count)
                return;

            var dialogueId = ResolveDialogueId(dialogue);
            if (string.IsNullOrWhiteSpace(dialogueId))
                return;

            if (ProgressStore == null)
                ProgressStore = NiumaGalProgressStore.Active ?? FindObjectOfType<NiumaGalProgressStore>();

            ProgressStore?.MarkDialogueRead(dialogueId);
        }

        private static string ResolveDialogueId(DialogueAsset dialogue)
        {
            return dialogue == null ? null : dialogue.DialogueId;
        }

        /// <summary>强制关闭当前对话</summary>
        public void ForceCloseDialogue()
        {
            DialogueService?.ForceClose(new DialogueCloseRequest
            {
                SourceModule = nameof(NiumaDialogueController),
                MarkAsCompleted = false
            });
        }

        /// <summary>
        /// 处理输入请求，调用 Arbiter 进行仲裁
        /// </summary>
        private void ConsumeInput()
        {
            var proc = InputPipeline.Current.currentFrameData.Processed;

            // 缓存期内或本帧按下
            if (proc.AdvanceJustPressed || proc.AdvanceBufferTimer > 0)
            {
                DialogueService?.Advance(new DialogueAdvanceRequest
                {
                    SourceModule = nameof(GalInputPipeline)
                });
                InputPipeline.ConsumeAdvancePressed();
            }

            // 长按持续状态，每帧发送
            if (proc.FastForwardActive)
                Arbiter.ProcessInput(new InputRequest(InputCommand.FastForward));

            if (proc.SkipUnitJustPressed) Arbiter.ProcessInput(new InputRequest(InputCommand.SkipUnit));
            if (proc.ToggleAutoJustPressed) Arbiter.ProcessInput(new InputRequest(InputCommand.ToggleAuto));
        }

        /// <summary>
        /// 剧本状态变更时的回调，负责触发 Presenter 播放当前句子
        /// </summary>
        private void OnScriptStateChanged(DialogueScriptState state)
        {
            if (state == DialogueScriptState.Running)
                Presenter?.PlaySentence(Blackboard.CurrentSentenceIndex);
        }
 
        /// <summary>
        /// 绑定 Arbiter 事件到 Presenter 和其他系统
        /// </summary>
        private void BindArbiterEvents()
        {
            Arbiter.OnSkipTypewriter += () => Presenter?.SkipTypewriter();
            Arbiter.OnStopVoice += () => Presenter?.StopVoice();
            Arbiter.OnDialogueClosed += OnDialogueClosedInternal;
        }

        /// <summary>
        /// 销毁前解绑事件，避免内存泄漏
        /// </summary>
        private void OnDestroy()
        {
            if (Blackboard != null)
                Blackboard.OnScriptStateChanged -= OnScriptStateChanged;
        }

    }
}
