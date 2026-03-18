# Техническая архитектура

## Обзор системы

Три ключевых пайплайна:
1. **Голос → Эхо**: микрофон → анализ громкости → генерация эхо-волны → визуализация
2. **Мультиплеер**: NGO host/client, синхронизация эхо-событий и состояний
3. **Враг**: AI на host, слышит эхо-события, охотится на игроков

---

## 1. Мультиплеер (Netcode for GameObjects)

### Модель

```
[Host]  = Server + Client (Игрок 1)
[Client] = Client (Игрок 2)
```

- **2 игрока**, host/client, LAN или Unity Relay
- Host авторитетен для: врага AI, игровых событий (win/lose), спавна объектов
- Движение игрока: **owner-authoritative** (`NetworkTransform` с `AuthorityMode = Owner`)
- Эхо-события: клиент детектирует звук → `ServerRpc` → host валидирует → `ClientRpc` всем

### Сетевые объекты

| Объект | Спавн | Авторитет |
|---|---|---|
| Player Prefab | NGO автоматически при подключении | Owner (движение), Server (состояние) |
| Enemy | Host при старте игры | Server (AI, позиция) |
| Эхо-события | По RPC (не NetworkObject) | Server (валидация) |
| Ambient Sources | Размещены в сцене | Не сетевые (одинаковы на обоих клиентах) |

### NetworkManager

**Файл:** `Scripts/Network/GameNetworkManager.cs`

- Настройка NGO, Player Prefab, спавн-точки
- Простое LAN-лобби: Host / Join (IP-адрес)
- На этапе прототипа — без Unity Relay (только LAN / localhost)

---

## 2. Голос → Эхо

### Принцип

Каждый клиент захватывает свой микрофон через `UnityEngine.Microphone`. Аудио анализируется локально (RMS громкости). Когда громкость превышает порог → генерируется эхо-волна от позиции игрока.

### Поток данных

```
[Микрофон игрока] (локально)
       ↓
[MicrophoneCapture] — Microphone.Start() → AudioClip → читаем samples
       ↓
[VoiceAnalyzer] — RMS / peak detection каждые N мс
       ↓
  громкость > порог?
       ↓ да
[PlayerEchoLocator] — SendEchoServerRpc(position, intensity)
       ↓
[Host: EchoManager] — валидация, добавление в массив
       ↓
[EchoManager] — SpawnEchoClientRpc(position, intensity, color)
       ↓
[Все клиенты: EchoManager] — спавн визуального эхо (Point Light / шейдер)
```

### MicrophoneCapture

**Файл:** `Scripts/Player/MicrophoneCapture.cs` (MonoBehaviour, локальный)

```csharp
// Ключевая логика:
// 1. Microphone.Start(deviceName, loop: true, lengthSec: 1, frequency: 44100)
// 2. Каждый кадр/интервал: audioClip.GetData(samples, offset)
// 3. RMS = sqrt(sum(samples[i]^2) / count)
// 4. Если RMS > threshold → callback
```

- Работает **только на owner-клиенте** (не на remote player)
- Порог громкости настраивается через ScriptableObject
- Минимальный интервал между эхо-событиями (cooldown ~0.3с) чтобы не спамить

### Голосовой чат (передача голоса между игроками)

Для прототипа: **внешний войс-чат (Discord)**. Микрофон захватывается в Unity только для детекции громкости, не для передачи звука.

Для продакшена: **Vivox** (Unity Gaming Services) — бесплатный, интегрирован в Unity, поддерживает proximity-based voice.

---

## 3. Эхолокация — визуализация

### Варианты технической реализации

(Подходы A/B/C сохраняются, выбор не зависит от мультиплеера)

### Подход A: Динамические Point Light ★ (текущий для прототипа)

При эхо-событии спавним временный Point Light с анимацией range и затуханием intensity.

**Плюсы:** ✅ Простейшая реализация, ✅ стандартные URP Lit материалы, ✅ автоматические тени
**Минусы:** ❌ Нет кольца, ❌ много Light = просадка, ❌ визуально «фонарик»
**Сложность:** ⭐

### Подход B: Light + Emission пульс (гибрид)

Point Light + Emission-анимация через MaterialPropertyBlock. Объекты «загораются» при прохождении фронта.

**Плюсы:** ✅ Эффект бегущей волны per-object, ✅ без кастомных шейдеров
**Минусы:** ❌ Per-object а не per-pixel, ❌ CPU bound на много объектов
**Сложность:** ⭐⭐

### Подход C: Screen-space пост-процессинг (URP Renderer Feature)

Полноэкранный шейдер: depth/normals → world position → ring + edge detection.

**Плюсы:** ✅ Лучший визуал, ✅ per-pixel кольцо, ✅ screen-space производительность
**Минусы:** ❌ HLSL шейдер + Renderer Feature
**Сложность:** ⭐⭐⭐

