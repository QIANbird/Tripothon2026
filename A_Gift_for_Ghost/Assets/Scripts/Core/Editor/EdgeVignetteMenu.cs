using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace Ghost.Core.EditorTools
{
    // 流动压暗边：创建材质，并挂到 PC / Mobile 两个渲染器的 Full Screen Pass 上（docs/tasks/screen-edge-vignette.md）
    // 已挂过的渲染器只更新材质引用；Mobile 关闭边缘虚化，省带宽
    public static class EdgeVignetteMenu
    {
        const string ShaderName = "Ghost/EdgeVignette";
        const string PcMaterialPath = "Assets/Art/PostFX/EdgeVignette.mat";
        const string MobileMaterialPath = "Assets/Art/PostFX/EdgeVignette_Mobile.mat";
        const string PcRendererPath = "Assets/Settings/PC_Renderer.asset";
        const string MobileRendererPath = "Assets/Settings/Mobile_Renderer.asset";
        const string FeatureName = "EdgeVignette";

        [MenuItem("Ghost/PostFX/Setup Edge Vignette")]
        public static void Setup()
        {
            var pc = EnsureMaterial(PcMaterialPath, 0.006f);
            var mobile = EnsureMaterial(MobileMaterialPath, 0f);
            if (pc == null || mobile == null) return;

            AttachFeature(PcRendererPath, pc);
            AttachFeature(MobileRendererPath, mobile);
            AssetDatabase.SaveAssets();
        }

        static Material EnsureMaterial(string path, float blur)
        {
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat != null) return mat;

            var shader = Shader.Find(ShaderName);
            if (shader == null)
            {
                Debug.LogError($"[EdgeVignette] 找不到着色器 {ShaderName}");
                return null;
            }
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            mat = new Material(shader);
            mat.SetFloat("_BlurAmount", blur);
            AssetDatabase.CreateAsset(mat, path);
            Debug.Log($"[EdgeVignette] 创建材质 → {path}");
            return mat;
        }

        static void AttachFeature(string rendererPath, Material material)
        {
            var data = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(rendererPath);
            if (data == null)
            {
                Debug.LogError($"[EdgeVignette] 找不到渲染器 {rendererPath}");
                return;
            }

            FullScreenPassRendererFeature feature = null;
            foreach (var f in data.rendererFeatures)
                if (f is FullScreenPassRendererFeature fs && f.name == FeatureName) feature = fs;

            if (feature == null)
            {
                feature = ScriptableObject.CreateInstance<FullScreenPassRendererFeature>();
                feature.name = FeatureName;
                AssetDatabase.AddObjectToAsset(feature, data);

                // rendererFeatures 和 m_RendererFeatureMap 要同步写，否则渲染器 Inspector 会报 Missing
                var so = new SerializedObject(data);
                var list = so.FindProperty("m_RendererFeatures");
                var map = so.FindProperty("m_RendererFeatureMap");
                list.arraySize++;
                list.GetArrayElementAtIndex(list.arraySize - 1).objectReferenceValue = feature;
                AssetDatabase.TryGetGUIDAndLocalFileIdentifier(feature, out _, out long localId);
                map.arraySize++;
                map.GetArrayElementAtIndex(map.arraySize - 1).longValue = localId;
                so.ApplyModifiedProperties();
                Debug.Log($"[EdgeVignette] 添加 Full Screen Pass → {rendererPath}");
            }

            feature.injectionPoint = FullScreenPassRendererFeature.InjectionPoint.AfterRenderingPostProcessing;
            feature.fetchColorBuffer = true;
            feature.requirements = ScriptableRenderPassInput.Color;
            feature.passMaterial = material;
            feature.passIndex = 0;
            feature.SetActive(true);
            EditorUtility.SetDirty(feature);
            EditorUtility.SetDirty(data);
            data.SetDirty();
        }
    }
}
