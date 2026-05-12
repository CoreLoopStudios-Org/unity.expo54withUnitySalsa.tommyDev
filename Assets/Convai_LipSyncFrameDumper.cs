// Convai_LipSyncFrameDumper.cs
// Drop this onto the GameObject that has ConvaiLipSyncComponent (or anywhere in
// the active scene). It subscribes to the SDK event hub the moment the runtime
// brings the lipsync module online and writes every received chunk to a JSONL
// file under Application.persistentDataPath. One JSON object per line — open
// it with any text editor or pipe through `jq` to inspect the raw NeuroSync
// frames that the Convai backend is streaming to your character.
//
// Each line:
//   {
//     "ts": "2026-05-12T09:12:34.567Z",
//     "characterId": "abc123",
//     "participantId": "PA_xyz",
//     "profileId": "arkit",
//     "frameRate": 60.0,
//     "channelCount": 60,
//     "frameCount": 12,
//     "channels": ["EyeBlinkLeft", ..., "RightEyeRoll"],
//     "frames":   [[...60 floats...], ...]
//   }
//
// Stats packets land in the same file as type "blendshape-turn-stats" so you
// can verify "Server: N frames | Received: N frames | Match: YES".
//
// Verified against com.convai.convai-sdk-for-unity@4.1.0.
// SAFE TO LEAVE IN DEBUG BUILDS ONLY — guard or strip for production.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using Convai.Domain.DomainEvents.LipSync;
using Convai.Domain.EventSystem;
using Convai.Runtime.Components;
using Convai.Runtime.Core; // ConvaiManager.ActiveManager
using UnityEngine;

namespace DaberDebug
{
    /// <summary>
    /// Captures every Convai NeuroSync chunk + viseme packet + end-of-turn stat
    /// to a JSONL file you can open and inspect. Mobile-safe (no allocations in
    /// the hot path beyond what JSON-serialization requires).
    /// </summary>
    [DefaultExecutionOrder(-1000)]
    public sealed class ConvaiLipSyncFrameDumper : MonoBehaviour
    {
        [Tooltip("If empty, defaults to Application.persistentDataPath. Use a writable folder on device.")]
        [SerializeField] private string _outputDirectory = "";

        [Tooltip("Include the full per-frame float arrays. Disable for slim turn-only summaries.")]
        [SerializeField] private bool _dumpFrames = true;

        [Tooltip("Also capture low-fidelity viseme dictionaries (the 15-channel legacy stream).")]
        [SerializeField] private bool _dumpVisemes = true;

        [Tooltip("Also capture end-of-turn blendshape stats from the server.")]
        [SerializeField] private bool _dumpTurnStats = true;

        private IEventHub _hub;
        private SubscriptionToken _packedToken;
        private SubscriptionToken _visemesToken;
        private SubscriptionToken _statsToken;
        private string _filePath;
        private StreamWriter _writer;
        private readonly object _writeLock = new();
        private int _chunkCount;
        private int _frameCountThisSession;

        private void OnEnable()
        {
            TryStart();
        }

        private void Update()
        {
            // Lazily attach — ConvaiManager may not have published its event hub yet at Awake.
            if (_hub == null) TryStart();
        }

        private void OnDisable()
        {
            DetachAndClose();
        }

        private void OnDestroy()
        {
            DetachAndClose();
        }

        private void TryStart()
        {
            if (_hub != null) return;

            var manager = ConvaiManager.ActiveManager;
            if (manager == null) return;

            if (!manager.TryGetEventHub(out _hub)) return;

            OpenFile();
            _packedToken = _hub.Subscribe<LipSyncPackedDataReceived>(OnPacked);
            if (_dumpVisemes)
                _visemesToken = _hub.Subscribe<VisemesReceived>(OnVisemes);
            if (_dumpTurnStats)
                _statsToken = _hub.Subscribe<BlendshapeTurnStatsReceived>(OnTurnStats);

            Debug.Log($"[LipSyncFrameDumper] Capturing to {_filePath}");
        }

        private void OpenFile()
        {
            string dir = string.IsNullOrWhiteSpace(_outputDirectory)
                ? Application.persistentDataPath
                : _outputDirectory;
            Directory.CreateDirectory(dir);
            string stamp = DateTime.UtcNow.ToString("yyyyMMdd_HHmmss", CultureInfo.InvariantCulture);
            _filePath = Path.Combine(dir, $"convai_lipsync_{stamp}.jsonl");
            _writer = new StreamWriter(_filePath, append: false, Encoding.UTF8) { AutoFlush = true };
        }

        private void OnPacked(LipSyncPackedDataReceived evt)
        {
            if (!evt.IsValid) return;
            _chunkCount++;
            _frameCountThisSession += evt.FrameCount;

            var sb = new StringBuilder(1024 + (_dumpFrames ? evt.FrameCount * 60 * 8 : 0));
            sb.Append('{');
            AppendField(sb, "kind", "lipsync-packed", true);
            AppendField(sb, "ts", DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture));
            AppendField(sb, "characterId", evt.CharacterId);
            AppendField(sb, "participantId", evt.ParticipantId);
            AppendField(sb, "profileId", evt.Chunk.ProfileId.Value);
            AppendNumber(sb, "frameRate", evt.Chunk.FrameRate);
            AppendNumber(sb, "channelCount", evt.Chunk.ChannelNames.Count);
            AppendNumber(sb, "frameCount", evt.FrameCount);
            AppendNumber(sb, "durationSeconds", evt.Duration);
            AppendChannels(sb, evt.Chunk.ChannelNames);
            if (_dumpFrames)
                AppendFrames(sb, evt.Chunk.Frames);
            sb.Append('}');

