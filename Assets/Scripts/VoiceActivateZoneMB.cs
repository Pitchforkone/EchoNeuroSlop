using System;
using UnityEngine;
[RequireComponent(typeof(Collider))]
public class VoiceActivateZoneMB : MonoBehaviour
{
    [Header("Voice Settings")]
    [Tooltip("Keyword to display when player enters the zone")]
    private string keyword = "Open";

    public Action<VoiceRecognizer> activate;
    public Action<VoiceRecognizer> deactivate;

    private void Start()
    {
        GetComponent<Collider>().isTrigger = true;
    }
    public void SetKeyword(string newKeyword)
    {
        keyword = newKeyword;
    }

    public void OnTriggerEnter(Collider other)
    {
        if (other.TryGetComponent(out VoiceRecognizer recognizer))
        {
            activate?.Invoke(recognizer);

            // Notify UI only for the local player
            if (recognizer == VoiceRecognizer.LocalInstance)
            {
                VoiceHintUI.Show(keyword);
            }
        }
    }

    public void OnTriggerExit(Collider other)
    {
        if (other.TryGetComponent(out VoiceRecognizer recognizer))
        {
            deactivate?.Invoke(recognizer);

            // Notify UI only for the local player
            if (recognizer == VoiceRecognizer.LocalInstance)
            {
                VoiceHintUI.Hide();
            }
        }
    }
}

