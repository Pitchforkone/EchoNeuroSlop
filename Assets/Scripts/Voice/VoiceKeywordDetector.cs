using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using UnityEngine;
using Photon.Pun;
using Vosk;

/// <summary>
/// Компонент распознавания речи через библиотеку Vosk (offline).
/// Работает только для локального игрока.
/// </summary>
public class VoiceRecognizer : MonoBehaviourPun
{
    public static VoiceRecognizer LocalInstance { get; private set; }

    [Header("Настройки Vosk")]
    [SerializeField] private string _modelFolder = "vosk-model";

    [Header("Инициализация")]
    [SerializeField] private float _maxWaitTime = 5f;
    [SerializeField] private float _checkInterval = 0.1f;

    private Model _model;
    private VoskRecognizer _recognizer;
    private int _lastSamplePos;
    private bool _isRunning;

    private readonly List<IVoiceWordListener> _listeners = new List<IVoiceWordListener>();

    private readonly Queue<string> _resultQueue = new Queue<string>();
    private readonly object _lock = new object();

    private Thread _processThread;
    private readonly Queue<float[]> _audioQueue = new Queue<float[]>();
    private readonly object _audioLock = new object();
    private volatile bool _threadRunning;

    private readonly HashSet<string> _sentWordsInCurrentPhrase = new HashSet<string>();
    private string _lastPartialText = "";

    private bool _initializationStarted;
    private SharedMicrophone _microphone;

    public void AddListener(IVoiceWordListener listener)
    {
        if (listener != null && !_listeners.Contains(listener))
            _listeners.Add(listener);
    }

    public void RemoveListener(IVoiceWordListener listener)
    {
        if (listener != null)
            _listeners.Remove(listener);
    }

    public void SimulateWord(string word)
    {
        if (string.IsNullOrWhiteSpace(word)) return;
        NotifyListeners(word.Trim());
    }

    private void Start()
    {
        if (!PhotonNetwork.IsConnected || photonView.IsMine)
        {
            InitializeAsLocal();
        }
    }

    private void InitializeAsLocal()
    {
        if (LocalInstance != null && LocalInstance != this)
        {
            LocalInstance.StopAll();
        }

        LocalInstance = this;

        _microphone = GetComponent<SharedMicrophone>();

        StartCoroutine(InitializeWithRetry());
    }

    private IEnumerator InitializeWithRetry()
    {
        if (_initializationStarted) yield break;
        _initializationStarted = true;

        float waitedTime = 0f;

        while (true)
        {
            if (_microphone != null && _microphone.IsRecording)
                break;

            if (SharedMicrophone.LocalInstance != null && SharedMicrophone.LocalInstance.IsRecording)
            {
                _microphone = SharedMicrophone.LocalInstance;
                break;
            }

            waitedTime += _checkInterval;

            if (waitedTime >= _maxWaitTime)
            {
                Debug.LogError("[VoiceRecognizer] SharedMicrophone не найден или не инициализирован после ожидания.");
                yield break;
            }

            yield return new WaitForSeconds(_checkInterval);
        }

        InitializeVosk();
    }

    private void InitializeVosk()
    {
        string modelPath = Path.Combine(Application.streamingAssetsPath, _modelFolder);

        if (!Directory.Exists(modelPath))
        {
            Debug.LogError($"[VoiceRecognizer] Модель Vosk не найдена: {modelPath}");
            return;
        }

        if (_microphone == null || !_microphone.IsRecording)
        {
            Debug.LogError("[VoiceRecognizer] SharedMicrophone не найден или не инициализирован.");
            return;
        }

        int sampleRate = _microphone.SampleRate;

        try
        {
            Vosk.Vosk.SetLogLevel(-1);
            _model = new Model(modelPath);
            _recognizer = new VoskRecognizer(_model, sampleRate);
        }
        catch (Exception e)
        {
            Debug.LogError($"[VoiceRecognizer] Ошибка инициализации Vosk: {e.Message}");
            return;
        }

        _lastSamplePos = _microphone.GetPosition();
        _isRunning = true;

        _threadRunning = true;
        _processThread = new Thread(ProcessAudioThread);
        _processThread.IsBackground = true;
        _processThread.Start();
    }

