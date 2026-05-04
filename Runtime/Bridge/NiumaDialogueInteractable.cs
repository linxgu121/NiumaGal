using NiumaGal.Dialogue;
using NiumaGal.Dialogue.Data;
using NiumaGal.Enum;
using NiumaInteract.Core.Data;
using NiumaInteract.Core.Enum;
using NiumaInteract.Core.Interface;
using UnityEngine;

namespace NiumaGal.Bridge
{
    /// <summary>
    /// NiumaGal 对话交互目标。
    /// 挂在 NPC 或可交互物体上，让 NiumaInteract 触发 NiumaGal 的对话启动入口。
    /// </summary>
    public sealed class NiumaDialogueInteractable : MonoBehaviour, IInteractable
    {
        [Header("对话")]
        [Tooltip("场景中的 NiumaDialogueController。为空且开启自动查找时，会在场景中查找。")]
        [SerializeField] private NiumaDialogueController dialogueController;
        [Tooltip("该目标触发时播放的 DialogueAsset。未绑定时目标不可交互。")]
        [SerializeField] private DialogueAsset dialogueAsset;
        [Tooltip("未手动绑定 DialogueController 时，是否自动在场景中查找。")]
        [SerializeField] private bool autoFindDialogueController = true;
        [Tooltip("是否只允许在对话系统空闲时启动对话。通常保持开启，避免重复启动对话。")]
        [SerializeField] private bool onlyWhenDialogueIdle = true;

        [Header("交互显示")]
        [Tooltip("交互 ID，默认为物体名称。可用于任务、存档或调试日志。")]
        [SerializeField] private string interactionId;
        [Tooltip("交互目标显示名称，例如 NPC 名称。")]
        [SerializeField] private string displayName = "NPC";
        [Tooltip("交互提示文本，例如“交谈”。")]
        [SerializeField] private string promptText = "交谈";
        [Tooltip("交互提示类型。世界空间提示通常显示在目标上方。")]
        [SerializeField] private PromptType promptType = PromptType.WorldSpace;
        [Tooltip("世界空间提示挂点。为空时使用 InteractionTransform。")]
        [SerializeField] private Transform promptAnchor;

        [Header("交互规则")]
        [Tooltip("交互检测使用的稳定位置源。为空时使用当前物体 Transform。")]
        [SerializeField] private Transform interactionTransform;
        [Tooltip("交互排序优先级。数值越大越容易成为焦点目标。")]
        [SerializeField] private float priority = 1f;
        [Tooltip("长按触发阈值，单位秒。短按对话保持 0 即可。")]
        [SerializeField] private float longPressDuration;
        [Tooltip("该目标支持的交互类型。普通对话使用 Short。")]
        [SerializeField] private InteractKind supportedKinds = InteractKind.Short;

        public string InteractionId => string.IsNullOrEmpty(interactionId) ? gameObject.name : interactionId;
        public Transform InteractionTransform => interactionTransform != null ? interactionTransform : transform;
        public string DisplayName => displayName;
        public string PromptText => promptText;
        public PromptType PromptType => promptType;
        public Transform PromptAnchor => promptAnchor != null ? promptAnchor : InteractionTransform;
        public float Priority => priority;
        public float LongPressDuration => longPressDuration;
        public InteractKind SupportedKinds => supportedKinds;

        private void Awake()
        {
            ResolveDialogueController();
        }

        /// <summary>
        /// 判断当前是否允许启动该对话。
        /// 这里只检查对话桥接需要的条件，不处理距离、输入和排序。
        /// </summary>
        public bool CanInteract(in InteractionContext context)
        {
            if (!isActiveAndEnabled || dialogueAsset == null)
                return false;

            ResolveDialogueController();
            if (dialogueController == null)
                return false;

            if (!onlyWhenDialogueIdle)
                return true;

            return dialogueController.Blackboard == null ||
                   dialogueController.Blackboard.InteractionState == InteractionState.Idle;
        }

        /// <summary>
        /// 执行已被交互仲裁器确认过的对话交互。
        /// 目标只调用对话控制器，不反向裁决交互是否成功。
        /// </summary>
        public void Interact(in InteractionRequest request)
        {
            if ((supportedKinds & request.Kind) != request.Kind)
                return;

            if (!CanInteract(request.Context))
                return;

            dialogueController.StartDialogue(dialogueAsset);
        }

        private void ResolveDialogueController()
        {
            if (dialogueController != null || !autoFindDialogueController)
                return;

            dialogueController = FindObjectOfType<NiumaDialogueController>();
        }

        private void OnValidate()
        {
            priority = priority > 0f ? priority : 0f;
            longPressDuration = longPressDuration > 0f ? longPressDuration : 0f;

            if (supportedKinds == InteractKind.None)
                supportedKinds = InteractKind.Short;
        }
    }
}
