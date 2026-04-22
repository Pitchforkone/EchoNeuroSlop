using System;
using System.Collections.Generic;
using System.Linq;
using CharacterCustomizationTool.FaceManagement;
using UnityEngine;
using Random = UnityEngine.Random;

    public class CharacterRandomizer : MonoBehaviour
    {
        [SerializeField] private RuntimeCharacterData _data;

        private void Awake()
        {
            Randomize();
        }

        public void Randomize()
        {
            if (_data == null)
            {
                Debug.LogError("[CharacterRandomizer] RuntimeCharacterData is not assigned.", this);
                return;
            }

            ClearAllSlots();

            var availableGroups = Enum.GetValues(typeof(RuntimeGroupType)).Cast<RuntimeGroupType>().ToList();
            int bodyVariantIndex = 0;

            foreach (var step in BuildSteps())
            {
                var slot = FindSlotForGroup(step.GroupType);
                var group = slot?.Groups.FirstOrDefault(g => g.GroupType == step.GroupType.ToString());
                int count = group?.Variants.Length ?? 0;

                var (index, isActive, nextGroups) = step.Process(count, availableGroups, bodyVariantIndex);

                availableGroups = nextGroups;

                if (step.GroupType == RuntimeGroupType.Body && isActive)
                    bodyVariantIndex = index;

                if (!isActive || group == null || index < 0 || index >= group.Variants.Length)
                    continue;

                if (step.GroupType == RuntimeGroupType.Costume)
                {
                    ApplyFullBodyCostume(index);
                }
                else
                {
                    var variant = group.Variants[index];
                    ApplyVariantToSlot(slot.SlotType, variant);
                }
            }

            SetupFacePicker();
        }

        private void ClearAllSlots()
        {
            foreach (Transform child in transform)
            {
                if (child.TryGetComponent<SkinnedMeshRenderer>(out var smr))
                    smr.sharedMesh = null;
            }
        }

        private void ApplyVariantToSlot(string slotTypeName, RuntimeMeshVariant variant)
        {
            var smr = FindRendererForSlot(slotTypeName);
            if (smr == null) return;

            smr.sharedMesh = variant.Mesh;
            smr.sharedMaterials = variant.Materials;
            if (smr.sharedMesh != null)
                smr.localBounds = smr.sharedMesh.bounds;
        }

        private void ApplyFullBodyCostume(int index)
        {
            if (index < 0 || index >= _data.FullBodyCostumes.Length) return;

            var costume = _data.FullBodyCostumes[index];
            foreach (var slot in costume.Slots)
            {
                ApplyVariantToSlot(slot.SlotType, new RuntimeMeshVariant { Mesh = slot.Mesh, Materials = slot.Materials });
            }
        }

        private SkinnedMeshRenderer FindRendererForSlot(string slotTypeName)
        {
            foreach (Transform child in transform)
            {
                var normalized = string.Join("", child.name.Split('_'));
                if (normalized.StartsWith(slotTypeName) && child.TryGetComponent<SkinnedMeshRenderer>(out var smr))
                    return smr;
            }
            return null;
        }

        private RuntimeSlotEntry FindSlotForGroup(RuntimeGroupType groupType)
        {
            if (groupType == RuntimeGroupType.Costume)
                return new RuntimeSlotEntry { SlotType = "Costumes", Groups = new[] { new RuntimeSlotGroup { GroupType = "Costume", Variants = new RuntimeMeshVariant[_data.FullBodyCostumes.Length] } } };

            return _data.Slots.FirstOrDefault(s =>
                s.Groups.Any(g => g.GroupType == groupType.ToString()));
        }

        private void SetupFacePicker()
        {
            if (_data.FaceMeshes == null || _data.FaceMeshes.Length == 0) return;

            var faceSmr = GetComponentsInChildren<SkinnedMeshRenderer>()
                .FirstOrDefault(r => r.sharedMesh != null && r.name.StartsWith("Faces"));

            if (faceSmr == null) return;

            var facePicker = GetComponent<FacePicker>() ?? gameObject.AddComponent<FacePicker>();

            var meshName = faceSmr.sharedMesh.name;
            var nameParts = meshName.Split('_');
            if (nameParts.Length < 4) return;

            var faceKey = nameParts[3];
            var matchingFaces = _data.FaceMeshes
                .Where(m => m.name.Split('_').Length >= 4 && m.name.Split('_')[3] == faceKey)
                .ToArray();

            if (matchingFaces.Length > 0)
                facePicker.SetFaces(matchingFaces);
        }

        // ── Step definitions (mirrors RandomCharacterGenerator, no Editor APIs) ──

        private static IEnumerable<RuntimeStep> BuildSteps()
        {
            return new RuntimeStep[]
            {
                new FacesRuntimeStep(),
                new BodyRuntimeStep(),
                new CostumeRuntimeStep(),
                new OutfitRuntimeStep(),
                new OutwearRuntimeStep(),
                new PantsRuntimeStep(),
                new ShortsRuntimeStep(),
                new SocksRuntimeStep(),
                new HatSingleRuntimeStep(),
                new HatRuntimeStep(),
                new HatsWithHairRuntimeStep(),
                new FaceAccessoriesRuntimeStep(),
                new GlassesRuntimeStep(),
                new ShoesRuntimeStep(),
                new HairstyleRuntimeStep(),
                new GlovesRuntimeStep(),
                new HairWithHatsRuntimeStep(),
                new EarsRuntimeStep(),
            };
        }
    }

    // ── Runtime group type enum (mirrors GroupType without Editor dependency) ──

    public enum RuntimeGroupType
    {
        Faces, Body, Costume, Outfit, Outwear, Pants, Shorts, Socks,
        HatSingle, Hat, HatsWithHair, FaceAccessories, Glasses, Shoes,
        Hairstyle, Gloves, HairWithHats, Ears,
    }

    // ── Step base classes ──

    internal abstract class RuntimeStep
    {
        public abstract RuntimeGroupType GroupType { get; }
        protected virtual float Probability => 0.35f;

        public abstract (int index, bool isActive, List<RuntimeGroupType> nextGroups) Process(
            int count, List<RuntimeGroupType> groups, int bodyIndex);

        protected List<RuntimeGroupType> RemoveSelf(List<RuntimeGroupType> groups)
            => groups.Where(g => g != GroupType).ToList();
    }

    internal abstract class SlotRuntimeStep : RuntimeStep
    {
        protected abstract RuntimeGroupType[] CompatibleGroups { get; }

        public override (int, bool, List<RuntimeGroupType>) Process(int count, List<RuntimeGroupType> groups, int bodyIndex)
        {
            var cannotProcess = !groups.Contains(GroupType);
            var next = RemoveSelf(groups);

            if (cannotProcess || Random.value > Probability)
                return (0, false, next);

            var filtered = next.Where(g => CompatibleGroups.Contains(g)).ToList();
            return (Random.Range(0, count), true, filtered);
        }
    }

    // ── Concrete step implementations ──

    internal class FacesRuntimeStep : RuntimeStep
    {
        public override RuntimeGroupType GroupType => RuntimeGroupType.Faces;
        public override (int, bool, List<RuntimeGroupType>) Process(int count, List<RuntimeGroupType> groups, int bodyIndex)
            => (Random.Range(0, count), true, RemoveSelf(groups));
    }

    internal class BodyRuntimeStep : RuntimeStep
    {
        public override RuntimeGroupType GroupType => RuntimeGroupType.Body;
        public override (int, bool, List<RuntimeGroupType>) Process(int count, List<RuntimeGroupType> groups, int bodyIndex)
            => (Random.Range(0, count), true, RemoveSelf(groups));
    }

    internal class CostumeRuntimeStep : RuntimeStep
    {
        public override RuntimeGroupType GroupType => RuntimeGroupType.Costume;
        protected override float Probability => 0.2f;
        public override (int, bool, List<RuntimeGroupType>) Process(int count, List<RuntimeGroupType> groups, int bodyIndex)
        {
            if (Random.value > Probability)
                return (0, false, RemoveSelf(groups));
            return (Random.Range(0, count), true, new List<RuntimeGroupType>());
        }
    }

    internal class OutfitRuntimeStep : SlotRuntimeStep
    {
        public override RuntimeGroupType GroupType => RuntimeGroupType.Outfit;
        protected override RuntimeGroupType[] CompatibleGroups => new[] { RuntimeGroupType.HatSingle, RuntimeGroupType.Hat, RuntimeGroupType.HatsWithHair, RuntimeGroupType.FaceAccessories, RuntimeGroupType.Glasses, RuntimeGroupType.Shoes, RuntimeGroupType.Hairstyle, RuntimeGroupType.Gloves, RuntimeGroupType.Ears };
    }

    internal class OutwearRuntimeStep : SlotRuntimeStep
    {
        public override RuntimeGroupType GroupType => RuntimeGroupType.Outwear;
        protected override RuntimeGroupType[] CompatibleGroups => new[] { RuntimeGroupType.Pants, RuntimeGroupType.Shorts, RuntimeGroupType.HatSingle, RuntimeGroupType.Hat, RuntimeGroupType.HatsWithHair, RuntimeGroupType.FaceAccessories, RuntimeGroupType.Glasses, RuntimeGroupType.Shoes, RuntimeGroupType.Hairstyle, RuntimeGroupType.Gloves, RuntimeGroupType.Ears };
    }

    internal class PantsRuntimeStep : SlotRuntimeStep
    {
        public override RuntimeGroupType GroupType => RuntimeGroupType.Pants;
        protected override RuntimeGroupType[] CompatibleGroups => new[] { RuntimeGroupType.HatSingle, RuntimeGroupType.Hat, RuntimeGroupType.HatsWithHair, RuntimeGroupType.FaceAccessories, RuntimeGroupType.Glasses, RuntimeGroupType.Shoes, RuntimeGroupType.Hairstyle, RuntimeGroupType.Gloves, RuntimeGroupType.Ears };
    }

    internal class ShortsRuntimeStep : SlotRuntimeStep
    {
        public override RuntimeGroupType GroupType => RuntimeGroupType.Shorts;
        protected override RuntimeGroupType[] CompatibleGroups => new[] { RuntimeGroupType.HatSingle, RuntimeGroupType.Hat, RuntimeGroupType.HatsWithHair, RuntimeGroupType.FaceAccessories, RuntimeGroupType.Glasses, RuntimeGroupType.Shoes, RuntimeGroupType.Hairstyle, RuntimeGroupType.Gloves, RuntimeGroupType.Ears };

        public override (int, bool, List<RuntimeGroupType>) Process(int count, List<RuntimeGroupType> groups, int bodyIndex)
        {
            var cannotProcess = !groups.Contains(GroupType);
            var next = RemoveSelf(groups);
            if (cannotProcess || Random.value > Probability)
                return (0, false, next);
            var filtered = next.Where(g => CompatibleGroups.Contains(g)).ToList();
            filtered.Add(RuntimeGroupType.Socks);
            return (Random.Range(0, count), true, filtered);
        }
    }

    internal class SocksRuntimeStep : SlotRuntimeStep
    {
        public override RuntimeGroupType GroupType => RuntimeGroupType.Socks;
        protected override float Probability => 0.5f;
        protected override RuntimeGroupType[] CompatibleGroups => new[] { RuntimeGroupType.HatSingle, RuntimeGroupType.Hat, RuntimeGroupType.HatsWithHair, RuntimeGroupType.FaceAccessories, RuntimeGroupType.Glasses, RuntimeGroupType.Shoes, RuntimeGroupType.Hairstyle, RuntimeGroupType.Gloves, RuntimeGroupType.Ears };
    }

    internal class HatSingleRuntimeStep : SlotRuntimeStep
    {
        public override RuntimeGroupType GroupType => RuntimeGroupType.HatSingle;
        protected override RuntimeGroupType[] CompatibleGroups => new[] { RuntimeGroupType.Shoes, RuntimeGroupType.Gloves };
    }

    internal class HatRuntimeStep : SlotRuntimeStep
    {
        public override RuntimeGroupType GroupType => RuntimeGroupType.Hat;
        protected override RuntimeGroupType[] CompatibleGroups => new[] { RuntimeGroupType.FaceAccessories, RuntimeGroupType.Glasses, RuntimeGroupType.Shoes, RuntimeGroupType.Gloves, RuntimeGroupType.Ears };
    }

    internal class HatsWithHairRuntimeStep : SlotRuntimeStep
    {
        public override RuntimeGroupType GroupType => RuntimeGroupType.HatsWithHair;
        protected override RuntimeGroupType[] CompatibleGroups => new[] { RuntimeGroupType.FaceAccessories, RuntimeGroupType.Shoes, RuntimeGroupType.Gloves, RuntimeGroupType.Ears };

        public override (int, bool, List<RuntimeGroupType>) Process(int count, List<RuntimeGroupType> groups, int bodyIndex)
        {
            var cannotProcess = !groups.Contains(GroupType);
            var next = RemoveSelf(groups);
            if (cannotProcess || Random.value > Probability)
                return (0, false, next);
            var filtered = next.Where(g => CompatibleGroups.Contains(g)).ToList();
            if (Random.value > 0.5f)
                filtered.Add(RuntimeGroupType.Glasses);
            return (Random.Range(0, count), true, filtered);
        }
    }

    internal class FaceAccessoriesRuntimeStep : SlotRuntimeStep
    {
        public override RuntimeGroupType GroupType => RuntimeGroupType.FaceAccessories;
        protected override RuntimeGroupType[] CompatibleGroups => new[] { RuntimeGroupType.Glasses, RuntimeGroupType.Shoes, RuntimeGroupType.Hairstyle, RuntimeGroupType.Gloves, RuntimeGroupType.Ears };
    }

    internal class GlassesRuntimeStep : SlotRuntimeStep
    {
        public override RuntimeGroupType GroupType => RuntimeGroupType.Glasses;
        protected override RuntimeGroupType[] CompatibleGroups => new[] { RuntimeGroupType.Shoes, RuntimeGroupType.Hairstyle, RuntimeGroupType.Gloves, RuntimeGroupType.Ears };
    }

    internal class ShoesRuntimeStep : SlotRuntimeStep
    {
        public override RuntimeGroupType GroupType => RuntimeGroupType.Shoes;
        protected override RuntimeGroupType[] CompatibleGroups => new[] { RuntimeGroupType.Hairstyle, RuntimeGroupType.Gloves, RuntimeGroupType.Ears };

        public override (int, bool, List<RuntimeGroupType>) Process(int count, List<RuntimeGroupType> groups, int bodyIndex)
        {
            var cannotProcess = !groups.Contains(GroupType);
            var next = RemoveSelf(groups);
            if (cannotProcess || Random.value > Probability)
                return (0, false, next);
            var filtered = next.Where(g => CompatibleGroups.Contains(g)).ToList();
            if (Random.value > 0.5f)
                filtered.Add(RuntimeGroupType.HairWithHats);
            return (Random.Range(0, count), true, filtered);
        }
    }

    internal class HairstyleRuntimeStep : SlotRuntimeStep
    {
        public override RuntimeGroupType GroupType => RuntimeGroupType.Hairstyle;
        protected override RuntimeGroupType[] CompatibleGroups => new[] { RuntimeGroupType.Gloves, RuntimeGroupType.Ears };
    }

    internal class GlovesRuntimeStep : SlotRuntimeStep
    {
        public override RuntimeGroupType GroupType => RuntimeGroupType.Gloves;
        protected override RuntimeGroupType[] CompatibleGroups => new[] { RuntimeGroupType.Ears };
    }

    internal class HairWithHatsRuntimeStep : SlotRuntimeStep
    {
        public override RuntimeGroupType GroupType => RuntimeGroupType.HairWithHats;
        protected override float Probability => 1f;
        protected override RuntimeGroupType[] CompatibleGroups => new[] { RuntimeGroupType.Ears };
    }

    internal class EarsRuntimeStep : RuntimeStep
    {
        public override RuntimeGroupType GroupType => RuntimeGroupType.Ears;
        protected override float Probability => 1f;

        public override (int, bool, List<RuntimeGroupType>) Process(int count, List<RuntimeGroupType> groups, int bodyIndex)
        {
            var cannotProcess = !groups.Contains(GroupType);
            var next = RemoveSelf(groups);
            if (cannotProcess || Random.value > Probability)
                return (0, false, next);
            return (bodyIndex, true, next);
        }
    }
