using NiumaGal.Presenter;
using UnityEngine;

namespace NiumaGal.Extension.Ambient
{
    /// <summary>
    /// 环境叙事驱动器
    /// 挂载在 NPC / 场景物体上，负责距离检测与触发
    /// 不走核心状态机，直接驱动 DialoguePresenter 的环境接口
    /// </summary>
    public class AmbientDialogueDriver : MonoBehaviour
    {
        [Header("配置资产")]
        public AmbientAsset Asset;

        [Header("目标玩家")]
        [Tooltip("未赋值则自动查找 Tag 为 Player 的物体")]
        public Transform PlayerTransform;

        [Header("表现层引用")]
        [Tooltip("未赋值则自动查找场景中的 DialoguePresenter")]
        public DialoguePresenter Presenter;

        private float _lastTriggerTime = -999f;
        private bool _hasTriggered;
        private bool _isInRange;

         private void Awake()
        {
            if (PlayerTransform == null)
            {
                var player = GameObject.FindGameObjectWithTag("Player");
                if (player != null) PlayerTransform = player.transform;
            }

            if (Presenter == null)
                Presenter = FindObjectOfType<DialoguePresenter>();
        }

        private void Update()
        {
            if (Asset == null || PlayerTransform == null || Presenter == null) return;
            if (Asset.OneShot && _hasTriggered) return;

            float dist = Vector3.Distance(transform.position, PlayerTransform.position);
            bool wasInRange = _isInRange;
            _isInRange = dist <= Asset.TriggerRadius;

            // 进入范围时触发
            if (_isInRange && !wasInRange)
            {
                if (Time.time - _lastTriggerTime >= Asset.Cooldown)
                    PlayAmbientLine();
            }
        }

        /// <summary>
        /// 距离检测与触发逻辑
        /// </summary>
        private void PlayAmbientLine()
        {
            if (Asset.Lines.Count == 0) return;

            // 随机取一句
            var line = Asset.Lines[Random.Range(0, Asset.Lines.Count)];
            
            Presenter.PlayAmbient(line, Asset.DefaultMode, transform);

            _lastTriggerTime = Time.time;
            _hasTriggered = true;
        }

        /// <summary>
        /// 在 Scene 视图中绘制触发范围
        /// </summary>
        private void OnDrawGizmosSelected()
        {
            if (Asset == null) return;
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(transform.position, Asset.TriggerRadius);
        }
    }
}
