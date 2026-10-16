using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.XR;

namespace Ghost.Core.EditorTools
{
    // 主场景的视角 rig（docs/tasks/vr-migration.md 第 2 步）：XR Origin 替换原来的固定相机。
    //   XR Origin（原机位的地面点，朝 +Z）→ Camera Offset（本地 y = 眼高）→ Main Camera（Tracked Pose Driver 跟头显）
    //                                                              → Right Aim（Tracked Pose Driver 跟右手柄指向，VR 交互射线）
    //                                     → ViewAnchor（名义眼位，不跟头，PlantFit 按它构图）
    // PC：没有头显，Tracked Pose Driver 解析不到控件、不改相机；XR 只给 Android 开了 Loader，Camera Offset 保持 1.6 m，
    //     相机世界姿态和原固定机位完全一样。
    // Quest：Floor 模式，XR Origin 把 Camera Offset 归零，眼高交给头显。
    public static class XROriginBuilder
    {
        public struct Rig
        {
            public XROrigin origin;
            public Camera camera;
            // 不跟头动的名义眼位：位置 = 原固定机位，朝向水平 +Z
            public Transform viewAnchor;
            // 右手柄的指向姿态（射线起点，forward = 射线方向）。PC 上不动
            public Transform rightAim;
        }

        // eyePosition：原固定机位（眼睛的世界坐标），rig 的地面点在它正下方
        public static Rig Build(Vector3 eyePosition, Color background)
        {
            var originGo = new GameObject("XR Origin");
            originGo.transform.SetPositionAndRotation(new Vector3(eyePosition.x, 0f, eyePosition.z), Quaternion.identity);

            var offsetGo = new GameObject("Camera Offset");
            offsetGo.transform.SetParent(originGo.transform, false);
            offsetGo.transform.localPosition = Vector3.up * eyePosition.y;

            var cameraGo = new GameObject("Main Camera") { tag = "MainCamera" };
            cameraGo.transform.SetParent(offsetGo.transform, false);
            var camera = cameraGo.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = background;
            camera.fieldOfView = 60f;
            camera.nearClipPlane = 0.05f;
            cameraGo.AddComponent<AudioListener>();
            AddHeadTracking(cameraGo);

            var anchor = new GameObject("ViewAnchor").transform;
            anchor.SetParent(originGo.transform, false);
            anchor.localPosition = Vector3.up * eyePosition.y;

            // 右手柄：和相机同在 Camera Offset 下，Tracked Pose Driver 跟手柄的指向姿态（第 3 步）
            var aimGo = new GameObject("Right Aim");
            aimGo.transform.SetParent(offsetGo.transform, false);
            AddRightAimTracking(aimGo);

            var origin = originGo.AddComponent<XROrigin>();
            origin.Origin = originGo;
            origin.CameraFloorOffsetObject = offsetGo;
            origin.Camera = camera;
            origin.RequestedTrackingOriginMode = XROrigin.TrackingOriginMode.Floor;
            // 只在 Device 模式下生效（Floor 模式下偏移由头显决定），和 PC 眼高一致
            origin.CameraYOffset = eyePosition.y;

            return new Rig { origin = origin, camera = camera, viewAnchor = anchor, rightAim = aimGo.transform };
        }

        // 头显姿态：绑定写在组件里（不进 .inputactions，手柄绑定在第 3 步加）
        static void AddHeadTracking(GameObject cameraGo)
        {
            var driver = cameraGo.AddComponent<TrackedPoseDriver>();
            driver.trackingType = TrackedPoseDriver.TrackingType.RotationAndPosition;
            driver.updateType = TrackedPoseDriver.UpdateType.UpdateAndBeforeRender;
            driver.positionInput = new InputActionProperty(Action("Position", "Vector3", "<XRHMD>/centerEyePosition"));
            driver.rotationInput = new InputActionProperty(Action("Rotation", "Quaternion", "<XRHMD>/centerEyeRotation"));
            driver.trackingStateInput = new InputActionProperty(Action("Tracking State", "Integer", "<XRHMD>/trackingState"));
        }

        // 右手柄指向姿态：OpenXR 的 aim pose（pointerPosition / pointerRotation）。
        // 只绑这一组：Value 动作有多个绑定时按数值大小抢控制权，混绑握持姿态会来回跳。Touch / Touch Plus / Touch Pro 配置都有这组控件
        static void AddRightAimTracking(GameObject aimGo)
        {
            var driver = aimGo.AddComponent<TrackedPoseDriver>();
            driver.trackingType = TrackedPoseDriver.TrackingType.RotationAndPosition;
            driver.updateType = TrackedPoseDriver.UpdateType.UpdateAndBeforeRender;
            driver.positionInput = new InputActionProperty(Action("Right Aim Position", "Vector3",
                "<XRController>{RightHand}/pointerPosition"));
            driver.rotationInput = new InputActionProperty(Action("Right Aim Rotation", "Quaternion",
                "<XRController>{RightHand}/pointerRotation"));
            driver.trackingStateInput = new InputActionProperty(Action("Right Aim Tracking State", "Integer",
                "<XRController>{RightHand}/trackingState"));
        }

        static InputAction Action(string name, string controlType, params string[] bindings)
        {
            var action = new InputAction(name, InputActionType.Value, expectedControlType: controlType);
            foreach (var binding in bindings) action.AddBinding(binding);
            return action;
        }
    }
}
