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
    }
}
