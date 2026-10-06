using UnityEngine;
using UnityEngine.UI;

namespace Ghost.Core
{
    // 场景里的 World Space 面板，进入每一关时显示该关的 PanelText。
    // 用 UGUI 的 Text + 动态字体：缺字时会用系统字体补字，中文不需要额外的字体资源。
    public class StagePanel : MonoBehaviour
    {
        public GameFlow flow;
        public Text label;

        void OnEnable()
        {
            if (flow == null || label == null)
            {
                Debug.LogError("[Flow] StagePanel 缺少 flow 或 label", this);
                enabled = false;
                return;
            }
            flow.StageEntered += Show;
            if (flow.CurrentStage != null) Show(flow.CurrentStage);
        }

        void OnDisable()
        {
            if (flow != null) flow.StageEntered -= Show;
        }

        void Show(Stage stage)
        {
            label.text = stage.PanelText;
        }
    }
}