### Сравнение

| Критерий | A: Point Light ★ | B: Emission | C: Post-Process |
|---|---|---|---|
| Сложность | ⭐ | ⭐⭐ | ⭐⭐⭐ |
| Визуал | Низкий | Средний | Высокий |
| Эффект кольца | ❌ | ⚠️ Per-object | ✅ Per-pixel |
| Годится для финала | Нет | Частично | Да |

### Выбранный подход

> **Подход A (Point Light)** для прототипа. Переход на C после подтверждения геймплея.

---

## 4. Враг

### EnemyAI

**Файл:** `Scripts/Enemy/EnemyAI.cs` (NetworkBehaviour, server-authoritative)

- Выполняется **только на host** (`if (!IsServer) return;`)
- **NavMeshAgent** для навигации
- Синхронизация позиции: `NetworkTransform`
- Сам создаёт эхо при движении (шаги врага = красное эхо → игроки «видят» врага)

### Поведение

```
Состояния:
  [Patrol] → ходит по заданным точкам
       ↓ слышит звук (эхо-событие в радиусе)
  [Investigate] → идёт к источнику звука
       ↓ видит/достигает игрока
  [Chase] → преследует, создаёт много шума (красное эхо)
       ↓ потерял игрока
  [Search] → осматривает район
       ↓ таймаут
  [Patrol]
```

### Как враг «слышит»

`EchoManager` на host при каждом эхо-событии нотифицирует `EnemyAI`:
- Вычисляет расстояние от врага до источника звука
- Если расстояние < `hearingRange` → `EnemyAI.OnSoundHeard(position, intensity)`
- Более громкие звуки слышны дальше

---

## 5. Компоненты системы

### EchoManager (NetworkBehaviour)

**Файл:** `Scripts/Core/EchoManager.cs`

```csharp
public struct EchoSourceData : INetworkSerializable
{
    public Vector3 Position;
    public float StartTime;
    public float Speed;
    public float MaxRadius;
    public float Intensity;
    public Color Color;
}
```

**Обязанности:**
- Хранит массив активных эхо-источников (max 16)
- Принимает `SpawnEchoServerRpc()` → валидирует → `SpawnEchoClientRpc()`
- На каждом клиенте: обновляет радиусы, удаляет истёкшие, управляет визуализацией
- На host: нотифицирует EnemyAI о звуках

### EchoSource (MonoBehaviour, локальный)

**Файл:** `Scripts/Echo/EchoSource.cs`

Компонент для ambient-источников (капающая вода, вентиляция):
- Не сетевой — одинаков на обоих клиентах
- Регистрирует себя в EchoManager по таймеру
- Настраиваемые параметры через EchoPreset

### PlayerController (NetworkBehaviour)

**Файл:** `Scripts/Player/PlayerController.cs`

Сетевой FPS-контроллер:
- `NetworkTransform` (AuthorityMode = Owner) для owner-authoritative движения
- CharacterController + камера (обзор)
- Генерирует эхо от шагов (ServerRpc)
- Только owner обрабатывает ввод и микрофон

### PlayerEchoLocator (NetworkBehaviour)

**Файл:** `Scripts/Player/PlayerEchoLocator.cs`

- Активное эхо (ЛКМ): `SendEchoServerRpc(position, activePreset)`
- Пассивное эхо (шаги): автоматически при ходьбе
- Голосовое эхо: получает callback от MicrophoneCapture → `SendEchoServerRpc(position, voiceIntensity)`

### MicrophoneCapture (MonoBehaviour, локальный)

**Файл:** `Scripts/Player/MicrophoneCapture.cs`

- Работает только на owner-клиенте
- `Microphone.Start()` → `AudioClip.GetData()` → RMS → callback
- Cooldown между эхо-событиями

---

## 6. Поток данных (полный)

```
[Микрофон / Клик / Шаги]   (на owner-клиенте)
         ↓
[PlayerEchoLocator] → SendEchoServerRpc(pos, intensity, color)
         ↓
[Host: EchoManager] 
  ├─→ Валидация (лимит 16, cooldown)
  ├─→ EnemyAI.OnSoundHeard(pos, intensity)  ← враг «слышит»
  └─→ SpawnEchoClientRpc(pos, intensity, color)
         ↓
[Все клиенты: EchoManager]
  └─→ Создать визуальное эхо (Point Light / шейдер)
         ↓
[Экран] — чёрный фон + визуализация геометрии в зоне эхо-волн
```

## 7. Аудио

- `AudioSource.Play()` синхронизирован с эхо-событиями
- Звуки воспроизводятся **локально на каждом клиенте** (не по сети)
- Каждый клиент получает ClientRpc с позицией → создаёт AudioSource.PlayClipAtPoint
- 3D sound settings: spatial blend = 1, rolloff = logarithmic
