using NiumaGal.Presenter;
using NiumaGal.Save;
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
        [Tooltip("环境叙事配置资产，包含台词池、触发半径、冷却和表现模式。")]
        public AmbientAsset Asset;

        [Header("目标玩家")]
        [Tooltip("未赋值则自动查找 Tag 为 Player 的物体")]
        public Transform PlayerTransform;

        [Header("表现层引用")]
        [Tooltip("未赋值则自动查找场景中的 DialoguePresenter")]
        public DialoguePresenter Presenter;

        [Header("进度记录（可选）")]
        [Tooltip("Gal 进度事实仓库。用于记录已触发的一次性环境叙事；为空时会自动查找。")]
        public NiumaGalProgressStore ProgressStore;

        private float _lastTriggerTime = -999f;
        private bool _hasTriggered;
        private bool _isInRange;
        private int _nextLineIndex;
        private int _lastLineIndex = -1;
        private bool _reportedMissingObstructionMask;

         private void Awake()
        {
            if (PlayerTransform == null)
            {
                var player = GameObject.FindGameObjectWithTag("Player");
                if (player != null) PlayerTransform = player.transform;
            }

            if (Presenter == null)
                Presenter = FindSceneObject<DialoguePresenter>();

            if (ProgressStore == null)
                ProgressStore = NiumaGalProgressStore.Active ?? FindSceneObject<NiumaGalProgressStore>();
        }

        private void Update()
        {
            if (Asset == null || PlayerTransform == null || Presenter == null) return;
            if (Asset.OneShot && IsOneShotConsumed()) return;

            float dist = Vector3.Distance(transform.position, PlayerTransform.position);
            bool wasInRange = _isInRange;
            _isInRange = dist <= Asset.TriggerRadius;

            // 进入范围时触发
            if (_isInRange && !wasInRange)
            {
                if (CanTrigger())
                    PlayAmbientLine();
            }
            else if (!_isInRange && wasInRange && Asset.CloseWhenExitRange)
            {
                Presenter.CloseAmbient();
            }
        }

        /// <summary>
        /// 距离检测与触发逻辑
        /// </summary>
        private void PlayAmbientLine()
        {
            if (Asset.Lines.Count == 0) return;

            int lineIndex = SelectLineIndex();
            var line = Asset.Lines[lineIndex];
            
            if (!Presenter.PlayAmbient(line, Asset.DefaultMode, transform, Asset.BubbleDuration))
                return;

            _lastTriggerTime = Time.time;
            _hasTriggered = true;
            _lastLineIndex = lineIndex;
            MarkAmbientTriggeredIfNeeded();
        }

        /// <summary>
        /// 判断一次性环境叙事是否已经被本次运行或存档进度消费。
        /// </summary>
        private bool IsOneShotConsumed()
        {
            if (_hasTriggered)
                return true;

            var ambientId = ResolveAmbientId();
            return !string.IsNullOrWhiteSpace(ambientId)
                   && ProgressStore != null
                   && ProgressStore.IsAmbientTriggered(ambientId);
        }

        /// <summary>
        /// 一次性环境叙事成功播放后写入进度事实。
        /// </summary>
        private void MarkAmbientTriggeredIfNeeded()
        {
            if (!Asset.OneShot)
                return;

            var ambientId = ResolveAmbientId();
            if (string.IsNullOrWhiteSpace(ambientId))
                return;

            if (ProgressStore == null)
                ProgressStore = NiumaGalProgressStore.Active ?? FindSceneObject<NiumaGalProgressStore>();

            ProgressStore?.MarkAmbientTriggered(ambientId);
        }

        private bool CanTrigger()
        {
            if (Time.time - _lastTriggerTime < Asset.Cooldown)
                return false;

            if (!Asset.RequireLineOfSight)
                return true;

            if (Asset.ObstructionMask.value == 0)
            {
                if (!_reportedMissingObstructionMask)
                {
                    _reportedMissingObstructionMask = true;
                    Debug.LogWarning("[AmbientDialogueDriver] 已开启 RequireLineOfSight，但 ObstructionMask 未设置，视线检测不会产生遮挡效果。", this);
                }

                return true;
            }

            Vector3 from = PlayerTransform.TransformPoint(Asset.PlayerEyeOffset);
            Vector3 to = transform.TransformPoint(Asset.SourceMouthOffset);
            Vector3 direction = to - from;
            float distance = direction.magnitude;

            if (distance <= 0.01f)
                return true;

            return !Physics.Raycast(
                from,
                direction.normalized,
                distance,
                Asset.ObstructionMask,
                QueryTriggerInteraction.Ignore);
        }

        private int SelectLineIndex()
        {
            int count = Asset.Lines.Count;
            if (count <= 1)
                return 0;

            if (!Asset.RandomLine)
            {
                int index = _nextLineIndex;
                _nextLineIndex = (_nextLineIndex + 1) % count;
                return index;
            }

            int selected = Random.Range(0, count);
            if (Asset.AvoidImmediateRepeat && selected == _lastLineIndex)
                selected = (selected + 1) % count;

            return selected;
        }

        private string ResolveAmbientId()
        {
            return Asset == null ? null : Asset.AmbientId;
        }

        private static T FindSceneObject<T>() where T : Object
        {
#if UNITY_2023_1_OR_NEWER
            return FindFirstObjectByType<T>();
#else
            return FindObjectOfType<T>();
#endif
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
