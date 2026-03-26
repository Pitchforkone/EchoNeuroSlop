using Mirror;
using System;
using System.Collections.Generic;
using UnityEngine;

public class DoorActivateZoneMB : NetworkBehaviour, IVoiceWordListener
{
    public VoiceActivateZoneMB activateZoneMB;
    private List<VoiceRecognizer> listPlayer = new();
    public void Start()
    {
        activateZoneMB.activate += OnActivate;
        activateZoneMB.deactivate += OnDeactivate;
    }
    public void OnActivate(VoiceRecognizer voice)
    {
        voice.AddListener(this);
        listPlayer.Add(voice);
    }
    public void OnDeactivate(VoiceRecognizer voice)
    {
        voice.RemoveListener(this);
        listPlayer.Remove(voice);
    }

    public void OnWordRecognized(string word)
    {
        if (string.Equals(word, "Open", StringComparison.OrdinalIgnoreCase))
        {
            listPlayer.ForEach(voice => voice.RemoveListener(this));
            Destroy(gameObject);
        }
    }
}
