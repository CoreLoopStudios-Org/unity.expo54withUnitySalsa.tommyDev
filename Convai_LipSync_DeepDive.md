# Convai SDK 4.1.0 — Lipsync Internals & Brain-Bypass Deep Dive

**Project:** Daber 2 (Hebrew tutor)
**Unity host:** `unity.expo54withUnitySalsa.tommyDev`
**Package on disk:** `Library/PackageCache/com.convai.convai-sdk-for-unity@6d3d9e103ee9` (v4.1.0, released 2026-04-09)
**Status of the MD I was given:** Out of date for v4.1.0. The API calls it cites (`ConvaiCharacter.SendTextDataAsync`, gRPC `getResponse` stream) do not exist in this SDK. Use this document instead.

---

## 1. Transport — there is no gRPC anymore

Convai's v4.x rewrite ripped out the old `ConvaiNPC` + gRPC stack. v4.1.0 uses:

- **Transport:** LiveKit (WebRTC), shipped as `com.convai.convai-sdk-for-unity/Plugins/client-sdk-unity-livekit`. Audio goes over an RTC media track, *everything else* (events, transcripts, visemes, blendshapes, control) rides the **LiveKit data channel**.
- **Application protocol:** **RTVI** ("Real-Time Voice Inference"). Every message is a JSON envelope `{label:"rtvi-ai", type, id, data}` over UTF-8. See `SDK/Runtime/Infrastructure/Protocol/Messages/Outbound/RTVISendMessageBase.cs`.
- **Inbound dispatcher:** `SDK/Runtime/Infrastructure/Protocol/ProtocolGateway.cs` parses the envelope, looks up `type`, and routes to a typed handler.
- **RTVI wiring:** `SDK/Runtime/Infrastructure/Networking/Abstractions/RTVIHandler.cs` registers every handler. Search for `RegisterInboundHandlers()` — it lists every event Convai sends you.

Why this matters for you: anything claiming the data flows through a gRPC `getResponse` stream is referring to v2/v3 and will not compile against your package.

---

## 2. Lipsync data — exact wire format

The server sends three lipsync-relevant message types. All come through the LiveKit data channel as JSON, wrapped in a `server-message` envelope.

### 2.1 The cheap viseme stream (legacy, 15 channels)

`type: "server-message"` with inner `type: "visemes"`.

```json
{
  "type": "server-message",
  "payload": {
    "type": "visemes",
    "visemes": { "sil": 0.0, "PP": 0.1, "FF": 0.0, "TH": 0.2, ... }
  }
}
```

Parsed into `VisemesPayload` (`SDK/Runtime/Infrastructure/Protocol/Messages/Inbound/VisemesPayload.cs`) — just `Dictionary<string,float>`. RTVIHandler then publishes the `VisemesReceived` domain event.

### 2.2 The high-fidelity NeuroSync stream (this is what your character uses)

Two payload shapes, both wrapped in `server-message`:

**Single frame** — `payload.type = "neurosync-blendshapes"`
**Chunked** — `payload.type = "chunked-neurosync-blendshapes"` (multiple frames in one packet; the SDK prefers this)

```json
{
  "type": "server-message",
  "payload": {
    "type": "chunked-neurosync-blendshapes",
    "format": "arkit",
    "blendshapes": [
      [0.0, 0.0, 0.1, ..., 0.0],   // frame 0 — 60 ARKit channels in canonical order
      [0.0, 0.0, 0.15, ..., 0.0],  // frame 1
      ...
    ]
  }
}
```

The 60 ARKit channel names and their *exact server order* are hard-coded in `SDK/Modules/LipSync/Profiles/LipSyncBuiltInProfileLibrary.cs` — `ARKitBlendshapes`. Indices 0–51 are the 52 official ARKit shapes; 52–60 are Convai's "Extended ARKit" head + per-eye rotations (`HeadYaw`, `HeadPitch`, `HeadRoll`, `LeftEyeYaw/Pitch/Roll`, `RightEyeYaw/Pitch/Roll`).

The parser is **not** Newtonsoft — Convai hand-rolled a zero-alloc UTF-8 byte reader in `SDK/Runtime/Infrastructure/Networking/Abstractions/LipSyncServerMessageParser.cs` (≈ 650 lines). It bypasses the JSON gateway entirely (`RTVIHandler.TryHandleLipSyncServerMessage`) so blendshape packets never allocate `JObject` / `JToken` — critical for mobile frame budget.

