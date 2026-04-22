using System;
using UnityEngine;
    [Serializable]
    public class RuntimeMeshVariant
    {
        public Mesh Mesh;
        public Material[] Materials;
    }

    [Serializable]
    public class RuntimeSlotGroup
    {
        public string GroupType;
        public RuntimeMeshVariant[] Variants;
    }

    [Serializable]
    public class RuntimeSlotEntry
    {
        public string SlotType;
        public RuntimeSlotGroup[] Groups;
    }

    [Serializable]
    public class RuntimeFullBodySlotEntry
    {
        public string SlotType;
        public Mesh Mesh;
        public Material[] Materials;
    }

    [Serializable]
    public class RuntimeFullBodyVariant
    {
        public RuntimeFullBodySlotEntry[] Slots;
    }

    [CreateAssetMenu(menuName = "Character Customization Tool/Runtime Character Data", fileName = "RuntimeCharacterData")]
    public class RuntimeCharacterData : ScriptableObject
    {
        public RuntimeFullBodyVariant[] FullBodyCostumes;
        public RuntimeSlotEntry[] Slots;
        public Mesh[] FaceMeshes;
    }
