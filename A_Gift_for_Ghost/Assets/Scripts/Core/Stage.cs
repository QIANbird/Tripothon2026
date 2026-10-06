using System;
using Ghost.Morph;
using UnityEngine;

namespace Ghost.Core
{
    // 所有阶段的基类。GameFlow 按顺序调用 Enter() / Exit()；
    // 阶段自己判断通关，满足条件时调用 Complete()，GameFlow 收到 Completed 后进入下一关。
    public abstract class Stage : MonoBehaviour
    {
        [Tooltip("阶段名，显示在调试信息里")]
        public string stageName = "Stage";
        [Tooltip("进入这一关时是否要变形")]
        public bool changesForm = true;
        [Tooltip("进入这一关时变形到的形态（changesForm 为 true 时才生效）")]
        public MorphForm form = MorphForm.Matrix;

        // 通关时发出，参数是阶段自己
        public event Action<Stage> Completed;

        // 当前是否是正在进行的阶段
        public bool IsActive { get; private set; }

        // 显示在阶段面板上的一行字，子类可以改写
        public virtual string PanelText => stageName;

        // 进入阶段：子类重写时先调用 base.Enter()，再做自己的初始化（生成问题、显示界面等）
        public virtual void Enter()
        {
            IsActive = true;
        }

        // 离开阶段：子类重写时先清理自己的东西，再调用 base.Exit()
        public virtual void Exit()
        {
            IsActive = false;
        }

        // 子类在满足通关条件时调用。不在进行中的阶段调用无效，避免重复通关
        protected void Complete()
        {
            if (!IsActive) return;
            Completed?.Invoke(this);
        }
    }
}
