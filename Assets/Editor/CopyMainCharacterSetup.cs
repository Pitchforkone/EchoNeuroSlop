using UnityEditor;
using UnityEngine;

/// <summary>
/// Editor utility: copies all gameplay components from MainCharacter prefab
/// onto FemaleCharacterPolyart and MaleCharacterPolyart prefabs.
/// Run via menu: Tools → Copy MainCharacter Setup to Polyart Characters
/// </summary>
public static class CopyMainCharacterSetup
{
    private const string SourcePath = "Assets/Prefab/MainCharacter.prefab";

    private static readonly string[] TargetPaths =
    {
        "Assets/RPG Tiny Hero Duo/Prefab/FemaleCharacterPolyart.prefab",
        "Assets/RPG Tiny Hero Duo/Prefab/MaleCharacterPolyart.prefab"
    };

    [MenuItem("Tools/Copy MainCharacter Setup to Polyart Characters")]
    static void Execute()
    {
        var sourcePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(SourcePath);
        if (sourcePrefab == null)
        {
            Debug.LogError($"[CopySetup] Source prefab not found: {SourcePath}");
            return;
        }

        foreach (string targetPath in TargetPaths)
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(targetPath) == null)
            {
                Debug.LogError($"[CopySetup] Target prefab not found: {targetPath}");
                continue;
            }
            CopyToTarget(sourcePrefab, targetPath);
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("[CopySetup] Done! MainCharacter components copied to both Polyart character prefabs.");
    }

    static void CopyToTarget(GameObject source, string targetPath)
    {
        var prefabRoot = PrefabUtility.LoadPrefabContents(targetPath);

        try
        {
            // 1. Set tag to Player
            prefabRoot.tag = "Player";

            // 2. Create Main Camera child (skip Capsule — models have their own mesh)
            Transform cameraTransform = CreateCameraChild(source, prefabRoot);

            // 3. Add CharacterController
            CopyComponentIfMissing<CharacterController>(source, prefabRoot);

            // 4. Copy all MonoBehaviours from source root
            foreach (var sourceMono in source.GetComponents<MonoBehaviour>())
            {
                if (sourceMono == null) continue;
                var type = sourceMono.GetType();

                // Skip if target already has this component type
                if (prefabRoot.GetComponent(type) != null) continue;

                var newComp = prefabRoot.AddComponent(type);
                if (newComp != null)
                {
                    EditorUtility.CopySerialized(sourceMono, newComp);
                    Debug.Log($"[CopySetup] Added {type.Name} to {prefabRoot.name}");
                }
                else
                {
                    Debug.LogWarning($"[CopySetup] Failed to add {type.Name} to {prefabRoot.name}");
                }
            }

            // 5. Fix internal references that pointed to source prefab hierarchy
            FixReferences(prefabRoot, cameraTransform);

            // 6. Save prefab
            PrefabUtility.SaveAsPrefabAsset(prefabRoot, targetPath);
            Debug.Log($"[CopySetup] Saved: {targetPath}");
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(prefabRoot);
        }
    }

    static Transform CreateCameraChild(GameObject source, GameObject target)
    {
        // Don't duplicate if already exists
        var existing = target.transform.Find("Main Camera");
        if (existing != null) return existing;

        var sourceCamTransform = source.transform.Find("Main Camera");
        if (sourceCamTransform == null)
        {
            Debug.LogWarning("[CopySetup] Main Camera not found in source prefab");
            return null;
        }

        // Create camera GameObject
        var camGO = new GameObject("Main Camera");
        camGO.tag = "MainCamera";
        camGO.transform.SetParent(target.transform, false);
        camGO.transform.localPosition = sourceCamTransform.localPosition;
        camGO.transform.localRotation = sourceCamTransform.localRotation;
        camGO.transform.localScale = sourceCamTransform.localScale;

        // Copy Camera component
        var sourceCam = sourceCamTransform.GetComponent<Camera>();
        if (sourceCam != null)
        {
            var cam = camGO.AddComponent<Camera>();
            EditorUtility.CopySerialized(sourceCam, cam);
        }

        // Copy AudioListener
        if (sourceCamTransform.GetComponent<AudioListener>() != null)
        {
            camGO.AddComponent<AudioListener>();
        }

        // Copy all MonoBehaviours on camera (e.g. UniversalAdditionalCameraData)
        foreach (var mono in sourceCamTransform.GetComponents<MonoBehaviour>())
        {
            if (mono == null) continue;
            var type = mono.GetType();
            var newComp = camGO.AddComponent(type);
            if (newComp != null)
            {
                EditorUtility.CopySerialized(mono, newComp);
            }
        }

        Debug.Log($"[CopySetup] Created Main Camera child on {target.name}");
        return camGO.transform;
    }

    static void CopyComponentIfMissing<T>(GameObject source, GameObject target) where T : Component
    {
        var sourceComp = source.GetComponent<T>();
        if (sourceComp == null) return;
        if (target.GetComponent<T>() != null) return;

        var newComp = target.AddComponent<T>();
        EditorUtility.CopySerialized(sourceComp, newComp);
        Debug.Log($"[CopySetup] Added {typeof(T).Name} to {target.name}");
    }

    static void FixReferences(GameObject prefabRoot, Transform cameraTransform)
    {
        foreach (var comp in prefabRoot.GetComponents<MonoBehaviour>())
        {
            if (comp == null) continue;

            var so = new SerializedObject(comp);

            // Fix PlayerController._cameraTransform → new camera
            var cameraProp = so.FindProperty("_cameraTransform");
            if (cameraProp != null && cameraProp.propertyType == SerializedPropertyType.ObjectReference
                && cameraTransform != null)
            {
                cameraProp.objectReferenceValue = cameraTransform;
                so.ApplyModifiedPropertiesWithoutUndo();
                Debug.Log($"[CopySetup] Fixed _cameraTransform on {comp.GetType().Name}");
            }

            // Fix NetworkTransformReliable.target → root transform
            if (comp.GetType().Name.Contains("NetworkTransform"))
            {
                var targetProp = so.FindProperty("target");
                if (targetProp != null && targetProp.propertyType == SerializedPropertyType.ObjectReference)
                {
                    targetProp.objectReferenceValue = prefabRoot.transform;
                    so.ApplyModifiedPropertiesWithoutUndo();
                    Debug.Log($"[CopySetup] Fixed target on {comp.GetType().Name}");
                }
            }

            // Reset NetworkIdentity._assetId so Mirror regenerates it
            if (comp.GetType().Name == "NetworkIdentity")
            {
                var assetIdProp = so.FindProperty("_assetId");
                if (assetIdProp != null)
                {
                    assetIdProp.intValue = 0;
                    so.ApplyModifiedPropertiesWithoutUndo();
                    Debug.Log($"[CopySetup] Reset _assetId on NetworkIdentity");
                }
            }
        }
    }
}