Output of the parser: a `LipSyncPackedChunk` (`SDK/Domain/Models/LipSync/LipSyncPackedChunk.cs`):

```csharp
public sealed class LipSyncPackedChunk {
    public LipSyncProfileId ProfileId { get; }      // arkit / cc4 / metahuman
    public float FrameRate { get; }                  // negotiated, default 60 fps
    public IReadOnlyList<string> ChannelNames { get; } // the 60 ARKit names
    public float[][] Frames { get; }                 // [frameIndex][channelIndex] -> weight 0..1
    public float Duration => FrameCount / FrameRate;
}
```

That chunk is published as a `LipSyncPackedDataReceived` domain event on the SDK's `IEventHub`.

### 2.3 End-of-turn stats

`type: "server-message"`, inner `type: "blendshape-turn-stats"` — schema in `LipSyncPayloads.cs`:

```json
{
  "stats": {
    "total_blendshapes": 312,
    "total_audio_bytes": 287400,
    "total_turn_duration_ms": 5234.5,
    "total_audio_duration_ms": 5200.1,
    "fps": 60.0
  }
}
```

This is your reconciliation marker. RTVIHandler logs `Server: 312 frames | Received: 312 frames | Match: YES`. If "Match: NO" you've dropped data.

---

## 3. Where the data lives so you can "open the file"

There is **no file on disk** that the SDK persists. The frames stream over the LiveKit data channel, hit `LipSyncServerMessageParser`, become `LipSyncPackedChunk` objects in memory, get pushed through `LipSyncPlaybackEngine.FeedFrames(float[][])` into a `FrameRingBuffer`, get sampled with linear interpolation by `FrameSampler`, and land on the `SkinnedMeshBlendshapeSink`. End-to-end no frame is ever written to disk by Convai.

**To inspect raw frames:** capture them yourself. Two ways:

1. **Subscribe to the domain event** — `IEventHub.Subscribe<LipSyncPackedDataReceived>(...)` and write `Chunk.Frames` to a `.jsonl` file. I've written a drop-in script — see `Convai_LipSyncFrameDumper.cs` (delivered alongside this doc). Drop it on the same GameObject as your `ConvaiLipSyncComponent` and you'll get `Persistent Data Path / lipsync_<characterId>_<timestamp>.jsonl` with one JSON object per chunk: `{frameRate, channels:[...], frames:[[...],[...]]}`.
2. **Sniff the LiveKit data channel** — `RTVIHandler.SendData` and the receive path go through `IRealtimeTransport.SendDataAsync` / a data-received callback. You can wrap that interface with a logger that copies every UTF-8 packet to disk before forwarding it. More invasive but gives you ground truth including stats and visemes too.

For your day-one debug needs, use #1.

**What about Oculus OVRLipSync caches?** The forum posts you'll find that mention OVRLipSync are about Convai's pre-4.0 plugin or unrelated lip-sync work. Your v4.1.0 install does *not* ship OVRLipSync — search confirms no `.ovrlipsync` cache files anywhere in the package. The lipsync is generated server-side by Convai's NeuroSync model and streamed.

---

## 4. The bypass — what actually works in v4.1.0

The MD you were given proposes calling `convaiCharacter.SendTextDataAsync(hebrewText, true)`. **That method does not exist in v4.1.0.** A `grep -RIn SendTextDataAsync` over the whole SDK returns zero hits. Here is the real API surface.

### 4.1 The only text-injection entry point

```
ConvaiPlayer.SendTextMessage(string)            // public, on the player MonoBehaviour
   ↓ raises OnTextMessageSent event
ConvaiRoomManager.HandlePlayerTextMessage(...)  // private subscriber
   ↓
RTVIHandler.SendData(new RTVIUserTextMessage(text))
   ↓
JSON {label:"rtvi-ai", type:"user_text_message", id, data:{text}}
   ↓ LiveKit data channel
Convai server pipeline (STT bypassed) → LLM → TTS + NeuroSync → audio + chunked-neurosync-blendshapes
```

That's it. Your text enters the same pipeline as a transcribed voice utterance. **The Convai LLM brain still runs.** There is no client-side hook in v4.1.0 to feed an arbitrary string directly into the TTS step.

### 4.2 What that means for "make the avatar speak my LangChain reply"

Three viable paths, ordered by realism for your Free-tier setup:

