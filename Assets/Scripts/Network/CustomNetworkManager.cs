using UnityEngine;
using Photon.Pun;
using Photon.Realtime;
using System.Collections.Generic;

/// <summary>
/// Custom Network Manager for handling player spawning with multiple character prefabs.
/// Uses Photon PUN 2 for networking.
///
/// Prefabs are assigned as GameObject references in the Inspector (drag-and-drop).
/// They are registered in Photon's DefaultPool at runtime, so they do NOT need to be in a Resources folder.
///
/// Required components on each player prefab:
///   - PhotonView               (об€зательно Ч идентификаци€ объекта в сети)
///   - PhotonTransformView       (синхронизаци€ позиции/поворота, добавить в Observed Components на PhotonView)
///   - CharacterController       (движение игрока)
///   - PlayerController          (управление от первого лица)
///   - PlayerEchoLocator         (эхолокаци€: шаги, активный пинг, микрофон)
///   - SharedMicrophone           (доступ к микрофону)
///   - VoiceRecognizer            (распознавание голоса через Vosk)
///   - VoiceWordSender            (отправка слов по гор€чим клавишам)
///   - PlayerInventory            (инвентарь)
///   - BoltThrower                (бросок болтов)
///   - VoiceGrenadeThrow          (бросок гранат из инвентар€)
///   - NightVisionController      (очки ночного видени€)
///   - Animator                   (если есть анимации) + PhotonAnimatorView (в Observed Components)
///   - Camera + AudioListener     (на дочернем объекте Ч камера от первого лица)
/// </summary>
public class CustomNetworkManager : MonoBehaviourPunCallbacks
{
    public static CustomNetworkManager Instance { get; private set; }

    [Header("Player Prefabs")]
    [Tooltip("ѕеретащите сюда префабы персонажей.\n" +
             "ѕервый игрок получит Element 0, второй Ч Element 1 и т.д.\n" +
             "ѕрефабы Ќ≈ об€заны лежать в папке Resources.")]
    public GameObject[] characterPrefabs;

    [Header("Spawn Settings")]
    public Vector3 spawnPosition = new Vector3(0, 1, 0);
    public float spawnRadius = 3f;

    [Header("Spawn Points")]
    [Tooltip("“очки спавна игроков")]
    public List<Transform> startPositions = new List<Transform>();

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        RegisterPrefabs();
    }

    /// <summary>
    /// Registers all character prefabs in Photon's DefaultPool so
    /// PhotonNetwork.Instantiate can find them by name without Resources folder.
    /// </summary>
    private void RegisterPrefabs()
    {
        if (characterPrefabs == null || characterPrefabs.Length == 0) return;

        DefaultPool pool = PhotonNetwork.PrefabPool as DefaultPool;
        if (pool == null)
        {
            Debug.LogError("[CustomNetworkManager] PhotonNetwork.PrefabPool is not DefaultPool! Cannot register prefabs.");
            return;
        }

        foreach (GameObject prefab in characterPrefabs)
        {
            if (prefab == null) continue;

            if (!pool.ResourceCache.ContainsKey(prefab.name))
            {
                pool.ResourceCache.Add(prefab.name, prefab);
                Debug.Log($"[CustomNetworkManager] Registered prefab: {prefab.name}");
            }
        }
    }

    public override void OnJoinedRoom()
    {
        base.OnJoinedRoom();
        SpawnLocalPlayer();
    }

    private void SpawnLocalPlayer()
    {
        if (characterPrefabs == null || characterPrefabs.Length == 0)
        {
            Debug.LogError("[CustomNetworkManager] No character prefabs assigned! " +
                           "Drag your player prefab(s) into the 'Character Prefabs' array in the Inspector.");
            return;
        }

        int index = (PhotonNetwork.LocalPlayer.ActorNumber - 1) % characterPrefabs.Length;
        GameObject prefab = characterPrefabs[index];

        if (prefab == null)
        {
            Debug.LogError($"[CustomNetworkManager] Character prefab at index {index} is null!");
            return;
        }

        if (prefab.GetComponent<PhotonView>() == null)
        {
            Debug.LogError($"[CustomNetworkManager] Prefab '{prefab.name}' is missing a PhotonView component!");
            return;
        }

        Vector3 position = GetSpawnPos();
        PhotonNetwork.Instantiate(prefab.name, position, Quaternion.identity);
        Debug.Log($"[CustomNetworkManager] Spawned player '{prefab.name}' at {position}");
    }

    private Vector3 GetSpawnPos()
    {
        if (startPositions.Count > 0)
        {
            Transform startPos = startPositions[Random.Range(0, startPositions.Count)];
            if (startPos != null)
                return startPos.position;
        }

        Vector2 randomOffset = Random.insideUnitCircle * spawnRadius;
        return spawnPosition + new Vector3(randomOffset.x, 0, randomOffset.y);
    }

    public override void OnLeftRoom()
    {
        base.OnLeftRoom();
    }

    /// <summary>
    /// Returns a random spawn position from startPositions list.
    /// </summary>
    public Vector3 GetRandomSpawnPosition()
    {
        return GetSpawnPos();
    }
}
