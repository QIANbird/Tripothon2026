using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Ghost.Pick
{
    // 吃的流程。订阅 Eat（E）：第一次把手举到嘴边，之后每次 Bite() 咬一口。
    // Bite() 是公开方法，不读输入，以后正式动画可以通过 Animation Event 调用。
    // 5a 只做节奏：缩小 + BiteTaken 事件。虚幻化和碎片由 5b/5c 订阅 BiteTaken。
    public class EatSequence : MonoBehaviour
    {
        public HandReach hand;
        [Tooltip("InputSystem_Actions.inputactions 资产")]
        public InputActionAsset actions;
        public string mapName = "Player";
        public string eatActionName = "Eat";

        [Header("节奏")]
        [Tooltip("一共几口")]
        public int biteCount = 3;
        [Tooltip("两口之间的最短间隔（秒）")]
        public float biteCooldown = 0.5f;
        [Tooltip("第 k 口后的缩放 = lerp(1, leftover, k / N)")]
        [Range(0f, 1f)] public float leftoverScale = 0.35f;

        // 咬了一口：(index 从 1 起, 一共几口)
        public event Action<int, int> BiteTaken;
        public event Action Finished;

        public int BitesTaken { get; private set; }
        public bool Raised { get; private set; }
        public bool IsFinished { get; private set; }
        public bool CanEat => hand != null && hand.HeldFruit != null && !IsFinished;

        InputAction eatAction;
        float lastBiteTime = -999f;

        void OnEnable()
        {
            if (actions == null)
            {
                Debug.LogError("[Pick] EatSequence 缺少 actions", this);
                enabled = false;
                return;
            }
            var map = actions.FindActionMap(mapName, throwIfNotFound: true);
            eatAction = map.FindAction(eatActionName, throwIfNotFound: true);
            // FirstPersonMotor 已经 Enable 过 Player 表，这里只订阅
            eatAction.performed += OnEat;
        }

        void OnDisable()
        {
            if (eatAction != null) eatAction.performed -= OnEat;
            eatAction = null;
        }

        void OnEat(InputAction.CallbackContext ctx)
        {
            if (!ctx.performed) return;
            TryEat();
        }

        // 没拿果实时无反应；第一次举到嘴边，之后咬一口
        public void TryEat()
        {
            if (!CanEat) return;
            if (!Raised)
            {
                if (hand.IsMoving) return;
                Raised = true;
                hand.GoMouth();
                return;
            }
            Bite();
        }

        // 咬一口。举到嘴边之后才能咬；正在举 / 在冷却中时忽略。不读输入
        public void Bite()
        {
            if (!CanEat || !Raised) return;
            if (hand.IsMoving) return;
            if (Time.time - lastBiteTime < biteCooldown) return;

            lastBiteTime = Time.time;
            BitesTaken++;
            int n = Mathf.Max(1, biteCount);
            hand.NudgeBite();
            hand.HeldFruit.SetScaleFactor(Mathf.Lerp(1f, leftoverScale, (float)BitesTaken / n));
            BiteTaken?.Invoke(BitesTaken, n);

            if (BitesTaken >= n)
            {
                IsFinished = true;
                Finished?.Invoke();
            }
        }

        public void ResetState()
        {
            BitesTaken = 0;
            Raised = false;
            IsFinished = false;
            lastBiteTime = -999f;
        }
    }
}
