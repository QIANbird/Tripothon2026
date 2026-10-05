using System.IO;
using UnityEditor;
using UnityEngine;

namespace Ghost.Morph.EditorTools
{
    public static class MorphMenu
    {
        const string FakePlantPath = "Assets/Data/Morph/FakePlant.asset";

        // 生成（或覆盖）假植物的节点数据资产
        [MenuItem("Ghost/Morph/Generate Fake Plant")]
        public static PlantNodeSet GenerateFakePlant()
        {
            var set = AssetDatabase.LoadAssetAtPath<PlantNodeSet>(FakePlantPath);
            if (set == null)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(FakePlantPath));
                set = ScriptableObject.CreateInstance<PlantNodeSet>();
                AssetDatabase.CreateAsset(set, FakePlantPath);
            }

            set.nodes = FakePlantGenerator.Generate(new FakePlantGenerator.Settings());
            LayoutGenerator.BuildAll(set.nodes, new LayoutGenerator.Settings());
            string error = set.Validate();
            if (error != null) Debug.LogError($"[Morph] 假植物数据无效：{error}");

            EditorUtility.SetDirty(set);
            AssetDatabase.SaveAssets();
            Debug.Log($"[Morph] 生成假植物：{set.Count} 个节点 → {FakePlantPath}");
            Selection.activeObject = set;
            return set;
        }

        const string NodeMaterialPath = "Assets/Art/Morph/MorphNode.mat";

        // 节点材质：Ghost/MorphNode 着色器 + GPU Instancing。已存在则直接返回
        [MenuItem("Ghost/Morph/Create Node Material")]
        public static Material EnsureNodeMaterial()
        {
            var mat = AssetDatabase.LoadAssetAtPath<Material>(NodeMaterialPath);
            if (mat != null) return mat;

            var shader = Shader.Find("Ghost/MorphNode");
            if (shader == null)
            {
                Debug.LogError("[Morph] 找不到着色器 Ghost/MorphNode");
                return null;
            }
            Directory.CreateDirectory(Path.GetDirectoryName(NodeMaterialPath));
            mat = new Material(shader) { enableInstancing = true };
            AssetDatabase.CreateAsset(mat, NodeMaterialPath);
            AssetDatabase.SaveAssets();
            Debug.Log($"[Morph] 创建节点材质 → {NodeMaterialPath}");
            return mat;
        }

        // Unity 内置立方体网格，节点默认形状
        public static Mesh BuiltinCube()
        {
            return Resources.GetBuiltinResource<Mesh>("Cube.fbx");
        }
    }
}
