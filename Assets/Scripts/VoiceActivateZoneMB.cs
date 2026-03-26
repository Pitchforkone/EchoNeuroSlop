using System;
using UnityEngine;
[RequireComponent(typeof(Collider))]
public class VoiceActivateZoneMB : MonoBehaviour
{
    public Action<VoiceRecognizer> activate;
    public Action<VoiceRecognizer> deactivate;
    private void Start()
    {
        GetComponent<Collider>().isTrigger = true;
    }
    public void OnTriggerEnter(Collider other)
    {
        if (other.TryGetComponent(out VoiceRecognizer recognizer))
        {
            activate?.Invoke(recognizer);
        }
    }
    public void OnTriggerExit(Collider other)
    {
        if (other.TryGetComponent(out VoiceRecognizer recognizer))
        {
            deactivate?.Invoke(recognizer);
        }
    }
}

