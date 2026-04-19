# Project: Unity-Expo Avatar (DaberAI)

## Context
This project is a **Unity as a Library (UaaL)** module embedded in an **Expo 54 (React Native)** application. It features a realistic 3D avatar with high-fidelity lip-sync and procedural animations for a Hebrew language learning app.

## Tech Stack
- **Engine:** Unity 6 (6000.5.0a8)
- **Render Pipeline:** URP (Universal Render Pipeline)
- **Lip-Sync:** SALSA LipSync Suite (Primary) + uLipSync (Secondary/Reference)
- **Avatar:** Ready Player Me (GLB format via glTFast)
- **Communication:** JSON-based bridge via `SalsaAvatarController.cs`
- **Target Platforms:** iOS and Android

## Core Components
- `SalsaAvatarController.cs`: Central hub for RN messages and audio management.
- `AvatarAnimations.cs`: Procedural bone overrides for rest pose and gestures.
- `SalsaLipSyncTester.cs`: Editor-only GUI for rapid testing.

## High-Level Mandates
1. **Performance:** The avatar runs inside a chat screen. Keep the footprint light; use procedural animations over heavy FBX clips where possible.
2. **Compatibility:** Maintain the `SalsaAvatarController` bridge. Any new features must be triggerable via the `ReceiveMessage(string json)` method.
3. **Lip-Sync Quality:** Prioritize SALSA's amplitude-based analysis for ElevenLabs audio.
4. **Latency:** Future updates must support WebSocket audio streaming for real-time LLM interactions.

## Architectural Patterns
- **LateUpdate Overrides:** Procedural animations must happen in `LateUpdate` to override the Animator's default state.
- **Async Audio:** Use Coroutines/Tasks for audio loading to prevent UI blocking during the RN bridge calls.
