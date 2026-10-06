using Ghost.Morph;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Ghost.Gameplay
{
    // G3 验收用：开局在测试场景里挂三种难度的问题，用 UI 动作表的 Point / Click 做最简单的节点拾取，
    // 并在 Console 打印问题事件。正式的拾取由 G2 的 NodePicker / PointerInput 负责，这个脚本不进正式场景。
    public class G3TestDriver : MonoBehaviour
    {
        public NodeMorpher morpher;
        public NodeIssueSystem issues;
        public NodeLinkRenderer links;
        [Tooltip("InputSystem_Actions.inputactions 资产")]
        public InputActionAsset actions;

        [Header("开局生成的问题")]
        public int easyCount = 3;
        public int mediumCount = 2;
        [Tooltip("困难问题放在所有果实节点上")]
        public bool hardOnFruits = true;
        public int seed = 3;

        [Header("拾取")]
        [Tooltip("节点包围球的最小半径（米），太小的节点不好点")]
        public float minPickRadius = 0.015f;

        public Color linkTestColor = new Color(0.25f, 0.55f, 1f);

        InputAction point;
        InputAction click;
        Camera cam;

        void Start()
        {
            cam = Camera.main;
            if (actions != null)
            {
                var ui = actions.FindActionMap("UI", throwIfNotFound: true);
                point = ui.FindAction("Point", throwIfNotFound: true);
                click = ui.FindAction("Click", throwIfNotFound: true);
                click.performed += OnClick;
                ui.Enable();
            }

            issues.IssueAttempted += (issue, solved) =>
                Debug.Log($"[G3Test] IssueAttempted node {issue.nodeId} ({issue.difficulty}) solved={solved} attempts={issue.attempts} remaining={issue.clicksRemaining}");
            issues.IssueResolved += issue =>
                Debug.Log($"[G3Test] IssueResolved node {issue.nodeId} ({issue.difficulty})");

            SetupIssues();
        }

        void OnDestroy()
        {
            if (click != null) click.performed -= OnClick;
        }

        public void SetupIssues()
        {
            issues.ClearAll();
            issues.GenerateRandom(easyCount, IssueDifficulty.Easy, n => n.organ == Organ.Leaf, seed);
            issues.GenerateRandom(mediumCount, IssueDifficulty.Medium, n => n.organ == Organ.Leaf || n.organ == Organ.Stem, seed + 1);
            if (hardOnFruits)
            {
                var fruits = new System.Collections.Generic.List<int>();
                foreach (var node in morpher.nodeSet.nodes)
                    if (node.organ == Organ.Fruit) fruits.Add(node.id);
                issues.AddIssues(fruits, IssueDifficulty.Hard);
            }
            Debug.Log($"[G3Test] 生成问题：{issues.Issues.Count} 个（Easy {easyCount}，Medium {mediumCount}，Hard = 果实节点）");
        }

        void OnClick(InputAction.CallbackContext context)
        {
            if (cam == null || point == null) return;
            int id = Pick(point.ReadValue<Vector2>());
            if (id < 0) return;
            SimulateClick(id);
        }

        // 模拟点击某个节点：有问题就尝试，没问题就高亮它和它父节点之间的连线（顺便验收连线染色）
        public void SimulateClick(int id)
        {
            var node = morpher.nodeSet.Get(id);
            if (node == null) return;
            if (issues.TryAttempt(id)) return;

            Debug.Log($"[G3Test] 点击无问题节点 {id}（{node.organ}，part={node.part}）");
            if (links != null)
            {
                if (links.IsLinkColored(id)) links.ClearLinkColor(id);
                else links.SetLinkColor(id, linkTestColor);
            }
        }

        // 最简单的射线 × 包围球拾取，取最近的命中
        int Pick(Vector2 screen)
        {
            Ray ray = cam.ScreenPointToRay(screen);
            int best = -1;
            float bestT = float.MaxValue;
            for (int i = 0; i < morpher.nodeSet.Count; i++)
            {
                if (morpher.GetNodeVisibility(i) < 0.5f) continue;
                if (!morpher.TryGetNodeWorldSphere(i, out var center, out var radius, minPickRadius)) continue;
                Vector3 oc = center - ray.origin;
                float t = Vector3.Dot(oc, ray.direction);
                if (t < 0f) continue;
                float d2 = oc.sqrMagnitude - t * t;
                if (d2 > radius * radius) continue;
                if (t < bestT)
                {
                    bestT = t;
                    best = i;
                }
            }
            return best;
        }

        // 调试键（编辑器里用，不走 Action，便于不改 inputactions）：Inspector 右键菜单触发
        [ContextMenu("Hide random leaf")]
        void HideRandomLeaf()
        {
            for (int i = 0; i < morpher.nodeSet.Count; i++)
            {
                if (morpher.nodeSet.nodes[i].organ == Organ.Leaf && !morpher.IsHidden(i))
                {
                    morpher.Hide(i);
                    return;
                }
            }
        }

        [ContextMenu("Show all")]
        void ShowAll()
        {
            for (int i = 0; i < morpher.nodeSet.Count; i++) morpher.Show(i);
        }

        [ContextMenu("Realness +0.25")]
        void RealnessUp()
        {
            morpher.Realness = Mathf.Min(1f, morpher.Realness + 0.25f);
        }

        [ContextMenu("Clear Realness")]
        void RealnessClear()
        {
            morpher.ClearRealness();
        }
    }
}
