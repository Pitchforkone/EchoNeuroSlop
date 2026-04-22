using System.Linq;
using CharacterCustomizationTool.Editor.Character;
using UnityEditor;
using UnityEngine;

namespace CharacterCustomizationTool.Editor
{
    public static class RuntimeCharacterDataBaker
    {
        private const string OutputPath = "Assets/ithappy/Cute_Characters/Configs/RuntimeCharacterData.asset";

        [MenuItem("Tools/Bake Runtime Character Data")]
        private static void Bake()
        {
            BaseMeshAccessor.FindRoot();

            var slotLibrary = SlotLibraryLoader.LoadSlotLibrary();
            if (slotLibrary == null)
            {
                Debug.LogError("[RuntimeCharacterDataBaker] SlotLibrary not found.");
                return;
            }

            var data = AssetDatabase.LoadAssetAtPath<RuntimeCharacterData>(OutputPath)
                       ?? ScriptableObject.CreateInstance<RuntimeCharacterData>();

            data.FullBodyCostumes = BakeFullBodyCostumes(slotLibrary);
            data.Slots = BakeSlots(slotLibrary);
            data.FaceMeshes = AssetLoader.LoadAssets<Mesh>("t:Mesh", AssetsPath.Folder.Faces).ToArray();

            if (!AssetDatabase.Contains(data))
            {
                System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(OutputPath)!);
                AssetDatabase.CreateAsset(data, OutputPath);
            }

            EditorUtility.SetDirty(data);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"[RuntimeCharacterDataBaker] Baked successfully → {OutputPath}");
        }

        private static RuntimeFullBodyVariant[] BakeFullBodyCostumes(SlotLibrary slotLibrary)
        {
            return slotLibrary.FullBodyCostumes.Select(entry =>
            {
                var slots = entry.Slots.Select(slotEntry =>
                {
                    var smr = slotEntry.GameObject.GetComponentInChildren<SkinnedMeshRenderer>();
                    return new RuntimeFullBodySlotEntry
                    {
                        SlotType = slotEntry.Type.ToString(),
                        Mesh = smr.sharedMesh,
                        Materials = smr.sharedMaterials,
                    };
                }).ToArray();

                return new RuntimeFullBodyVariant { Slots = slots };
            }).ToArray();
        }

        private static RuntimeSlotEntry[] BakeSlots(SlotLibrary slotLibrary)
        {
            return slotLibrary.Slots.Select(slotEntry =>
            {
                var groups = slotEntry.Groups.Select(groupEntry =>
                {
                    var variants = groupEntry.Variants.Select(variantGo =>
                    {
                        var smr = variantGo.GetComponentInChildren<SkinnedMeshRenderer>();
                        return new RuntimeMeshVariant
                        {
                            Mesh = smr.sharedMesh,
                            Materials = smr.sharedMaterials,
                        };
                    }).ToArray();

                    return new RuntimeSlotGroup
                    {
                        GroupType = groupEntry.Type.ToString(),
                        Variants = variants,
                    };
                }).ToArray();

                return new RuntimeSlotEntry
                {
                    SlotType = slotEntry.Type.ToString(),
                    Groups = groups,
                };
            }).ToArray();
        }
    }
}
