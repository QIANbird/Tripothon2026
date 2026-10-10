using System.Collections.Generic;
using Ghost.Morph;
using UnityEngine;
using UnityEngine.Rendering;

namespace Ghost.Stages
{
    // S4：在虫子节点的位置摆真实的虫子模型（Tripo 生成的 bug FBX），让几何植株上看到的是"真虫子"。
    // 实例是 NodeMorpher 物体的子物体（TargetRotator 转动植株时一起转），每帧按节点当前姿态摆放：
    //   - 位置 / 朝向 = 节点的 CurrentPoses（虫背 = 节点的 up，长轴 = 节点的 forward）；
    //   - 大小 = lengthMeters × 节点显示缩放（G3 Hide 的缩小动画会带着模型一起缩没）；
    //   - 变形中不显示，变形结束后 growTime 秒内长出来。
    // 只改显示，不参与拾取（拾取仍按节点包围球）。不改 Morph 代码，也不改模型资产。
    // 以后换成 pepper_plant_bugs 节点集（虫子模型直接在彩椒 Variant 里）时，这个组件就不需要了，见 PROJECT_SUMMARY G9。
    public class BugModelInstances : MonoBehaviour
    {
        public NodeMorpher morpher;
        [Tooltip("虫子模型（FBX 资产）")]
        public GameObject bugPrefab;
        [Tooltip("虫子身长（米），真实尺寸约 1–1.5 cm，放大到约 4 cm 让 2 m 外看得见、点得到")]
        public float lengthMeters = 0.04f;
        [Tooltip("变形结束后长出来的时长（秒）")]
        public float growTime = 0.4f;
        [Tooltip("模型本身的朝向修正：Tripo 虫子的长轴是 X、背朝 +Y，转成节点的长轴 Z")]
        public Vector3 modelEuler = new Vector3(0f, 90f, 0f);

        class Instance
        {
            public int nodeId;
            public Transform transform;
            public float baseScale;
        }

        readonly List<Instance> instances = new List<Instance>();
        Transform root;
        float grow;

        public int Count => instances.Count;
        public bool HasInstance(int nodeId) => instances.Exists(x => x.nodeId == nodeId);

        public Transform InstanceOf(int nodeId)
        {
            var inst = instances.Find(x => x.nodeId == nodeId);
            return inst != null ? inst.transform : null;
        }

        // 在这些节点上生成模型（先清掉旧的）
        public void Spawn(IEnumerable<int> nodeIds)
        {
            Clear();
            if (morpher == null || bugPrefab == null) return;

            // 模型本身的尺寸：按包围盒最长边换算缩放
            float modelLength = MeasureLength(bugPrefab);
            if (modelLength <= 1e-5f) modelLength = 1f;

            root = new GameObject("BugModels").transform;
            root.SetParent(morpher.transform, false);
            foreach (int id in nodeIds)
            {
                var go = Instantiate(bugPrefab, root);
                go.name = $"bug_{id}";
                foreach (var r in go.GetComponentsInChildren<Renderer>())
                    r.shadowCastingMode = ShadowCastingMode.Off;
                // 不挡 PointerInput 的 Physics 射线
                foreach (var c in go.GetComponentsInChildren<Collider>()) Destroy(c);
                go.transform.localScale = Vector3.zero;
                instances.Add(new Instance { nodeId = id, transform = go.transform, baseScale = lengthMeters / modelLength });
            }
            grow = 0f;
            LateUpdate();
        }

        public void Clear()
        {
            instances.Clear();
            if (root != null) Destroy(root.gameObject);
            root = null;
        }

        void OnDisable()
        {
            Clear();
        }

        void LateUpdate()
        {
            if (instances.Count == 0 || morpher == null || !morpher.IsReady) return;
            var poses = morpher.CurrentPoses;
            if (poses == null) return;

            // 变形中缩没，变形结束后再长出来（跟着节点飞的模型看起来很乱）
            float step = growTime > 0f ? Time.deltaTime / growTime : 1f;
            grow = Mathf.MoveTowards(grow, morpher.IsMorphing ? 0f : 1f, step);
            float g = grow * grow * (3f - 2f * grow);
            Quaternion fix = Quaternion.Euler(modelEuler);

            foreach (var inst in instances)
            {
                if (inst.nodeId < 0 || inst.nodeId >= poses.Length) continue;
                var pose = poses[inst.nodeId];
                float k = g * morpher.GetNodeVisibility(inst.nodeId) * inst.baseScale;
                inst.transform.localPosition = pose.position;
                inst.transform.localRotation = pose.rotation * fix;
                inst.transform.localScale = Vector3.one * k;
                bool visible = k > 1e-5f;
                if (inst.transform.gameObject.activeSelf != visible) inst.transform.gameObject.SetActive(visible);
            }
        }

        static float MeasureLength(GameObject prefab)
        {
            var bounds = new Bounds();
            bool any = false;
            foreach (var mf in prefab.GetComponentsInChildren<MeshFilter>())
            {
                if (mf.sharedMesh == null) continue;
                // 网格包围盒换算到预制体根空间
                var m = prefab.transform.worldToLocalMatrix * mf.transform.localToWorldMatrix;
                var b = mf.sharedMesh.bounds;
                for (int i = 0; i < 8; i++)
                {
                    Vector3 corner = b.center + Vector3.Scale(b.extents, new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
                    Vector3 p = m.MultiplyPoint3x4(corner);
                    if (!any) { bounds = new Bounds(p, Vector3.zero); any = true; }
                    else bounds.Encapsulate(p);
                }
            }
            return any ? Mathf.Max(bounds.size.x, Mathf.Max(bounds.size.y, bounds.size.z)) : 0f;
        }
    }
}
