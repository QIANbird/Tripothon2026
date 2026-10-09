using Ghost.Core;
using Ghost.Interaction;
using Ghost.Morph;
using Ghost.Pick;
using Ghost.Player;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;

namespace Ghost.Stages
{
    // Pick 阶段（形态 Real）：写实植株按真实尺寸固定在玩家前方，不能转；玩家第一人称走过去、蹲下、对准果实摘下，按 E 举到嘴边一口一口吃。
    // 这是唯一移动相机的阶段：PlayerRig 接管主相机，离开时还回固定机位（docs/CURRENT_STATE.md 不变量"相机永远不动"的例外）。
    // 当前完成第 1–5a 步（移动、固定植株、XRI 摘、举到嘴边、咬一口缩小）。5b/5c 的虚幻化和消散订阅 EatSequence.BiteTaken。
    //
    // 跳关可逆：
    //   Enter：ResetShared，关指针输入和旋转，PlantFit 停止适配、缓动到真实尺寸摆放，打开场地碰撞和玩家，给果实加 Interactable；
    //   Exit：关玩家（相机回固定机位、光标解锁），关场地，果实还原到植株上，手回待机，PlantFit 恢复适配，指针输入还原，ResetShared。
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

        [Header("摘")]
        [Tooltip("写实模型里可摘果实的子物体名")]
        public string fruitObjectName = "pepper_red_picked";
        public HandReach hand;
        public EatSequence eat;
        [Tooltip("果实的 Interactable 注册到这里（交互发起方：头上的 XRRayInteractor，PC 从屏幕中心射出）")]
        public XRInteractionManager interactionManager;
        [Tooltip("伸手可及的距离（米，从眼睛到果实中心）。范围外果实的 Interactable 关闭，不高亮也摘不了")]
        public float reachDistance = 0.7f;

        [TextArea] public string panelText = "采摘：对准果实按左键摘下，按 E 举到嘴边再咬。WASD 移动，C / Ctrl 蹲下";
        public override string PanelText => panelText;

        bool savedPointerEnabled = true;
        PickableFruit fruit;
        RealModelHandoff handoff;
        bool fruitReady;

        public override void Enter()
        {
            base.Enter();
            if (ctx == null || plantFit == null || player == null || area == null || plantAnchor == null)
            {
                Debug.LogError($"[Stage] {stageName} 缺少 ctx / plantFit / player / area / plantAnchor", this);
                return;
            }
            ctx.ResetShared();

            // 交互发起方换成第一人称 XRI，鼠标点击不再拾取节点
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
            if (hand != null) hand.ResetPose();
            if (eat != null)
            {
                eat.ResetState();
                eat.BiteTaken += OnBiteTaken;
            }
            player.Activate();
            fruitReady = false;
        }

        public override void Exit()
        {
            if (eat != null) eat.BiteTaken -= OnBiteTaken;
            RestoreFruit();
            if (hand != null) hand.ResetPose();
            if (eat != null) eat.ResetState();
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

        void Update()
        {
            if (!IsActive) return;
            if (!fruitReady) TryBindFruit();
            UpdatePickable();
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

        void TryBindFruit()
        {
            // 再次进关：果实已经找过，只重新订阅（Exit 时退订了）
            if (fruit != null)
            {
                fruit.PickRequested -= OnPickRequested;
                fruit.PickRequested += OnPickRequested;
                fruitReady = true;
                return;
            }
            if (handoff == null && ctx != null && ctx.morpher != null)
                handoff = ctx.morpher.GetComponent<RealModelHandoff>();
            if (handoff == null || handoff.ModelInstance == null) return;

            var t = FindChild(handoff.ModelInstance.transform, fruitObjectName);
            if (t == null)
            {
                Debug.LogError($"[Pick] 写实模型里找不到果实 '{fruitObjectName}'", this);
                fruitReady = true;
                return;
            }
            fruit = t.GetComponent<PickableFruit>();
            if (fruit == null) fruit = t.gameObject.AddComponent<PickableFruit>();
            fruit.Init(interactionManager);
            fruit.PickRequested += OnPickRequested;
            fruitReady = true;
        }

        void UpdatePickable()
        {
            bool inReach = false;
            if (fruit != null && !fruit.IsDetached && (hand == null || !hand.IsPicking) && player != null && player.motor != null && player.motor.cameraPivot != null)
            {
                float d = Vector3.Distance(player.motor.cameraPivot.position, fruit.WorldCenter);
                inReach = d <= reachDistance;
            }
            if (fruit != null) fruit.SetPickable(inReach);
        }

        void OnPickRequested(PickableFruit f)
        {
            if (!IsActive || hand == null) return;
            hand.ReachAndPick(f);
        }

        void OnBiteTaken(int index, int total)
        {
            // 5a：只缩小。5b/5c 订阅 BiteTaken 做虚幻化和碎片
        }

        void RestoreFruit()
        {
            if (fruit == null) return;
            fruit.PickRequested -= OnPickRequested;
            fruit.Restore();
        }

        static Transform FindChild(Transform root, string name)
        {
            if (root.name == name) return root;
            for (int i = 0; i < root.childCount; i++)
            {
                var found = FindChild(root.GetChild(i), name);
                if (found != null) return found;
            }
            return null;
        }
    }
}
