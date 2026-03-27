using Mirror;
using System;
using System.Collections.Generic;
using UnityEngine;

public class DoorActivateZoneMB : NetworkBehaviour, IVoiceWordListener
{
    public VoiceActivateZoneMB activateZoneMB;
    private List<VoiceRecognizer> listPlayer = new();
    private bool isOpened = false;
    public string KeyWord = "Open"; // —лово дл€ открыти€ двери
    public void Start()
    {
        activateZoneMB.activate += OnActivate;
        activateZoneMB.deactivate += OnDeactivate;
    }
    
    public void OnActivate(VoiceRecognizer voice)
    {
        if (isOpened) return;
        
        voice.AddListener(this);
        listPlayer.Add(voice);
    }
    
    public void OnDeactivate(VoiceRecognizer voice)
    {
        voice.RemoveListener(this);
        listPlayer.Remove(voice);
        
        // ”ничтожаем зону только если дверь уже открыта
        if (isOpened)
        {
            Destroy(activateZoneMB.gameObject);
        }
    }

    public void OnWordRecognized(string word)
    {
        if (isOpened) return;
        
        if (string.Equals(word, KeyWord, StringComparison.OrdinalIgnoreCase))
        {
            isOpened = true;
            listPlayer.ForEach(voice => voice.RemoveListener(this));
            listPlayer.Clear();
            GetComponent<Animator>().SetBool("Open", true);
            
            // —крываем подсказку дл€ локального игрока
            VoiceHintUI.Hide();
            
            // ”ничтожаем зону активации
            Destroy(activateZoneMB.gameObject);
        }
    }
}
