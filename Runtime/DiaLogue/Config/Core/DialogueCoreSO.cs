using UnityEngine;

namespace NiumaGal.Dialogue.Config.Core
{
    [CreateAssetMenu(fileName = "DialogueCoreSO", menuName = "NiumaGal/Config/DialogueCoreSO", order = 0)]
    public class DialogueCoreSO : ScriptableObject
    {
        [Header("打字机表现")]
        [Tooltip("每个字出现的时间间隔(秒),0为瞬间显示")]
        public float TypewriterInterval = 0.05f;

        [Tooltip("快进时每个字出现的时间间隔(秒),0为瞬间显示")]
        public float FastForwardInterval = 0.005f;

        [Header("自动播放")]
        [Tooltip("自动模式下，句子结束后等待多久自动推进（秒）")]
        public float AutoAdvanceDelay = 1.5f;

        [Header("跳过策略")]
        [Tooltip("是否允许跳过未读过的对话单元(false = 仅已读可跳）")]
        public bool AllowSkipUnread = true;
    }
}