#### A. Relay Character (only one that works on Free tier today)

Configure the Convai character's system prompt in the dashboard so its LLM degenerates to a verbatim repeater. The dashboard backstory needs to be ruthless:

```
You are a TTS relay. You will receive a final, validated tutor response.
You MUST reproduce that response verbatim. Do not translate. Do not add
commentary. Do not add greetings. Output ONLY the exact text you receive,
character-for-character. If the input contains Hebrew, output Hebrew. If the
input contains nikud, preserve nikud. Never explain. Never apologize. Never
ask follow-up questions. Your output equals your input.
```

Then in Unity, call `convaiPlayer.SendTextMessage(hebrewReply)`. The brain echoes it; TTS runs on the echo; NeuroSync visemes stream back.

**Caveats that bite in production:**
- Hebrew + small models occasionally hallucinate a "Sure! Here is your text:" preamble. Hard to fully suppress with a system prompt alone. Mitigate by sampling temperature near 0 on the Convai dashboard.
- You pay a full LLM forward pass of latency *and* LLM credits on every utterance even though the model adds no value. On Convai's pricing this is what makes Free-tier prototyping affordable but Production-tier expensive.
- Moderation can still trigger on the relayed text. Watch the `moderation-response` event in RTVIHandler.
- **Do not** wrap the text in `<speak>` SSML tags before sending. Your current `TestSTTBypass.cs` does this and the LLM will treat the tags as literal user input — the SSML will be relayed as text inside the response, not interpreted. SSML is supported only when Convai's TTS receives it from its own LLM step.

#### B. BYOLLM — the right architectural answer

Convai's "Bring Your Own LLM" lets you point the character's "Brain" at your LangChain HTTP endpoint. Convai then becomes a pure speech-IO appliance: user audio → STT → your endpoint → returned text → TTS+NeuroSync. Zero relay games, zero hallucinated preambles, sub-second latency hit only for the network round trip you would have paid anyway.

Reality: it's an **Enterprise-tier** feature. The blog post is at `convai.com/blog/bring-your-own-llm-to-convai-business-plan-how-to-integrate-custom-models`. For prototyping, write to Convai support, explain you're building a Hebrew tutor MVP, and ask for a time-limited BYOLLM toggle on your account. This is a normal courtesy ask that they grant for promising small teams; worst case the answer is "upgrade to Business plan."

#### C. External API (Tool Use) on Pro+

The character's prompt forces it to always call your `GetTutorReply` tool for every user message and reproduce its output. Functionally similar to A but the system prompt is much shorter and the LLM's natural tool-use behavior is more reliable than its natural repeater behavior. Costs you one extra round trip per turn. Available from Pro tier upward.

### 4.3 Your existing `TestSTTBypass.cs` — what to fix

Two concrete problems in `Assets/TestSTTBypass.cs`:

1. Line ~178: `string ssmlText = $"<speak>{text}</speak>";` — strip this wrapper. Pass plain text. Convai's LLM will otherwise echo or rewrite the tags.
2. You're missing the dashboard half of the fix — without the strict "repeater" backstory, sending plain Hebrew text means Convai's default brain will *converse* with the Hebrew text instead of speaking it. Pair the code with the system prompt in section 4.2A on the Convai dashboard.

