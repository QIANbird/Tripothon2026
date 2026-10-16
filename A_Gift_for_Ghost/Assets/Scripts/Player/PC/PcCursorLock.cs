using Ghost.Core;
using UnityEngine;

namespace Ghost.Player.PC
{
    // PC 专用：第一人称时锁定并隐藏系统光标，停用时恢复。挂在玩家根物体下，跟着玩家一起开关。
    // 暂停界面打开时解锁（要点"返回开始界面"），关闭时重新锁定。VR 版没有光标，整个组件不用。
    public class PcCursorLock : MonoBehaviour
    {
        void OnEnable()
        {
            PauseMenu.PausedChanged += HandlePaused;
            SetLocked(!PauseMenu.IsPaused);
        }

        void OnDisable()
        {
            PauseMenu.PausedChanged -= HandlePaused;
            SetLocked(false);
        }

        void HandlePaused(bool paused) => SetLocked(!paused);

        static void SetLocked(bool locked)
        {
            Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !locked;
        }
    }
}
