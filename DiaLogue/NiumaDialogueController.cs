using NiumaGal.Dialogue.Arbitration;
using NiumaGal.Dialogue.Config;
using NiumaGal.Dialogue.Data;
using NiumaGal.Dialogue.Driver;
using NiumaGal.Dialogue.Input;
using NiumaGal.Dialogue.RuntimeData;
using NiumaGal.Enum;
using NiumaGal.Presenter;
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

        [Header("输入源")]
        public GalInputSource InputSourceRef;

        [Header("自动播放驱动（可选，未赋值则自动创建）")]
        public AutoPlayDriver AutoPlayDriver;

        // 核心系统
        public NiumaGalBlackboard Blackboard { get; private set; }
        public GalInputPipeline InputPipeline { get; private set; }
        public StateMachine InteractionSM { get; private set; }
        public StateMachine ScriptSM { get; private set; }
        public GalArbiter Arbiter { get; private set; }

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
            if (Presenter is DialoguePresenter dp)
                dp.SetDialogueAsset(asset);

            Arbiter.StartDialogue(asset);
        }

        /// <summary>强制关闭当前对话</summary>
        public void ForceCloseDialogue() => Arbiter.CloseDialogue();

        /// <summary>
        /// 处理输入请求，调用 Arbiter 进行仲裁
        /// </summary>
        private void ConsumeInput()
        {
            var proc = InputPipeline.Current.currentFrameData.Processed;

            // 缓存期内或本帧按下
            if (proc.AdvanceJustPressed || proc.AdvanceBufferTimer > 0)
            {
                Arbiter.ProcessInput(new InputRequest(InputCommand.Advance));
                InputPipeline.ConsumeAdvancePressed();
            }

            // 长按持续状态，每帧发送
            if (proc.FastForwardActive)
                Arbiter.ProcessInput(new InputRequest(InputCommand.FastForward));

            if (proc.SkipUnitJustPressed) Arbiter.ProcessInput(new InputRequest(InputCommand.SkipUnit));
            if (proc.ToggleAutoJustPressed) Arbiter.ProcessInput(new InputRequest(InputCommand.ToggleAuto));
            if (proc.MenuJustPressed) Arbiter.ProcessInput(new InputRequest(InputCommand.Menu));
            if (proc.LogJustPressed) Arbiter.ProcessInput(new InputRequest(InputCommand.Log));
            if (proc.HideUIJustPressed) Arbiter.ProcessInput(new InputRequest(InputCommand.HideUI));
            if (proc.SaveJustPressed) Arbiter.ProcessInput(new InputRequest(InputCommand.Save));
            if (proc.LoadJustPressed) Arbiter.ProcessInput(new InputRequest(InputCommand.Load));
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
            Arbiter.OnDialogueClosed += () => Presenter?.CloseDialogue();
            Arbiter.OnHideUI += () => Presenter?.HideUI();

            Arbiter.OnOpenMenu += () => { /* 转发给外部 MenuManager */ };
            Arbiter.OnOpenLog += () => { /* 转发给外部 LogManager */ };
            Arbiter.OnSave += ctx => { /* 转发给外部 SaveManager */ };
            Arbiter.OnLoad += ctx => { /* 转发给外部 LoadManager */ };
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