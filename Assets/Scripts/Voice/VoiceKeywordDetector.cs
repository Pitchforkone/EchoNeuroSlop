using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using UnityEngine;
using Vosk;

/// <summary>
/// Распознаёт речь через микрофон с помощью Vosk (офлайн) и передаёт
/// каждое распознанное слово зарегистрированным слушателям (IVoiceWordListener).
/// Использует SharedMicrophone для доступа к микрофону (общий с PlayerEchoLocator).
///
/// Использование:
///   1. Повесить на любой GameObject на сцене.
///   2. Убедиться, что на сцене есть SharedMicrophone.
///   3. Положить модель Vosk в StreamingAssets/vosk-model.
///   4. Зарегистрировать слушателей через AddListener / RemoveListener.
/// </summary>
public class VoiceRecognizer : MonoBehaviour
{
    [Header("Настройки Vosk")]
    [Tooltip("Имя папки модели внутри StreamingAssets")]
    [SerializeField] private string _modelFolder = "vosk-model";

    private Model _model;
    private VoskRecognizer _recognizer;
    private int _lastSamplePos;
    private bool _isRunning;

    private readonly List<IVoiceWordListener> _listeners = new List<IVoiceWordListener>();

    // Потокобезопасная очередь результатов из фонового потока
    private readonly Queue<string> _resultQueue = new Queue<string>();
    private readonly object _lock = new object();

    private Thread _processThread;
    private readonly Queue<float[]> _audioQueue = new Queue<float[]>();
    private readonly object _audioLock = new object();
    private volatile bool _threadRunning;

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

    private void Start()
    {
        InitializeVosk();
    }

    private void InitializeVosk()
    {
        string modelPath = Path.Combine(Application.streamingAssetsPath, _modelFolder);

        if (!Directory.Exists(modelPath))
        {
            Debug.LogError($"[VoiceRecognizer] Модель Vosk не найдена: {modelPath}");
            Debug.LogError("[VoiceRecognizer] Скачайте модель с https://alphacephei.com/vosk/models");
            Debug.LogError($"[VoiceRecognizer] и распакуйте в StreamingAssets/{_modelFolder}");
            return;
        }

        if (SharedMicrophone.Instance == null || !SharedMicrophone.Instance.IsRecording)
        {
            Debug.LogError("[VoiceRecognizer] SharedMicrophone не найден или не записывает. Добавьте SharedMicrophone на сцену.");
            return;
        }

        int sampleRate = SharedMicrophone.Instance.SampleRate;

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

        _lastSamplePos = SharedMicrophone.Instance.GetPosition();
        _isRunning = true;

        _threadRunning = true;
        _processThread = new Thread(ProcessAudioThread);
        _processThread.IsBackground = true;
        _processThread.Start();
    }

    private void Update()
    {
        if (!_isRunning || _recognizer == null) return;

        var mic = SharedMicrophone.Instance;
        if (mic == null || !mic.IsRecording || mic.Clip == null) return;

        int currentPos = mic.GetPosition();
        if (currentPos == _lastSamplePos) return;

        int sampleCount;
        if (currentPos > _lastSamplePos)
        {
            sampleCount = currentPos - _lastSamplePos;
        }
        else
        {
            sampleCount = (mic.Clip.samples - _lastSamplePos) + currentPos;
        }

        float[] samples = new float[sampleCount];
        mic.Clip.GetData(samples, _lastSamplePos);
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
            }
            else
            {
                string partial = _recognizer.PartialResult();
                string text = ParseVoskPartial(partial);
                if (!string.IsNullOrEmpty(text))
                {
                    lock (_lock)
                    {
                        _resultQueue.Enqueue(text);
                    }
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

    private static string ParseVoskPartial(string json)
    {
        return ExtractJsonValue(json, "partial");
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
            string trimmed = word.Trim();
            if (string.IsNullOrEmpty(trimmed)) continue;

            NotifyListeners(trimmed);
        }
    }

    private void NotifyListeners(string word)
    {
        for (int i = _listeners.Count - 1; i >= 0; i--)
        {
            try
            {
                _listeners[i].OnWordRecognized(word);
            }
            catch (Exception e)
            {
                Debug.LogError($"[VoiceRecognizer] Ошибка в слушателе: {e.Message}");
            }
        }
    }

    private void OnDisable()
    {
        StopAll();
    }

    private void OnDestroy()
    {
        StopAll();
    }

    private void StopAll()
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