            WriteLine(sb.ToString());
        }

        private void OnVisemes(VisemesReceived evt)
        {
            var sb = new StringBuilder(512);
            sb.Append('{');
            AppendField(sb, "kind", "visemes", true);
            AppendField(sb, "ts", DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture));
            AppendField(sb, "characterId", evt.CharacterId);
            AppendField(sb, "participantId", evt.ParticipantId);
            sb.Append(",\"visemes\":{");
            bool first = true;
            foreach (var kv in evt.Visemes)
            {
                if (!first) sb.Append(',');
                first = false;
                sb.Append('"').Append(Escape(kv.Key)).Append("\":")
                  .Append(kv.Value.ToString("R", CultureInfo.InvariantCulture));
            }
            sb.Append('}');
            sb.Append('}');
            WriteLine(sb.ToString());
        }

        private void OnTurnStats(BlendshapeTurnStatsReceived evt)
        {
            var sb = new StringBuilder(512);
            sb.Append('{');
            AppendField(sb, "kind", "blendshape-turn-stats", true);
            AppendField(sb, "ts", DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture));
            AppendField(sb, "characterId", evt.CharacterId);
            AppendField(sb, "participantId", evt.ParticipantId);
            AppendNumber(sb, "totalBlendshapes", evt.TotalBlendshapes);
            AppendNumber(sb, "receivedBlendshapeFrames", evt.ReceivedBlendshapeFrames);
            AppendNumber(sb, "totalAudioBytes", evt.TotalAudioBytes);
            AppendNumber(sb, "totalTurnDurationMs", evt.TotalTurnDurationMs);
            AppendNumber(sb, "totalAudioDurationMs", evt.TotalAudioDurationMs);
            AppendNumber(sb, "fps", evt.Fps);
            AppendField(sb, "match", evt.FrameCountMatches ? "YES" : "NO");
            sb.Append('}');
            WriteLine(sb.ToString());
        }

        private void WriteLine(string json)
        {
            lock (_writeLock)
            {
                if (_writer == null) return;
                _writer.WriteLine(json);
            }
        }

        private void DetachAndClose()
        {
            if (_hub != null)
            {
                if (_packedToken != default) _hub.Unsubscribe(_packedToken);
                if (_visemesToken != default) _hub.Unsubscribe(_visemesToken);
                if (_statsToken != default) _hub.Unsubscribe(_statsToken);
                _packedToken = default;
                _visemesToken = default;
                _statsToken = default;
            }
            _hub = null;

            lock (_writeLock)
            {
                if (_writer != null)
                {
                    _writer.Flush();
                    _writer.Dispose();
                    _writer = null;
                }
            }

            if (_chunkCount > 0)
                Debug.Log($"[LipSyncFrameDumper] Closed. Wrote {_chunkCount} chunks / {_frameCountThisSession} frames → {_filePath}");
        }

        // ------------- tiny zero-dependency JSON writer (no Newtonsoft pull-in needed) ----

        private static void AppendField(StringBuilder sb, string key, string value, bool isFirst = false)
        {
            if (!isFirst) sb.Append(',');
            sb.Append('"').Append(key).Append("\":\"").Append(Escape(value)).Append('"');
        }

        private static void AppendNumber(StringBuilder sb, string key, double value)
        {
            sb.Append(",\"").Append(key).Append("\":")
              .Append(value.ToString("R", CultureInfo.InvariantCulture));
        }

        private static void AppendNumber(StringBuilder sb, string key, int value)
        {
            sb.Append(",\"").Append(key).Append("\":").Append(value);
        }

        private static void AppendChannels(StringBuilder sb, IReadOnlyList<string> names)
        {
            sb.Append(",\"channels\":[");
            for (int i = 0; i < names.Count; i++)
            {
                if (i > 0) sb.Append(',');
                sb.Append('"').Append(Escape(names[i])).Append('"');
            }
            sb.Append(']');
        }

        private static void AppendFrames(StringBuilder sb, float[][] frames)
        {
            sb.Append(",\"frames\":[");
            for (int f = 0; f < frames.Length; f++)
            {
                if (f > 0) sb.Append(',');
                sb.Append('[');
                var row = frames[f];
                for (int c = 0; c < row.Length; c++)
                {
                    if (c > 0) sb.Append(',');
                    sb.Append(row[c].ToString("R", CultureInfo.InvariantCulture));
                }
                sb.Append(']');
            }
            sb.Append(']');
        }

        private static string Escape(string s)
        {
            if (string.IsNullOrEmpty(s)) return string.Empty;
            // minimal escape — keys/IDs are ASCII; values may contain quotes/slashes
            return s.Replace("\\", "\\\\").Replace("\"", "\\\"");
        }
    }
}
