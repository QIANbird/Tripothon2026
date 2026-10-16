using System.Collections.Generic;
using Ghost.Morph;
using PrimeTween;
using UnityEngine;

namespace Ghost.Stages
{
    // 临时调试开关（docs/tasks/s1-s2-missing-nodes.md 第 1 步验收，第 2–4 步做完后删除）。
    // Play 时挂在 GameFlow 物体上（和 StageContext 同一物体），最好先 Shift+2 跳到 S2（Circuit 形态）：
    //   showGaps   —— 4 个缺失节点的连线断开，缺口槽位画呼吸外发光
    //   scatter    —— 4 个节点用姿态覆盖飘到散落位置（PrimeTween 缓动）
    //   growToward —— 缺口两侧的连线重新朝缺口长出来（模拟 S2 拖近正确缺口）
    // 只读 Inspector 勾选，不读键盘 / 鼠标。
    [RequireComponent(typeof(StageContext))]
    public class MissingNodeDebug : MonoBehaviour
    {
        public bool showGaps;
        public bool scatter;
        public bool growToward;
        [Tooltip("可选覆盖（Leaf / Stem / Fruit / Root 顺序），< 0 = 自动")]
        public List<int> overrides = new List<int>();
        public Color gapGlowColor = new Color(0.35f, 0.7f, 1f, 0.9f);
        public float gapGlowFrequency = 1.2f;
        public float scatterSeconds = 0.8f;
        [Tooltip("连线生长速度（每秒）")]
        public float growSpeed = 2.5f;

        StageContext ctx;
        List<int> missing;
        NodePose[] scatterPoses;
        bool appliedGaps, appliedScatter, appliedGrow;
        Tween scatterTween;

        void Awake()
        {
            ctx = GetComponent<StageContext>();
        }

        void Update()
        {
            if (ctx == null || ctx.morpher == null || ctx.links == null || !ctx.morpher.IsReady) return;
            if (missing == null)
            {
                missing = MissingNodePicker.Pick(ctx.morpher.nodeSet, ctx.links.HasLink, overrides);
                scatterPoses = MissingNodePicker.ScatterPoses(ctx.morpher.nodeSet, missing);
            }

            if (showGaps != appliedGaps) ApplyGaps(showGaps);
            if (scatter != appliedScatter) ApplyScatter(scatter);
            if (growToward != appliedGrow && appliedGaps)
            {
                appliedGrow = growToward;
                foreach (int id in missing) ctx.links.SetNodeLinksGrowth(id, growToward ? 1f : 0f, growSpeed);
            }
        }

        void ApplyGaps(bool on)
        {
            appliedGaps = on;
            appliedGrow = false;
            growToward = false;
            foreach (int id in missing)
            {
                ctx.links.SetNodeLinksGrowth(id, on ? 0f : 1f);
                if (ctx.halos == null) continue;
                if (on)
                    ctx.halos.SetGlowAt(id, ctx.morpher.nodeSet.Get(id).GetPose(MorphForm.Circuit),
                        gapGlowColor, 1f, gapGlowFrequency);
                else ctx.halos.ClearAt(id);
            }
        }

        void ApplyScatter(bool on)
        {
            appliedScatter = on;
            scatterTween.Stop();
            float from = on ? 0f : 1f, to = on ? 1f : 0f;
            scatterTween = Tween.Custom(this, from, to, scatterSeconds, (self, w) =>
            {
                for (int i = 0; i < self.missing.Count; i++)
                    self.ctx.morpher.SetPoseOverride(self.missing[i], self.scatterPoses[i], w);
            }, Ease.InOutSine).OnComplete(this, self =>
            {
                if (self.appliedScatter) return;
                foreach (int id in self.missing) self.ctx.morpher.ClearPoseOverride(id);
            });
        }

        void OnDisable()
        {
            scatterTween.Stop();
            if (ctx == null || missing == null) return;
            foreach (int id in missing)
            {
                if (ctx.morpher != null) ctx.morpher.ClearPoseOverride(id);
                if (ctx.links != null) ctx.links.SetNodeLinksGrowth(id, 1f);
                if (ctx.halos != null) ctx.halos.ClearAt(id);
            }
            appliedGaps = appliedScatter = appliedGrow = false;
            showGaps = scatter = growToward = false;
        }
    }
}