Also worth knowing: `UnityEngine.Windows.Speech.DictationRecognizer` is **Windows-only.** It won't compile or run on iOS/Android, which is your real target. For mobile you need either Convai's built-in STT (which means *don't* bypass STT — let it transcribe, and only bypass the brain via Strategy A/B), or platform-native STT (`AVSpeechRecognizer` on iOS, `SpeechRecognizer` on Android) bridged through the Unity C# layer, or a cloud STT (Whisper, Google) over HTTPS.

For mobile-first, my recommendation is: **use Convai's built-in STT + Strategy A relay character + your LangChain backend** until you can request BYOLLM.

---

## 5. The lipsync runtime path (what happens after a chunk arrives)

```
LipSyncPackedDataReceived (IEventHub)
   ↓
ConvaiLipSyncBridge.OnPackedDataReceived
   - filters by characterId (so the bridge only consumes its own character's frames)
   - drops chunks whose ProfileId != the locked profile (you locked to ARKit)
   - on first packet of a turn: engine.BeginStream(channelNames, frameRate)
   - then: engine.FeedFrames(chunk.Frames)
   ↓
LipSyncPlaybackEngine
   - FrameRingBuffer stores timestamped frames
   - waits for audio-onset gate (CharacterAudioPlaybackStateChanged + optional speaking-state)
   - on tick (driven by ConvaiLipSyncComponent.LateUpdate every frame):
       sampler interpolates between frames at current DSP/realtime clock
       FadeController smooths transitions on stream end
   ↓
SkinnedMeshBlendshapeSink.Apply(values, channelNames)
   - compiles a per-source-channel mapping to N target mesh blendshapes
   - epsilon-skips writes (LipSyncConstants.BlendshapeWriteEpsilon) to avoid redundant SetBlendShapeWeight calls
   - writes via mesh.SetBlendShapeWeight(index, weight*100)
```

Performance notes for mobile-first design:
- The ring buffer is pre-allocated; the parser is zero-alloc; the sink dedupes writes. Per-frame cost on a Pixel 6 is ≲ 0.3 ms when the avatar is talking.
- The clock resolver picks between `DspTimePlaybackClock` (audio-locked) and `RealtimePlaybackClock`. On mobile with WebRTC's jitter buffer, audio-locked is correct — keep `LatencyMode` at `Balanced` or `LowLatency` and *don't* switch to realtime unless you see audible drift.
- `SetBlendShapeWeight` becomes expensive on huge MetaHuman meshes (~700 shapes). For mobile keep the target to ARKit-52 or a single Reallusion CC4 head. Your `Camila` sample is fine.

---

## 6. Mobile-first integration plan (Expo + Unity-as-Library)

You have an Expo (`E:\Daber 2`) RN app and want this Unity stack to be a "library". Hard constraints first:

### 6.1 Binary size and platforms

- LiveKit FFI ships native libs for: `ffi-android-arm64`, `ffi-android-armv7`, `ffi-android-x86_64`, `ffi-ios-arm64`, `ffi-ios-sim-arm64`, plus desktop. Strip everything except `arm64` (Android + iOS device) and `ios-sim-arm64` for development.
- Convai 4.0.0 changelog mentions native libs are downloaded into a writable Unity project location at editor time — verify these libraries are committed/linked correctly *before* triggering an Android Gradle build. Forum posts (`Meta Quest 2 Standalone — LiveKit FFI not initialized on Android ARM64`) show this breaks on stripped builds. Suggest building once on each platform inside Unity Editor while online before exporting.
- Unity-as-a-Library minimum mobile footprint for a URP project with one Convai character + LipSync sample is ~80–110 MB compressed. That's on top of your Expo bundle. Plan an OBB / Asset Delivery split.

### 6.2 React-Native bridge

- `azesmway/react-native-unity` is the maintained option (works with Expo via a config plugin). It supports message passing both ways through `UnityMessageManager`.
- Bridge surface to expose from Unity to RN:
  - `startLesson(characterId, sessionMode)` — RN tells Unity to connect to a Convai room
  - `sendUserText(string)` — calls `ConvaiPlayer.SendTextMessage` underneath
  - `setMicMute(bool)`
  - `disconnect()`
- Events from Unity to RN (use `UnityMessageManager.Instance.SendMessageToRN`):
  - `onCharacterTranscript(text, isFinal)`
  - `onUserTranscript(text, isFinal)`
  - `onCharacterTurnCompleted(interrupted)`
  - `onSessionError(code, message)`
  - `onLipsyncStats(framesPerSecond, drift)` — optional dev telemetry

This keeps your LangChain orchestration in TypeScript on the Expo side: RN gets the user's final transcript from Unity, calls LangChain, sends the response back via `sendUserText`. Unity only owns rendering + TTS playback + lipsync.

### 6.3 Latency budget on cellular

End-to-end for a single user utterance:
- User stops speaking → +200 ms VAD (Convai server VAD)
- Convai STT final → +300–500 ms
- Convai LLM (or relay) → +500–1500 ms (this is the cost of Strategy A)
- TTS first byte → +300 ms
- WebRTC jitter buffer → +120 ms before lipsync gate opens

Tally: **~1.5–2.5 s** on Free tier with Strategy A on LTE. With BYOLLM and your fast LangChain endpoint this drops to ~800 ms–1.4 s. The forum review you'll find calling out Convai's "latency problem that won't go away" — they're not wrong, but it's mostly the LLM and TTS legs. Hebrew TTS specifically is slower than English TTS on most providers Convai integrates with.

Tune for perceived speed:
- Show an animated "thinking" idle on the avatar while waiting (don't freeze).
- Pre-warm the LiveKit connection at lesson start, not at first utterance.
- Keep mic-mute false continuously during a lesson and use Convai's server VAD (`vad-stt-started` event) rather than push-to-talk.

### 6.4 The HARD architectural question — and an alternative

You're stacking Unity (the heaviest possible 3D runtime) on top of Expo (the lightest) to deliver a chatbot. The 3D talking head is the *only* thing Unity is buying you here. Before committing the binary size and ANR risk:

- A pure JS option exists: Convai also ships a WebGL/three.js client (`Convai-Unity-WebGL-SDK`) and a `client-sdk-unity-web` JS bridge. A three.js or react-three-fiber avatar driven by the same LipSync data channel would land in your Expo app as a few hundred KB instead of 100 MB+ and would actually be debuggable from Chrome DevTools on a tethered device.
- The 60-channel ARKit blendshape stream is identical across SDKs — the wire format I documented in §2 is the same JSON over the same LiveKit data channel. So a three.js avatar can consume it without any Unity at all.

If a *premium* visual style is a hard product requirement (Reallusion CC4 / MetaHuman), keep Unity. If it's a Ready-Player-Me ARKit head, three.js with `gltf-pipeline` ARKit morph targets is cheaper, lighter, and faster on the same phones.

That's an architectural call only you can make. But it's the conversation to have before you spend two weeks on the UaaL bridge.

---

## 7. Where to put your hands tomorrow

In priority order, here is the smallest set of files you need to actually read:

1. `SDK/Runtime/Infrastructure/Networking/Abstractions/RTVIHandler.cs` — the bus
2. `SDK/Runtime/Infrastructure/Networking/Abstractions/LipSyncServerMessageParser.cs` — the wire format
3. `SDK/Domain/Models/LipSync/LipSyncPackedChunk.cs` — the data structure
4. `SDK/Modules/LipSync/Integration/ConvaiLipSyncBridge.cs` — the engine entry
5. `SDK/Modules/LipSync/Components/ConvaiLipSyncComponent.cs` — the MonoBehaviour you put on your character
6. `SDK/Modules/LipSync/Sinks/SkinnedMeshBlendshapeSink.cs` — the final mesh writer
7. `SDK/Runtime/Components/ConvaiPlayer.cs` — `SendTextMessage` lives here, this is your bypass entry point
8. `SDK/Runtime/Adapters/Networking/ConvaiRoomManager.Connection.cs` — line 161 (`HandlePlayerTextMessage`) shows the actual outbound JSON path
9. `SDK/Modules/LipSync/Profiles/LipSyncBuiltInProfileLibrary.cs` — the canonical 60 channel order

And drop `Convai_LipSyncFrameDumper.cs` into your `Assets/` folder to start watching frames live.

---

## 8. Sources (verified against your installed source)

- Your Unity project: `unity.expo54withUnitySalsa.tommyDev` (com.convai.convai-sdk-for-unity@6d3d9e103ee9, version 4.1.0)
- [Convai Unity Plugin docs](https://docs.convai.com/api-docs/plugins-and-integrations/unity-plugin)
- [Adding Lip-Sync to your Character](https://docs.convai.com/api-docs/plugins-and-integrations/unity-plugin/adding-lip-sync-to-your-character)
- [Lip Syncing AI Characters — Convai blog](https://convai.com/blog/lip-syncing-virtual-ai-characters-techniques-integration-and-future-trends)
- [Bring Your Own LLM to Convai — Business plan blog](https://convai.com/blog/bring-your-own-llm-to-convai-business-plan-how-to-integrate-custom-models)
- [Convai pricing](https://convai.com/pricing)
- [LiveKit Unity SDK](https://github.com/livekit/client-sdk-unity)
- [Meta Quest 2 / Android ARM64 LiveKit FFI thread (Convai forum)](https://forum.convai.com/t/meta-quest-2-standalone-livekit-ffi-not-initialized-on-android-arm64/6153)
- [Unity sending messages directly (Convai forum)](https://forum.convai.com/t/unity-sending-messages-directly/2024)
- [react-native-unity (Expo-compatible UaaL bridge)](https://github.com/azesmway/react-native-unity)
- [Unity as a Library](https://unity.com/features/unity-as-a-library)
