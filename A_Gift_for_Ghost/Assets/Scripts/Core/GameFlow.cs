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

        // 进入 / 离开某个阶段时发出，供音频、界面等模块订阅
        public event Action<Stage> StageEntered;
        public event Action<Stage> StageExited;

        public int CurrentIndex { get; private set; } = -1;
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
            EnterStage(0);
        }

        // 进入下一关；已经是最后一关时不做任何事
        public void Next()
        {
            if (CurrentIndex + 1 >= stages.Length)
            {
                Debug.Log("[Flow] 已经是最后一关", this);
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

            var current = CurrentStage;
            if (current != null)
            {
                current.Exit();
                StageExited?.Invoke(current);
            }
            EnterStage(index);
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
