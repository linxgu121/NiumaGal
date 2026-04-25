using NiumaGal.Dialogue.Input.Base;
using NiumaGal.Dialogue.Input.Data;
using UnityEngine;
using UnityEngine.InputSystem;

namespace NiumaGal.Dialogue.Input
{
    /// <summary>
    /// 对话输入源
    /// </summary>
    public class GalInputSource : InputSourceBase
    {
        [Header("输入动作引用")]
        public InputActionReference advanceAction;
        public InputActionReference skipUnitAction;
        public InputActionReference toggleAutoAction;
        public InputActionReference menuAction;
        public InputActionReference logAction;
        public InputActionReference hideUIAction;
        public InputActionReference saveAction;
        public InputActionReference loadAction;

         private void OnEnable() => ToggleActions(true);
        private void OnDisable() => ToggleActions(false);

        public override void FetchRawInput(ref GalRawInputData rawData)
        {
            // Advance 需要 Pressed（长按检测）和 JustPressed（单帧触发）
            rawData.AdvancePressed = advanceAction?.action.IsPressed() ?? false;
            rawData.AdvanceJustPressed = advanceAction?.action.WasPressedThisFrame() ?? false;

            // 其他命令仅采样 JustPressed
            rawData.SkipUnitJustPressed = skipUnitAction?.action.WasPressedThisFrame() ?? false;
            rawData.ToggleAutoJustPressed = toggleAutoAction?.action.WasPressedThisFrame() ?? false;
            rawData.MenuJustPressed = menuAction?.action.WasPressedThisFrame() ?? false;
            rawData.LogJustPressed = logAction?.action.WasPressedThisFrame() ?? false;
            rawData.HideUIJustPressed = hideUIAction?.action.WasPressedThisFrame() ?? false;
            rawData.SaveJustPressed = saveAction?.action.WasPressedThisFrame() ?? false;
            rawData.LoadJustPressed = loadAction?.action.WasPressedThisFrame() ?? false;
        }

        /// <summary>
        /// 从输入源采样原始输入数据并写入结构体
        /// </summary>
        /// <param name="enable"></param>
        private void ToggleActions(bool enable)
        {
            InputActionReference[] all = {
                advanceAction, skipUnitAction, toggleAutoAction, menuAction,
                logAction, hideUIAction, saveAction, loadAction
            };

            foreach (var ar in all)
            {
                if (ar == null) continue;
                if (enable) ar.action.Enable();
                else ar.action.Disable();
            }
        }
    }
}
