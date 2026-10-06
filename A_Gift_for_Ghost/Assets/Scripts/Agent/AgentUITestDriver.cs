using Ghost.Interaction;
using UnityEngine;

namespace Ghost.Agent
{
    // 只用于 G4Test 场景：用假数据填任务面板，打开一次详情弹窗，弹出询问框（Yes 打印 "[G4] Yes"）。
    // 右键按住节点显示该节点的详情，松开后停留再淡出（正式阶段由 StageContext 统一处理）。
    public class AgentUITestDriver : MonoBehaviour
    {
        public AgentTaskPanel taskPanel;
        public NodeDetailPopup detailPopup;
        public AgentQueryDialog query;
        public PointerInput pointer;
        public NodePicker picker;

        [Tooltip("开局为这个节点打开一次详情弹窗（随后按 lingerSeconds 淡出）")]
        public int popupNode = 35;

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
            detailPopup.Show(popupNode, $"节点 #{popupNode}", body);
            detailPopup.Release();

            AskQuery();
            if (pointer != null)
            {
                pointer.InspectStart += OnInspect;
                pointer.InspectEnd += detailPopup.Release;
            }
        }

        void OnDestroy()
        {
            if (pointer == null) return;
            pointer.InspectStart -= OnInspect;
            pointer.InspectEnd -= detailPopup.Release;
        }

        void OnInspect(int id)
        {
            string organ = picker != null ? picker.OrganOf(id)?.ToString() : "?";
            detailPopup.Show(id, $"节点 #{id} · {organ}", "状态  PENDING\n进度  0 / 1\n置信度  87%");
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
