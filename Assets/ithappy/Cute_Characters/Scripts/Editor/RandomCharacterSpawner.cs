using System.Linq;
using CharacterCustomizationTool.Editor.Character;
using CharacterCustomizationTool.Editor.FaceEditor;
using Controller;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace CharacterCustomizationTool.Editor
{
    public static class RandomCharacterSpawner
    {
        [MenuItem("Tools/Spawn Random Character")]
        private static void SpawnRandomCharacter()
        {
            BaseMeshAccessor.FindRoot();

            var customizableCharacter = new CustomizableCharacter(SlotLibraryLoader.LoadSlotLibrary());
            customizableCharacter.Randomize();

            var character = customizableCharacter.InstantiateCharacter();
            character.name = "RandomCharacter";

            foreach (var skinnedMeshRenderer in character.GetComponentsInChildren<SkinnedMeshRenderer>())
            {
                skinnedMeshRenderer.sharedMesh = null;
            }

            var enabledSlots = customizableCharacter.Slots.Where(s => s.IsEnabled).ToArray();
            foreach (var slot in enabledSlots)
            {
                foreach (var meshInfo in slot.Meshes)
                {
                    var child = character.transform
                        .Cast<Transform>()
                        .First(t => string.Join("", t.name.Split('_').ToArray()).StartsWith(meshInfo.Item1.ToString()));

                    if (child.TryGetComponent<SkinnedMeshRenderer>(out var smr))
                    {
                        smr.sharedMesh = meshInfo.Item2;
                        smr.sharedMaterials = meshInfo.Item3;
                        smr.localBounds = smr.sharedMesh.bounds;
                    }
                }
            }

            FaceLoader.AddFaces(character);
            AddAnimator(character);
            AddMovementComponents(character);

            var sceneView = SceneView.lastActiveSceneView;
            if (sceneView != null)
            {
                character.transform.position = sceneView.pivot;
            }

            Undo.RegisterCreatedObjectUndo(character, "Spawn Random Character");
            Selection.activeGameObject = character;
        }

        private static void AddAnimator(GameObject character)
        {
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(AssetsPath.AnimationController);
            var animator = character.GetComponent<Animator>();
            animator.runtimeAnimatorController = controller;
            animator.applyRootMotion = false;
        }

        private static void AddMovementComponents(GameObject character)
        {
            AddCharacterController(character);
        }

        private static void AddCharacterController(GameObject character)
        {
            var characterController = character.AddComponent<CharacterController>();
            characterController.center = new Vector3(0, .6f, 0);
            characterController.radius = .35f;
            characterController.height = 1.2f;
            characterController.skinWidth = 0.0001f;
        }
    }
}
