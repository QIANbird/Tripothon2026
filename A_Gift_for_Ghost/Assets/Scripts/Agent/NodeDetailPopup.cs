using Ghost.Interaction;
using UnityEngine;
using UnityEngine.UI;

namespace Ghost.Agent
{
    // 节点详情弹窗（World Space）。显示在被点节点的侧面，用一条细引线连回节点，不挡住节点本身。
    // 正对相机（只在位置变化时重算朝向，固定相机下完全不抖）；节点在画面右半边时自动翻到左侧。
    // 没有 Collider，不挡指针射线。
    public class NodeDetailPopup : MonoBehaviour
    {
        [Header("引用（AgentUIBuilder 会填好）")]
        public Camera viewCamera;
        public RectTransform panel;
        public RectTransform box;
        public Text titleLabel;
        public Text bodyLabel;
        public RectTransform leader;
        public RectTransform anchorDot;

        [Header("摆放（米）")]
        [Tooltip("弹窗离节点的横向距离（沿相机右方向）")]
        public float sideOffset = 0.1f;
        [Tooltip("往相机方向拉近（米），字更大，也不会和周围节点穿插")]
        public float towardCamera = 0.35f;
        [Tooltip("节点在画面右半边时翻到左侧")]
        public bool autoFlip = true;
        [Tooltip("只绕竖直轴转向相机；关掉时和相机画面平行（PC 固定相机推荐）")]
        public bool yawOnly = false;

        [Header("尺寸（像素，1000 px = 1 m）")]
        public float width = 520f;
        public float padding = 24f;
        public float titleHeight = 56f;
        public float titleBodyGap = 10f;

        [Header("行为")]
        [Tooltip("显示多少秒后自动隐藏，0 = 不自动隐藏")]
        public float autoHideSeconds = 0f;

        public bool Visible => panel != null && panel.gameObject.activeSelf;
        public int NodeId { get; private set; } = -1;

        NodePicker followPicker;
        Vector3 anchorWorld;
        Vector3 lastPlacedAnchor;
        Vector3 lastCameraPosition;
        bool flipped;
        float hideAt = -1f;

        void Awake()
        {
            if (panel == null || box == null || titleLabel == null || bodyLabel == null)
            {
                Debug.LogError("[Agent] NodeDetailPopup 缺少界面引用，请用 AgentUIBuilder 生成", this);
                enabled = false;
                return;
            }
            if (viewCamera == null) viewCamera = Camera.main;
            panel.gameObject.SetActive(false);
        }

        // 在世界坐标 worldPos（节点位置）旁边显示
        public void Show(Vector3 worldPos, string title, string body)
        {
            followPicker = null;
            NodeId = -1;
            Open(worldPos, title, body);
        }

        // 显示在节点 id 旁边，并在节点移动时（变形中）跟随。拿不到位置时返回 false
        public bool ShowAtNode(NodePicker picker, int nodeId, string title, string body)
        {
            if (picker == null || !picker.TryGetNodeWorldPosition(nodeId, out Vector3 pos)) return false;
            followPicker = picker;
            NodeId = nodeId;
            Open(pos, title, body);
            return true;
        }

        public void Hide()
        {
            followPicker = null;
            NodeId = -1;
            hideAt = -1f;
            if (panel != null) panel.gameObject.SetActive(false);
        }

        // 只改文字，不动位置（比如同一节点的进度在变）
        public void SetText(string title, string body)
        {
            titleLabel.text = title ?? "";
            bodyLabel.text = body ?? "";
            Resize();
        }

        void Open(Vector3 worldPos, string title, string body)
        {
            if (!enabled) return;
            anchorWorld = worldPos;
            panel.gameObject.SetActive(true);
            SetText(title, body);
            // 在一次显示内固定左右侧，节点跟随时不来回翻
            flipped = autoFlip && IsOnRightHalf(worldPos);
            Place(true);
            hideAt = autoHideSeconds > 0f ? Time.time + autoHideSeconds : -1f;
        }

        void LateUpdate()
        {
            if (!Visible) return;
            if (hideAt > 0f && Time.time >= hideAt)
            {
                Hide();
                return;
            }
            if (followPicker != null && followPicker.TryGetNodeWorldPosition(NodeId, out Vector3 pos)) anchorWorld = pos;
            Place(false);
        }

