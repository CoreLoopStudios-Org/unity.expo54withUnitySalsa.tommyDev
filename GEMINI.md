# Project: Unity-Expo Avatar (DaberAI)

## Context
This project is a **Unity as a Library (UaaL)** module embedded in an **Expo 54 (React Native)** application. It features a realistic 3D avatar with high-fidelity lip-sync and Animator-based animations for a Hebrew language learning app.

## Tech Stack
- **Engine:** Unity 6 (6000.5.0a8)
- **Render Pipeline:** URP (Universal Render Pipeline)
- **Lip-Sync:** SALSA LipSync Suite (Primary) + uLipSync (Secondary/Reference)
- **Avatar:** Ready Player Me (GLB format via glTFast)
- **Communication:** JSON-based bridge via `SalsaAvatarController.cs`
- **Target Platforms:** iOS and Android

## Core Components
- `SalsaAvatarController.cs`: Central hub for RN messages and audio management. Uses Animator triggers for body animations (wave, nod, talk, idle).
- `SalsaLipSyncTester.cs`: Editor-only GUI for rapid testing.

## High-Level Mandates
1. **Performance:** The avatar runs inside a chat screen. Body animations are handled via the Animator Controller for better state management.
2. **Compatibility:** Maintain the `SalsaAvatarController` bridge. Any new features must be triggerable via the `ReceiveMessage(string json)` method.
3. **Lip-Sync Quality:** Prioritize SALSA's amplitude-based analysis for ElevenLabs audio.
4. **Manual Assignment:** For optimization and explicit control, components (Salsa, Eyes, Emoter, Animator) are assigned manually in the `SalsaAvatarController` Inspector.

## Architectural Patterns
- **Animator State Machine:** Use triggers (`wave`, `nod`, `talk`, `idle`) to transition between animations. The state machine must automatically return to the default 'Idle' state.
- **Async Audio:** Use Coroutines/Tasks for audio loading to prevent UI blocking during the RN bridge calls.
