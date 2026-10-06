using Ghost.Interaction;
using UnityEngine;

namespace Ghost.Agent
{
    // 只用于 G4Test 场景：用假数据填任务面板，在固定节点旁打开详情弹窗，弹出询问框（Yes 打印 "[G4] Yes"）。
    // 点击节点时把弹窗移到被点的节点旁边（这也是 G6 接 G2 Tap 的写法示例）。
    public class AgentUITestDriver : MonoBehaviour
    {
        public AgentTaskPanel taskPanel;
        public NodeDetailPopup detailPopup;
        public AgentQueryDialog query;
        public PointerInput pointer;
        public NodePicker picker;

        [Tooltip("开局在这个节点旁打开详情弹窗；节点不存在时用 fallbackPosition")]
        public int popupNode = 35;
        public Vector3 fallbackPosition = new Vector3(0.1f, 1.1f, 0f);

        public string question = "已达到当前权限下的处理上限。是否要进一步查看信息？";

        void Start()
        {
            taskPanel.SetTitle("S1 · 植株维护");
            taskPanel.SetModeLabel("FULL PROXY");
            taskPanel.SetTasks(new[]
            {
                new AgentTask("叶片异常 ×6", TaskState.Done),
                new AgentTask("茎部异常 ×3", TaskState.Running),
                new AgentTask("果实严重异常 ×4", TaskState.Failed),
                new AgentTask("生成报告", TaskState.Pending),
            });
            taskPanel.SetMetric("完成率", 0.62f);
            taskPanel.SetMetric("置信度", 0.38f);
            taskPanel.SetMetric("处理进度", 0.7f, "7 / 10");

            const string body = "状态  RUNNING\n进度  2 / 3\n置信度  41%";
            if (picker == null || !detailPopup.ShowAtNode(picker, popupNode, $"节点 #{popupNode}", body))
                detailPopup.Show(fallbackPosition, "节点", body);

            AskQuery();
            if (pointer != null) pointer.Tap += OnTap;
        }

        void OnDestroy()
        {
            if (pointer != null) pointer.Tap -= OnTap;
        }

        void OnTap(int id)
        {
            string organ = picker != null ? picker.OrganOf(id)?.ToString() : "?";
            detailPopup.ShowAtNode(picker, id, $"节点 #{id} · {organ}", "状态  PENDING\n进度  0 / 1\n置信度  87%");
        }

        [ContextMenu("Ask Query")]
        public void AskQuery()
        {
            query.Ask(question, () =>
            {
                Debug.Log("[G4] Yes");
                taskPanel.UpdateTask(3, TaskState.Running);
            });
        }

        [ContextMenu("Advance Tasks")]
        public void AdvanceTasks()
        {
            taskPanel.UpdateTask(1, TaskState.Done);
            taskPanel.SetMetric("完成率", 0.85f);
        }

        [ContextMenu("Switch To Assistive")]
        public void SwitchToAssistive() => taskPanel.SetModeLabel("ASSISTIVE");
    }
}
