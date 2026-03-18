# Констрейнты и правила проекта

## Жёсткие констрейнты (нельзя нарушать)

### 1. Никакого постоянного света
- В дефолтном состоянии сцены **запрещены** постоянные источники света
- Environment Lighting → Ambient Color = `(0, 0, 0, 1)` (абсолютный чёрный)
- Skybox = чёрный (или отключен)
- Временные Point Light создаются **только** эхо-системой и автоматически удаляются

### 2. Визуализация = звук
- Каждая визуальная «вспышка» мира привязана к звуковому событию
- Нет звука → нет визуализации → полная темнота
- Источники звука: голос игрока (микрофон), шаги, активный пинг, враг, окружение

### 3. Мультиплеер
- **Netcode for GameObjects (NGO)** — единственный сетевой фреймворк
- Host/Client модель, **2 игрока**
- Сетевые объекты наследуют `NetworkBehaviour`
- Игровая логика (AI врага, win/lose) — **server-authoritative** (выполняется на host)
- Движение игрока — **owner-authoritative** (`NetworkTransform` с `AuthorityMode = Owner`)
- Эхо-события — через `ServerRpc` / `ClientRpc`

### 4. Рендер-пайплайн
- Только **URP** (не Built-in, не HDRP)
- Кастомные шейдеры (если нужны) — **HLSL** через URP Shader Library
- Пост-процессинг (если нужен) — через **URP Renderer Feature**

### 5. Input System
- Только **New Input System** (PlayerInput / InputAction)
- Legacy `Input.GetKey()` / `Input.GetAxis()` — запрещены

### 6. Микрофон
- Захват через `UnityEngine.Microphone` (на owner-клиенте)
- Только анализ громкости (RMS), не запись и не передача аудио по сети
- Голосовой чат — отдельная система (Vivox / внешний)

## Мягкие констрейнты (рекомендации для прототипа)

### Производительность
- Максимум **16 одновременных эхо-источников**
- Эхо-пульс живёт **2–4 секунды**, затем затухает
- Целевой FPS: **60+** на mid-range PC

### Архитектура
- NetworkBehaviour + ScriptableObject для сетевых систем
- MonoBehaviour + ScriptableObject для локальных (ambient sources, microphone)
- Управление эхо-источниками — через `EchoManager` (NetworkBehaviour)
- Компонентный подход: каждая функция = отдельный компонент

### Код
- C# стиль: `[SerializeField] private`, PascalCase public, _camelCase private
- Один класс = один файл
- RPC: `[ServerRpc]` → суффикс `ServerRpc`, `[ClientRpc]` → суффикс `ClientRpc`
- Папки Scripts/: Core, Player, Echo, Enemy, Network, Audio

## Ограничения прототипа (что НЕ делаем)

- ❌ Поддержка >2 игроков
- ❌ Dedicated server (только host/client)
- ❌ Unity Relay / Lobby Service (только LAN)
- ❌ Передача голоса по сети (используем внешний чат)
- ❌ Процедурная генерация уровней
- ❌ Сложный AI (только patrol → investigate → chase)
- ❌ Система сохранений
- ❌ UI-меню (только HUD-минимум + экран подключения)
- ❌ Поддержка платформ кроме Windows
- ❌ VR/AR
