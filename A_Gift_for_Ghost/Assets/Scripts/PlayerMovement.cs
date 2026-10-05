using UnityEngine;
using UnityEngine.InputSystem; // 新输入系统（本项目只启用了新输入系统）

// 把这个脚本挂到胶囊体上，按 WASD 就能在地面上移动
public class PlayerMovement : MonoBehaviour
{
    // 移动速度（米/秒）。public 字段会显示在 Inspector 里，可以直接调
    public float moveSpeed = 5f;

    // Update 每一帧调用一次
    void Update()
    {
        // 拿到当前键盘，没插键盘就直接返回
        Keyboard keyboard = Keyboard.current;
        if (keyboard == null) return;

        // 读取 WASD：x 是左右，z 是前后
        float x = 0f;
        float z = 0f;
        if (keyboard.wKey.isPressed) z += 1f;
        if (keyboard.sKey.isPressed) z -= 1f;
        if (keyboard.dKey.isPressed) x += 1f;
        if (keyboard.aKey.isPressed) x -= 1f;

        // 组成方向向量，并归一化，防止斜着走比直走快
        Vector3 direction = new Vector3(x, 0f, z).normalized;

        // 位移 = 方向 × 速度 × 这一帧经过的时间（Time.deltaTime 让移动不受帧率影响）
        transform.Translate(direction * moveSpeed * Time.deltaTime, Space.World);
    }
}
