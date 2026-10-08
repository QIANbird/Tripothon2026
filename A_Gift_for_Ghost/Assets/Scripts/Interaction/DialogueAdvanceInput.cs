using Ghost.Narrative;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Ghost.Interaction
{
    // PC 端的对白推进：在空白处左键点击（PointerInput.TapEmpty），或按 Gameplay 动作表的 Advance（空格 / 回车）。
    // 点中节点、物体或 HUD 时不推进，节点优先；全黑屏时（开场）看不到节点，点哪里都推进。打字中先补全整句，打完再点进入下一句（见 SubtitlePanel.Advance）。
    // 以后 VR 版另写一个发起方，调用同一个 SubtitlePanel.Advance()。
    public class DialogueAdvanceInput : MonoBehaviour
    {
        public PointerInput pointer;
        public SubtitlePanel subtitles;
        [Tooltip("InputSystem_Actions.inputactions 资产")]
        public InputActionAsset actions;
        public string mapName = "Gameplay";

        InputAction advanceAction;

        void OnEnable()
        {
            if (subtitles == null)
            {
                Debug.LogError("[Dialogue] DialogueAdvanceInput 缺少 subtitles", this);
                enabled = false;
                return;
            }
            if (pointer != null)
            {
                pointer.TapEmpty += HandleTapEmpty;
                pointer.Tap += HandleTapNode;
            }
            if (actions != null)
            {
                var map = actions.FindActionMap(mapName, throwIfNotFound: false);
                advanceAction = map?.FindAction("Advance", throwIfNotFound: false);
                if (advanceAction == null) Debug.LogWarning("[Dialogue] Gameplay 动作表里没有 Advance，只能点击空白处推进", this);
                else map.Enable(); // PointerInput 也会启用同一张表，重复启用无副作用
            }
        }

        void OnDisable()
        {
            if (pointer != null)
            {
                pointer.TapEmpty -= HandleTapEmpty;
                pointer.Tap -= HandleTapNode;
            }
            advanceAction = null;
        }

        void Update()
        {
            if (advanceAction != null && advanceAction.WasPressedThisFrame()) subtitles.Advance();
        }

        void HandleTapEmpty()
        {
            // 阶段关掉指针输入时（例如变形中），点击也不推进
            if (pointer != null && !pointer.InputEnabled) return;
            subtitles.Advance();
        }

        void HandleTapNode(int id)
        {
            if (subtitles.blackout != null && subtitles.blackout.IsBlack) HandleTapEmpty();
        }
    }
}
