using System.Collections.Generic;
using NiumaGal.Dialogue.Data;
using UnityEngine;

namespace NiumaGal.Extension.Ambient
{
    [CreateAssetMenu(fileName = "AmbientAsset", menuName = "NiumaGal/Ambient/AmbientAsset")]
    public class AmbientAsset : ScriptableObject
    {
        [Header("台词池")]
        [Tooltip("环境台词池，随机播放其中一句；独白模式则按顺序播放全部")]
        public List<DialogueSentence> Lines = new List<DialogueSentence>();

        [Tooltip("是否随机选择台词。关闭后会按顺序循环播放。")]
        public bool RandomLine = true;

        [Tooltip("随机选择时是否避免连续两次播放同一句。台词数量小于 2 时该选项无效。")]
        public bool AvoidImmediateRepeat = true;

        [Header("触发设置")]
        [Tooltip("触发半径(Bubble/Subtitle 距离检测用）")]
        public float TriggerRadius = 5f;

        [Tooltip("冷却时间（秒），防止反复触发")]
        public float Cooldown = 10f;

        [Tooltip("是否只触发一次")]
        public bool OneShot = false;

        [Tooltip("玩家离开触发半径时是否关闭当前环境叙事。适合靠近才显示的头顶气泡。")]
        public bool CloseWhenExitRange = false;

        [Tooltip("是否需要玩家与叙事源之间没有遮挡。开启后会使用 ObstructionMask 做视线检测。")]
        public bool RequireLineOfSight = false;

        [Tooltip("视线检测遮挡层。必须设置遮挡层，否则开启 RequireLineOfSight 后视线检测不会产生遮挡效果。墙体、门板、地形等应放入该层。")]
        public LayerMask ObstructionMask;

        [Tooltip("视线检测起点在玩家本地空间的偏移。默认约等于站立玩家眼睛高度，蹲伏或特殊体型玩家可单独调整。")]
        public Vector3 PlayerEyeOffset = new Vector3(0f, 1.5f, 0f);

        [Tooltip("视线检测终点在叙事源本地空间的偏移。默认约等于普通 NPC 头部/嘴部高度，小孩、巨人或低矮物体需要调整。")]
        public Vector3 SourceMouthOffset = new Vector3(0f, 1f, 0f);

        [Header("表现设置")]
        [Tooltip("默认表现模式")]
        public AmbientMode DefaultMode = AmbientMode.Bubble;

        [Tooltip("气泡持续时间(秒),0 则按文本长度自动计算")]
        public float BubbleDuration = 3f;

        [Tooltip("独白模式下句间停顿（秒）")]
        public float MonologueLineInterval = 0.5f;
    }
}
