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

        [Header("外部条件与行为处理器（可选）")]
        [Tooltip("实现 IDialogueConditionResolver 的组件。用于把任务、剧情、背包等条件交给外部模块判断。")]
        [SerializeField] private MonoBehaviour conditionResolverProvider;

        [Tooltip("实现 IDialogueActionHandler 的组件。用于把 OpenMiniGame、LoadScene、Quest 等行为交给外部模块执行。")]
        [SerializeField] private MonoBehaviour actionHandlerProvider;

        [Tooltip("未手动绑定处理器时，是否自动在场景中查找实现了对应接口的组件。正式场景建议手动绑定，调试阶段可开启。")]
        [SerializeField] private bool autoFindExternalHandlers = true;

        [Tooltip("引用缺失或绑定类型不正确时是否输出警告。")]
        [SerializeField] private bool logWarnings = true;

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
        private IDialogueConditionResolver _conditionResolver;
        private IDialogueActionHandler _actionHandler;
        private bool _externalHandlersLocked;

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
                Presenter = GetComponent<DialoguePresenter>() ?? FindSceneObject<DialoguePresenter>();

            if (Presenter is DialoguePresenter concretePresenter)
                concretePresenter.Initialize(Blackboard, Config);

            if (ProgressStore == null)
                ProgressStore = GetComponent<NiumaGalProgressStore>() ?? FindSceneObject<NiumaGalProgressStore>();

            ResolveExternalHandlers(logWarnings);

            var dialogueService = new DialogueService(
                Blackboard,
                Arbiter,
                ProgressStore,
                DialogueAssets,
                BootIfNeeded,
                OnDialogueServiceStarting,
                _conditionResolver,
                _actionHandler);
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
            StartDialogueWithResult(asset);
        }

        /// <summary>
        /// 启动对话并返回结构化结果。交互、剧情或调试工具需要失败原因时使用该入口。
        /// </summary>
        public DialogueOperationResult StartDialogueWithResult(DialogueAsset asset)
        {
            return DialogueService?.StartDialogue(new DialogueStartRequest
            {
                DialogueAsset = asset,
                SourceModule = nameof(NiumaDialogueController)
            }) ?? DialogueOperationResult.Fail(DialogueOperationFailureReason.ServiceNotReady, "DialogueService 尚未初始化。");
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

        /// <summary>
        /// 外部模块可显式注入条件解析器与行为处理器。
        /// lockHandlers 为 true 时，后续自动解析不会覆盖这次注入的对象。
        /// </summary>
        public void SetExternalHandlers(
            IDialogueConditionResolver conditionResolver,
            IDialogueActionHandler actionHandler,
            bool lockHandlers = true)
        {
            _conditionResolver = conditionResolver;
            _actionHandler = actionHandler;
            _externalHandlersLocked = lockHandlers;
            ApplyExternalHandlersToService();
        }

        /// <summary>
        /// 解除外部处理器锁定，并重新从 Inspector 或场景中解析处理器。
        /// </summary>
        public void UnlockAndResolveExternalHandlers()
        {
            _externalHandlersLocked = false;
            ResolveExternalHandlers(logWarnings);
            ApplyExternalHandlersToService();
        }

        /// <summary>
        /// 热更新对话资产注册表。用于编辑器调试、剧情热加载或模块启动器统一注入。
        /// </summary>
        public void SetDialogueAssets(DialogueAsset[] dialogueAssets)
        {
            DialogueAssets = dialogueAssets ?? Array.Empty<DialogueAsset>();
            DialogueConfigurationService?.SetDialogueAssets(DialogueAssets);
        }

        /// <summary>
        /// 热更新 Gal 进度仓库。读档或切换全局 ProgressStore 后调用，避免 Service 继续持有旧引用。
        /// </summary>
        public void SetProgressStore(NiumaGalProgressStore progressStore)
        {
            ProgressStore = progressStore;
            DialogueConfigurationService?.SetProgressStore(ProgressStore);
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
                ProgressStore = NiumaGalProgressStore.Active ?? FindSceneObject<NiumaGalProgressStore>();

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

        [ContextMenu("NiumaGal/重新解析外部条件与行为处理器")]
        private void ResolveExternalHandlersFromMenu()
        {
            UnlockAndResolveExternalHandlers();
        }

        [ContextMenu("NiumaGal/刷新对话资产注册表")]
        private void RefreshDialogueAssetsFromMenu()
        {
            DialogueConfigurationService?.SetDialogueAssets(DialogueAssets);
        }

        [ContextMenu("NiumaGal/强制关闭当前对话")]
        private void ForceCloseDialogueFromMenu()
        {
            ForceCloseDialogue();
        }

        private void ResolveExternalHandlers(bool warn)
        {
            if (_externalHandlersLocked)
            {
                return;
            }

            _conditionResolver = ResolveProvider<IDialogueConditionResolver>(
                conditionResolverProvider,
                autoFindExternalHandlers,
                warn,
                "conditionResolverProvider");

            _actionHandler = ResolveProvider<IDialogueActionHandler>(
                actionHandlerProvider,
                autoFindExternalHandlers,
                warn,
                "actionHandlerProvider");
        }

        private void ApplyExternalHandlersToService()
        {
            if (DialogueConfigurationService == null)
            {
                return;
            }

            DialogueConfigurationService.SetConditionResolver(_conditionResolver);
            DialogueConfigurationService.SetActionHandler(_actionHandler);
        }

        private T ResolveProvider<T>(MonoBehaviour provider, bool autoFind, bool warn, string fieldName) where T : class
        {
            if (provider != null)
            {
                if (provider is T typedProvider)
                {
                    return typedProvider;
                }

                if (warn && logWarnings)
                {
                    Debug.LogWarning($"[NiumaDialogueController] {fieldName} 未实现 {typeof(T).Name}。", this);
                }

                return null;
            }

            return autoFind ? FindFirstSceneImplementation<T>() : null;
        }

        private static T FindFirstSceneImplementation<T>() where T : class
        {
#if UNITY_2023_1_OR_NEWER
            var behaviours = FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include, FindObjectsSortMode.None);
#else
            var behaviours = FindObjectsOfType<MonoBehaviour>(true);
#endif
            for (var i = 0; i < behaviours.Length; i++)
            {
                if (behaviours[i] is T implementation)
                {
                    return implementation;
                }
            }

            return null;
        }

        private static T FindSceneObject<T>() where T : UnityEngine.Object
        {
#if UNITY_2023_1_OR_NEWER
            return FindFirstObjectByType<T>();
#else
            return FindObjectOfType<T>();
#endif
        }
 
        /// <summary>
        /// 绑定 Arbiter 事件到 Presenter 和其他系统
        /// </summary>
        private void BindArbiterEvents()
        {
            Arbiter.OnSkipTypewriter += HandleSkipTypewriter;
            Arbiter.OnStopVoice += HandleStopVoice;
            Arbiter.OnDialogueClosed += OnDialogueClosedInternal;
        }

        private void UnbindArbiterEvents()
        {
            if (Arbiter == null)
                return;

            Arbiter.OnSkipTypewriter -= HandleSkipTypewriter;
            Arbiter.OnStopVoice -= HandleStopVoice;
            Arbiter.OnDialogueClosed -= OnDialogueClosedInternal;
        }

        private void HandleSkipTypewriter()
        {
            Presenter?.SkipTypewriter();
        }

        private void HandleStopVoice()
        {
            Presenter?.StopVoice();
        }

        private void OnDisable()
        {
            // 组件被禁用时确保玩家输入不会因为对话中断而一直被阻塞。
            TPCInputSource?.SetBlocked(false);
        }

        /// <summary>
        /// 销毁前解绑事件，避免内存泄漏
        /// </summary>
        private void OnDestroy()
        {
            if (Blackboard != null)
                Blackboard.OnScriptStateChanged -= OnScriptStateChanged;

            UnbindArbiterEvents();
        }

    }
}
