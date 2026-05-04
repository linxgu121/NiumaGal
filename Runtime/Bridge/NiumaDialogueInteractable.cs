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
        [SerializeField] private NiumaDialogueController dialogueController;
        [SerializeField] private DialogueAsset dialogueAsset;
        [SerializeField] private bool autoFindDialogueController = true;
        [SerializeField] private bool onlyWhenDialogueIdle = true;

        [Header("交互显示")]
        [SerializeField] private string interactionId;
        [SerializeField] private string displayName = "NPC";
        [SerializeField] private string promptText = "交谈";
        [SerializeField] private PromptType promptType = PromptType.WorldSpace;
        [SerializeField] private Transform promptAnchor;

        [Header("交互规则")]
        [SerializeField] private Transform interactionTransform;
        [SerializeField] private float priority = 1f;
        [SerializeField] private float longPressDuration;
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
