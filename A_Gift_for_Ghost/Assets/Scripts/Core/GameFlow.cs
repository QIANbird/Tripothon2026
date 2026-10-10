using System;
using Ghost.Morph;
using UnityEngine;

namespace Ghost.Core
{
    // 阶段状态机：按顺序运行 stages 列表。
    // 当前阶段 Complete() 后自动进入下一关；进入新阶段时，Enter() 和变形同时开始（变形本身就是进入下一关的标志）。
    public class GameFlow : MonoBehaviour
    {
        [Tooltip("按游戏顺序排列的阶段")]
        public Stage[] stages;
        [Tooltip("植株的 NodeMorpher；阶段需要变形时调用它")]
        public NodeMorpher morpher;
        [Tooltip("勾上时 Play 直接进入第 0 关；不勾时等开始界面调用 Begin()")]
        public bool autoStart;

        // 进入 / 离开某个阶段时发出，供音频、界面等模块订阅
        public event Action<Stage> StageEntered;
        public event Action<Stage> StageExited;
        // 最后一关结束（Complete 或在最后一关调 Next）时发出一次，结束界面订阅它
        public event Action FlowFinished;

        public int CurrentIndex { get; private set; } = -1;
        public bool HasStarted => CurrentIndex >= 0;
        public bool IsFinished { get; private set; }
        public Stage CurrentStage => CurrentIndex >= 0 && CurrentIndex < stages.Length ? stages[CurrentIndex] : null;

        void Awake()
        {
            foreach (var stage in stages)
                if (stage != null) stage.Completed += OnStageCompleted;
        }

        void OnDestroy()
        {
            foreach (var stage in stages)
                if (stage != null) stage.Completed -= OnStageCompleted;
        }

        void Start()
        {
            if (stages == null || stages.Length == 0)
            {
                Debug.LogError("[Flow] GameFlow 没有配置阶段", this);
                return;
            }
            if (autoStart) Begin();
        }

        // 开始界面点"开始"时调用：进入第 0 关。已经开始过就忽略
        public void Begin()
        {
            if (HasStarted || stages == null || stages.Length == 0) return;
            EnterStage(0);
        }

        // 进入下一关；已经是最后一关时不做任何事
        public void Next()
        {
            if (!HasStarted)
            {
                Begin();
                return;
            }
            if (CurrentIndex + 1 >= stages.Length)
            {
                Finish();
                return;
            }
            JumpTo(CurrentIndex + 1);
        }

        // 跳到指定下标的阶段（调试跳关也用它）
        public void JumpTo(int index)
        {
            if (index < 0 || index >= stages.Length)
            {
                Debug.LogWarning($"[Flow] 没有下标为 {index} 的阶段", this);
                return;
            }

            if (IsFinished) return; // 结束界面出现后只能重新开始，不再跳关
            var current = CurrentStage;
            if (current != null)
            {
                current.Exit();
                StageExited?.Invoke(current);
            }
            EnterStage(index);
        }

        // 流程结束：退出最后一关，通知结束界面。只发一次
        void Finish()
        {
            if (IsFinished) return;
            IsFinished = true;
            var current = CurrentStage;
            if (current != null)
            {
                current.Exit();
                StageExited?.Invoke(current);
            }
            Debug.Log("[Flow] 流程结束", this);
            FlowFinished?.Invoke();
        }

        void EnterStage(int index)
        {
            CurrentIndex = index;
            var stage = stages[index];

            // 变形和 Enter() 在同一帧开始。跳关时目标阶段可能不变形，就用它之前最近一次变形的形态
            if (morpher != null) morpher.MorphTo(FormAt(index));

            stage.Enter();
            Debug.Log($"[Flow] 第 {index} 关：{stage.stageName}", this);
            StageEntered?.Invoke(stage);
        }

        // 某一关应该处于的形态：往前找最近一个会变形的阶段；都不变形时用植株的初始形态
        public MorphForm FormAt(int index)
        {
            for (int i = index; i >= 0; i--)
                if (stages[i] != null && stages[i].changesForm) return stages[i].form;
            return morpher != null ? morpher.startForm : MorphForm.Matrix;
        }

        void OnStageCompleted(Stage stage)
        {
            // 只认当前阶段的通关
            if (stage != CurrentStage) return;
            Next();
        }
    }
}
