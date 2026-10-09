using UnityEngine;

namespace Ghost.Player.PC
{
    // PC 专用：第一人称时锁定并隐藏系统光标，停用时恢复。挂在玩家根物体下，跟着玩家一起开关。
    // VR 版没有光标，整个组件不用。
    public class PcCursorLock : MonoBehaviour
    {
        void OnEnable()
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        void OnDisable()
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
    }
}