        static Quaternion YawRotation(Vector3 forward, Transform cam)
        {
            forward.y = 0f;
            if (forward.sqrMagnitude < 1e-6f) forward = cam.forward;
            return Quaternion.LookRotation(forward, Vector3.up);
        }

        bool IsOnRightHalf(Vector3 worldPos)
        {
            if (viewCamera == null) return false;
            return viewCamera.WorldToViewportPoint(worldPos).x > 0.5f;
        }

        void Place(bool force)
        {
            if (viewCamera == null) return;
            var cam = viewCamera.transform;
            if (!force && (anchorWorld - lastPlacedAnchor).sqrMagnitude < 1e-8f
                && (cam.position - lastCameraPosition).sqrMagnitude < 1e-8f) return;
            lastPlacedAnchor = anchorWorld;
            lastCameraPosition = cam.position;

            Vector3 toCamera = cam.position - anchorWorld;
            Vector3 right = Vector3.Cross(Vector3.up, -toCamera).normalized;
            if (right.sqrMagnitude < 1e-6f) right = cam.right;
            float side = flipped ? -1f : 1f;

            // 引线和锚点方块放在 Canvas 本地坐标里：节点在 pivot 侧外 sideOffset 处
            Vector3 position = anchorWorld + right * (sideOffset * side) + toCamera.normalized * towardCamera;
            panel.position = position;
            panel.rotation = yawOnly ? YawRotation(position - cam.position, cam)
                : AgentUIStyle.FacingRotation(position, cam);

            panel.pivot = new Vector2(flipped ? 1f : 0f, 0.5f);
            box.anchorMin = box.anchorMax = new Vector2(flipped ? 1f : 0f, 0.5f);
            box.pivot = new Vector2(flipped ? 1f : 0f, 0.5f);
            box.anchoredPosition = Vector2.zero;

            // 节点在 Canvas 平面上的投影（沿相机视线），引线端点在画面上正好落在节点上
            Vector3 onPlane = anchorWorld;
            Vector3 viewDir = anchorWorld - cam.position;
            float denom = Vector3.Dot(viewDir, panel.forward);
            if (Mathf.Abs(denom) > 1e-5f)
                onPlane = cam.position + viewDir * (Vector3.Dot(position - cam.position, panel.forward) / denom);
            Vector3 local = panel.InverseTransformPoint(onPlane);
            var nodeLocal = new Vector2(local.x, local.y);
            if (leader != null)
            {
                Vector2 start = Vector2.zero;
                Vector2 d = nodeLocal - start;
                leader.anchorMin = leader.anchorMax = panel.pivot;
                leader.pivot = new Vector2(0f, 0.5f);
                leader.anchoredPosition = start;
                leader.sizeDelta = new Vector2(d.magnitude, leader.sizeDelta.y);
                leader.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg);
            }
            if (anchorDot != null)
            {
                anchorDot.anchorMin = anchorDot.anchorMax = panel.pivot;
                anchorDot.pivot = new Vector2(0.5f, 0.5f);
                anchorDot.anchoredPosition = nodeLocal;
            }
        }

        // 高度随正文行数变化
        void Resize()
        {
            float inner = width - padding * 2f;
            var titleRect = titleLabel.rectTransform;
            titleRect.anchoredPosition = new Vector2(padding, -padding);
            titleRect.sizeDelta = new Vector2(inner, titleHeight);

            var bodyRect = bodyLabel.rectTransform;
            bodyRect.anchoredPosition = new Vector2(padding, -padding - titleHeight - titleBodyGap);
            bodyRect.sizeDelta = new Vector2(inner, 10f);
            float bodyHeight = string.IsNullOrEmpty(bodyLabel.text) ? 0f : bodyLabel.preferredHeight;
            bodyRect.sizeDelta = new Vector2(inner, bodyHeight);

            float height = padding * 2f + titleHeight + (bodyHeight > 0f ? titleBodyGap + bodyHeight : 0f);
            box.sizeDelta = new Vector2(width, height);
            panel.sizeDelta = new Vector2(width, height);
        }
    }
}