    private void Update()
    {
        if (PhotonNetwork.IsConnected && !photonView.IsMine) return;

        if (!_isRunning || _recognizer == null) return;

        if (_microphone == null || !_microphone.IsRecording || _microphone.Clip == null) return;

        int currentPos = _microphone.GetPosition();
        if (currentPos == _lastSamplePos) return;

        int sampleCount;
        if (currentPos > _lastSamplePos)
        {
            sampleCount = currentPos - _lastSamplePos;
        }
        else
        {
            sampleCount = (_microphone.Clip.samples - _lastSamplePos) + currentPos;
        }

        float[] samples = new float[sampleCount];
        _microphone.Clip.GetData(samples, _lastSamplePos);
        _lastSamplePos = currentPos;

        lock (_audioLock)
        {
            _audioQueue.Enqueue(samples);
        }

        lock (_lock)
        {
            while (_resultQueue.Count > 0)
            {
                string text = _resultQueue.Dequeue();
                ProcessRecognizedText(text);
            }
        }
    }

    private void ProcessAudioThread()
    {
        while (_threadRunning)
        {
            float[] samples = null;

            lock (_audioLock)
            {
                if (_audioQueue.Count > 0)
                    samples = _audioQueue.Dequeue();
            }

            if (samples == null)
            {
                Thread.Sleep(10);
                continue;
            }

            byte[] pcmBytes = FloatToPCM16(samples);

            if (_recognizer.AcceptWaveform(pcmBytes, pcmBytes.Length))
            {
                string result = _recognizer.Result();
                string text = ParseVoskText(result);
                if (!string.IsNullOrEmpty(text))
                {
                    lock (_lock)
                    {
                        _resultQueue.Enqueue(text);
                    }
                }
                _lastPartialText = "";
                lock (_lock)
                {
                    _sentWordsInCurrentPhrase.Clear();
                }
            }
        }
    }

    private static byte[] FloatToPCM16(float[] floatSamples)
    {
        byte[] bytes = new byte[floatSamples.Length * 2];
        for (int i = 0; i < floatSamples.Length; i++)
        {
            float clamped = Mathf.Clamp(floatSamples[i], -1f, 1f);
            short val = (short)(clamped * 32767f);
            bytes[i * 2] = (byte)(val & 0xFF);
            bytes[i * 2 + 1] = (byte)((val >> 8) & 0xFF);
        }
        return bytes;
    }

    private static string ParseVoskText(string json)
    {
        return ExtractJsonValue(json, "text");
    }

    private static string ExtractJsonValue(string json, string key)
    {
        if (string.IsNullOrEmpty(json)) return null;

        string search = "\"" + key + "\" : \"";
        int start = json.IndexOf(search, StringComparison.Ordinal);
        if (start < 0)
        {
            search = "\"" + key + "\":\"";
            start = json.IndexOf(search, StringComparison.Ordinal);
        }
        if (start < 0) return null;

        start += search.Length;
        int end = json.IndexOf("\"", start, StringComparison.Ordinal);
        if (end < 0) return null;

        string value = json.Substring(start, end - start).Trim();
        return string.IsNullOrEmpty(value) ? null : value;
    }

    private void ProcessRecognizedText(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return;

        string[] words = text.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);

        foreach (var word in words)
        {
            string trimmed = word.Trim().ToLowerInvariant();
            if (string.IsNullOrEmpty(trimmed)) continue;

            if (_sentWordsInCurrentPhrase.Contains(trimmed))
                continue;

            _sentWordsInCurrentPhrase.Add(trimmed);
            NotifyListeners(trimmed);
        }

        _sentWordsInCurrentPhrase.Clear();
    }

    private void NotifyListeners(string word)
    {
        for (int i = _listeners.Count - 1; i >= 0; i--)
        {
            try
            {
                _listeners[i].OnWordRecognized(word);
                Debug.Log($"[VoiceWordSender] Отправлено слово: {word}");
            }
            catch (Exception e)
            {
                Debug.LogError($"[VoiceRecognizer] Ошибка в слушателе: {e.Message}");
            }
        }
    }

    private void OnDisable()
    {
        if (!PhotonNetwork.IsConnected)
        {
            StopAll();
        }
    }

    private void OnDestroy()
    {
        CleanupLocal();
    }

    private void CleanupLocal()
    {
        StopAll();
        if (LocalInstance == this)
        {
            LocalInstance = null;
        }
    }

    public void StopAll()
    {
        _isRunning = false;
        _threadRunning = false;

        if (_processThread != null && _processThread.IsAlive)
        {
            _processThread.Join(1000);
            _processThread = null;
        }

        if (_recognizer != null)
        {
            _recognizer.Dispose();
            _recognizer = null;
        }

        if (_model != null)
        {
            _model.Dispose();
            _model = null;
        }
    }
}
