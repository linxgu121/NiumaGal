using NiumaGal.Dialogue;
using NiumaGal.Dialogue.Data;
using NiumaGal.Enum;
using UnityEngine;

namespace NiumaGal.Test
{
    /// <summary>
    /// 极简对话触发器（仅用于测试 NiumaGal 核心链路）
    /// 挂在带 SphereCollider (IsTrigger=true) 的 NPC 上
    /// 进入范围按 E 触发对话，对话中再次按 E 推进
    /// </summary>
    public class SimpleDialogueTrigger : MonoBehaviour
    {
        [Header("对话系统引用")]
        [Tooltip("场景中的 NiumaDialogueController")]
        public NiumaDialogueController DialogueController;

        [Tooltip("该 NPC 的对话资产")]
        public DialogueAsset DialogueAsset;

        [Header("触发设置")]
        [Tooltip("玩家标签")]
        public string PlayerTag = "Player";

        [Tooltip("交互按键（临时，后续由 InputSystem 统一接管）")]
        public KeyCode InteractKey = KeyCode.E;

        private bool _playerInRange;

        private void OnTriggerEnter(Collider other)
        {
            if (!other.CompareTag(PlayerTag)) return;
            _playerInRange = true;
            Debug.Log($"[NiumaGal.Test] 进入交互范围：{gameObject.name}，按 {InteractKey} 交谈");
        }

        private void OnTriggerExit(Collider other)
        {
            if (!other.CompareTag(PlayerTag)) return;
            _playerInRange = false;
            Debug.Log($"[NiumaGal.Test] 离开交互范围：{gameObject.name}");
        }

        private void Update()
        {
            if (!_playerInRange) return;
            if (!Input.GetKeyDown(InteractKey)) return;
            

            if (DialogueController == null)
            {
                Debug.LogError("[NiumaGal.Test] DialogueController 未赋值");
                return;
            }

            if (DialogueAsset == null)
            {
                Debug.LogError("[NiumaGal.Test] DialogueAsset 未赋值");
                return;
            }

            // 如果当前没在对话，启动对话；如果在对话中，Advance 由 NiumaGal 输入系统处理
            // 这里只负责"启动"对话
            if (DialogueController.Blackboard.InteractionState == InteractionState.Idle)
            {
                DialogueController.StartDialogue(DialogueAsset);
                Debug.Log("[NiumaGal.Test] 对话启动");
            }
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.yellow;
            var col = GetComponent<SphereCollider>();
            float radius = col != null ? col.radius : 2f;
            Gizmos.DrawWireSphere(transform.position, radius);
        }
    }
}
