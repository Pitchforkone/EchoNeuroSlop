using CharacterCustomizationTool.FaceManagement;
using UnityEngine;

/// <summary>
/// Synchronizes face expressions with microphone input from SharedMicrophone.
/// Attach to the same GameObject as FacePicker.
/// </summary>
public class CharacterLipSync : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private FacePicker _facePicker;

    [Header("Emotion Settings")]
    [Tooltip("Base emotion shown when not speaking")]
    [SerializeField] private FaceType _baseEmotion = FaceType.Neutral;

    [Tooltip("Face used when mouth is open while speaking")]
    [SerializeField] private FaceType _talkingFace = FaceType.Shout;

    [Tooltip("Face used when very loud (shouting)")]
    [SerializeField] private FaceType _shoutFace = FaceType.Shout;

    [Header("Thresholds")]
    [Tooltip("Amplitude above this = character is speaking")]
    [SerializeField, Range(0.0001f, 0.1f)] private float _speakThreshold = 0.005f;

    [Tooltip("Amplitude above this = character is shouting")]
    [SerializeField, Range(0.05f, 1f)] private float _shoutThreshold = 0.2f;

    [Tooltip("How fast mouth opens/closes (frames per second)")]
    [SerializeField, Range(2f, 20f)] private float _mouthFlipRate = 8f;

    [Header("Smoothing")]
    [Tooltip("How fast amplitude smoothing reacts")]
    [SerializeField, Range(1f, 30f)] private float _smoothSpeed = 10f;

    private float _smoothAmplitude;
    private float _mouthFlipTimer;
    private bool _mouthOpen;
    private FaceType _lastAppliedFace;
    private int _lastMicPosition;

    private readonly float[] _audioSamples = new float[256];

    private void Awake()
    {
        if (_facePicker == null)
            _facePicker = GetComponentInChildren<FacePicker>();
    }

    private void Update()
    {
        float rawAmplitude = GetAmplitudeFromMicrophone();
        _smoothAmplitude = Mathf.Lerp(_smoothAmplitude, rawAmplitude, Time.deltaTime * _smoothSpeed);

        FaceType targetFace = ResolveFace(_smoothAmplitude);
        if (targetFace != _lastAppliedFace && _facePicker != null && _facePicker.HasFace(targetFace))
        {
            _facePicker.PickFace(targetFace);
            _lastAppliedFace = targetFace;
        }
    }

    private float GetAmplitudeFromMicrophone()
    {
        var mic = SharedMicrophone.LocalInstance;
        if (mic == null || !mic.IsRecording || mic.Clip == null)
            return 0f;

        int currentPos = mic.GetPosition();
        int clipSamples = mic.Clip.samples;

        // Сколько новых сэмплов появилось с прошлого кадра
        int sampleCount = _audioSamples.Length;
        int readFrom = (currentPos - sampleCount + clipSamples) % clipSamples;

        mic.Clip.GetData(_audioSamples, readFrom);

        float sum = 0f;
        for (int i = 0; i < _audioSamples.Length; i++)
            sum += _audioSamples[i] * _audioSamples[i];

        return Mathf.Sqrt(sum / _audioSamples.Length);
    }

    private FaceType ResolveFace(float amplitude)
    {
        if (amplitude < _speakThreshold)
        {
            _mouthOpen = false;
            return _baseEmotion;
        }

        if (amplitude >= _shoutThreshold)
            return _shoutFace;

        // Flip mouth open/closed at rate proportional to amplitude
        float flipInterval = 1f / Mathf.Lerp(_mouthFlipRate * 0.5f, _mouthFlipRate, amplitude / _shoutThreshold);
        _mouthFlipTimer += Time.deltaTime;
        if (_mouthFlipTimer >= flipInterval)
        {
            _mouthFlipTimer = 0f;
            _mouthOpen = !_mouthOpen;
        }

        return _mouthOpen ? _talkingFace : _baseEmotion;
    }

    /// <summary>
    /// Call this to change the base emotion at runtime (e.g. from dialogue system).
    /// </summary>
    public void SetBaseEmotion(FaceType emotion)
    {
        _baseEmotion = emotion;
    }

    /// <summary>
    /// Force a specific face for a duration, then return to lip sync.
    /// </summary>
    public void FlashEmotion(FaceType emotion, float duration)
    {
        StartCoroutine(FlashEmotionCoroutine(emotion, duration));
    }

    private System.Collections.IEnumerator FlashEmotionCoroutine(FaceType emotion, float duration)
    {
        var previous = _baseEmotion;
        SetBaseEmotion(emotion);
        yield return new WaitForSeconds(duration);
        SetBaseEmotion(previous);
    }
}
