using Ghost.Core;
using Ghost.Interaction;
using Ghost.Morph;
using Ghost.Player;
using UnityEngine;

namespace Ghost.Stages
{
    // Pick 阶段（形态 Real）：写实植株按真实尺寸固定在玩家前方，不能转；玩家第一人称走过去。
    // 这是唯一移动相机的阶段：PlayerRig 接管主相机，离开时还回固定机位（docs/CURRENT_STATE.md 不变量"相机永远不动"的例外）。
    // 当前完成第 1–3 阶段（移动 + 固定植株），摘果实 / 吃在后续阶段加入；暂时按 N 继续。
    //
    // 跳关可逆：
    //   Enter：ResetShared，关指针输入和旋转，PlantFit 停止适配、缓动到真实尺寸摆放，打开场地碰撞和玩家；
    //   Exit：关玩家（相机回固定机位、光标解锁），关场地，PlantFit 恢复适配，指针输入还原，ResetShared。
    public class PickStage : Stage
    {
        public StageContext ctx;
        public PlantFit plantFit;
        public TargetRotator rotator;
        public PlayerRig player;
        [Tooltip("地面、边界、底座和植株挡板。只在本阶段启用")]
        public GameObject area;
        [Tooltip("植株底部中心放在这里（底座顶面）")]
        public Transform plantAnchor;

        [Header("植株摆放")]
        [Tooltip("植株缩放（1 = 节点集的真实尺寸，pepper_plant 约 0.7 m 高）")]
        public float plantScale = 1f;
        [Tooltip("植株从适配姿态移到真实尺寸的时长（秒）。和进关变形同时进行")]
        public float placeSeconds = 1.6f;
        [Tooltip("离开时植株缓动回适配姿态的时长（秒）")]
        public float releaseSeconds = 0.6f;

        [TextArea] public string panelText = "采摘：WASD 移动，鼠标转头，C / Ctrl 蹲下。按 N 继续";
        public override string PanelText => panelText;

        bool savedPointerEnabled = true;

        public override void Enter()
        {
            base.Enter();
            if (ctx == null || plantFit == null || player == null || area == null || plantAnchor == null)
            {
                Debug.LogError($"[Stage] {stageName} 缺少 ctx / plantFit / player / area / plantAnchor", this);
                return;
            }
            ctx.ResetShared();

            // 交互发起方换成第一人称（第 4 阶段接 XRI），鼠标点击不再拾取节点
            if (ctx.pointer != null)
            {
                savedPointerEnabled = ctx.pointer.InputEnabled;
                ctx.pointer.InputEnabled = false;
            }
            if (rotator != null)
            {
                rotator.Disable();
                rotator.ResetRotation(false);
            }

            area.SetActive(true);
            PlacePlant();
            player.Activate();
        }

        public override void Exit()
        {
            if (player != null) player.Deactivate();
            if (area != null) area.SetActive(false);
            if (plantFit != null) plantFit.Release(releaseSeconds);
            if (ctx != null)
            {
                if (ctx.pointer != null) ctx.pointer.InputEnabled = savedPointerEnabled;
                ctx.ResetShared();
            }
            base.Exit();
        }

        // 植株底部中心对齐到 plantAnchor：PlantFit 在场景根，本地坐标 = 世界坐标
        void PlacePlant()
        {
            plantFit.GetFormBounds(MorphForm.Real, out Vector3 min, out Vector3 max);
            Vector3 baseCenter = new Vector3((min.x + max.x) * 0.5f, min.y, (min.z + max.z) * 0.5f);
            Vector3 target = plantAnchor.position - baseCenter * plantScale;
            Transform parent = plantFit.transform.parent;
            if (parent != null) target = parent.InverseTransformPoint(target);
            plantFit.Hold(target, plantScale, placeSeconds);
        }
    }
}
