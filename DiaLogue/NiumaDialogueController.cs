using NiumaGal.Dialogue.Input;
using UnityEngine;

namespace NiumaGal.Dialogue
{
    /// <summary>
    /// NiumaGal 对话系统总控制器
    /// 负责子系统初始化与驱动
    /// </summary>
    public class NiumaDialogueController : MonoBehaviour
    {
        [Header("输入源")]
        public GalInputSource InputSourceRef;
    }
}