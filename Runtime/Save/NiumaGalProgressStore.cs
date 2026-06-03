using System;
using System.Collections.Generic;
using UnityEngine;

namespace NiumaGal.Save
{
    /// <summary>
    /// NiumaGal 进度事实仓库。
    /// 用于记录已读对话、已触发环境叙事等可持久化事实，不参与 UI 表现和对话播放。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class NiumaGalProgressStore : MonoBehaviour
    {
        private readonly HashSet<string> _readDialogueIds = new HashSet<string>(StringComparer.Ordinal);
        private readonly HashSet<string> _triggeredAmbientIds = new HashSet<string>(StringComparer.Ordinal);
        private long _revision;

        /// <summary>
        /// 当前场景中的默认进度仓库。
        /// 只作为未手动绑定时的兜底入口，正式场景仍建议 Inspector 显式绑定。
        /// </summary>
        public static NiumaGalProgressStore Active { get; private set; }

        /// <summary>
        /// Gal 进度修订号。
        /// NiumaSave 通过该值判断是否需要标记存档脏状态。
        /// </summary>
        public long Revision => _revision;

        private void OnEnable()
        {
            if (Active == null)
            {
                Active = this;
            }
        }

        private void OnDisable()
        {
            if (Active == this)
            {
                Active = null;
            }
        }

        /// <summary>
        /// 记录指定对话已经完整读完。
        /// </summary>
        public bool MarkDialogueRead(string dialogueId)
        {
            if (string.IsNullOrWhiteSpace(dialogueId))
            {
                return false;
            }

            var added = _readDialogueIds.Add(dialogueId);
            if (added)
            {
                BumpRevision();
            }

            return added;
        }

        /// <summary>
        /// 查询指定对话是否已经读完。
        /// </summary>
        public bool IsDialogueRead(string dialogueId)
        {
            return !string.IsNullOrWhiteSpace(dialogueId) && _readDialogueIds.Contains(dialogueId);
        }

        /// <summary>
        /// 记录指定环境叙事已经触发。
        /// </summary>
        public bool MarkAmbientTriggered(string ambientId)
        {
            if (string.IsNullOrWhiteSpace(ambientId))
            {
                return false;
            }

            var added = _triggeredAmbientIds.Add(ambientId);
            if (added)
            {
                BumpRevision();
            }

            return added;
        }

        /// <summary>
        /// 查询指定环境叙事是否已经触发。
        /// </summary>
        public bool IsAmbientTriggered(string ambientId)
        {
            return !string.IsNullOrWhiteSpace(ambientId) && _triggeredAmbientIds.Contains(ambientId);
        }

        /// <summary>
        /// 导出 Gal 存档快照。
        /// </summary>
        public GalSaveData ExportSaveData()
        {
            var readDialogueIds = new string[_readDialogueIds.Count];
            _readDialogueIds.CopyTo(readDialogueIds);
            Array.Sort(readDialogueIds, StringComparer.Ordinal);

            var triggeredAmbientIds = new string[_triggeredAmbientIds.Count];
            _triggeredAmbientIds.CopyTo(triggeredAmbientIds);
            Array.Sort(triggeredAmbientIds, StringComparer.Ordinal);

            return new GalSaveData
            {
                ReadDialogueIds = readDialogueIds,
                TriggeredAmbientIds = triggeredAmbientIds
            };
        }

        /// <summary>
        /// 从 Gal 存档快照恢复进度事实。
        /// </summary>
        public void ImportSaveData(GalSaveData saveData)
        {
            _readDialogueIds.Clear();
            _triggeredAmbientIds.Clear();

            AddRange(_readDialogueIds, saveData?.ReadDialogueIds);
            AddRange(_triggeredAmbientIds, saveData?.TriggeredAmbientIds);
            BumpRevision();
        }

        private void BumpRevision()
        {
            _revision = _revision == long.MaxValue ? long.MaxValue : _revision + 1;
        }

        private static void AddRange(HashSet<string> target, string[] values)
        {
            if (target == null || values == null)
            {
                return;
            }

            for (var i = 0; i < values.Length; i++)
            {
                if (!string.IsNullOrWhiteSpace(values[i]))
                {
                    target.Add(values[i]);
                }
            }
        }
    }
}
