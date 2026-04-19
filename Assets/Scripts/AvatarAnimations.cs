using UnityEngine;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// Handles body animations (wave, nod, talk gesture) for the RPM avatar.
///
/// REST POSE STRATEGY (standard Unity pattern):
///   - The Animator stays ENABLED (even without a controller)
///   - In LateUpdate (which runs AFTER the Animator), we override arm bones
///     to bring them from T-pose to a natural standing position
///   - This is identical to how IK, look-at, and procedural animation layers
///     work in every professional Unity project
///
/// PROCEDURAL ANIMATIONS:
///   - Coroutines set a target override; LateUpdate applies it
///   - All rotations are stored as ABSOLUTE targets, not deltas
///   - When no procedural animation is active, rest pose is applied
///
/// Auto-attached by AvatarController.
/// </summary>
public class AvatarAnimations : MonoBehaviour
{
    private Animator _animator;
    private bool _hasAnimatorController;

    // Cached parameter hashes
    private HashSet<int> _parameterHashes;

    // Bone references
    private Transform _rightUpperArm;
    private Transform _rightLowerArm;
    private Transform _rightHand;
    private Transform _leftUpperArm;
    private Transform _leftLowerArm;
    private Transform _headBone;
    private Transform _neckBone;

    // T-pose base rotations (captured before any modification)
    private Quaternion _tposeRightUpper;
    private Quaternion _tposeRightLower;
    private Quaternion _tposeLeftUpper;
    private Quaternion _tposeLeftLower;

    // Rest pose targets (arms at sides)
    private Quaternion _restRightUpper;
    private Quaternion _restRightLower;
    private Quaternion _restLeftUpper;
    private Quaternion _restLeftLower;

    // Current arm override (set by procedural animations or rest pose)
    private Quaternion? _overrideRightUpper;
    private Quaternion? _overrideRightLower;
    private Quaternion? _overrideRightHand;
    private Quaternion? _overrideLeftUpper;
    private Quaternion? _overrideLeftLower;

    // Procedural animation state
    private Coroutine _currentProcedural;
    public string CurrentAnimName { get; private set; }
    private bool _bonesReady;

    void Start()
    {
        _animator = GetComponentInChildren<Animator>();
        _hasAnimatorController = _animator != null && _animator.runtimeAnimatorController != null;

        if (_hasAnimatorController)
        {
            _parameterHashes = new HashSet<int>();
            foreach (var param in _animator.parameters)
                _parameterHashes.Add(param.nameHash);

            Debug.Log($"[AvatarAnimations] Animator Controller found: {_animator.runtimeAnimatorController.name}");
        }
        else
        {
            Debug.Log("[AvatarAnimations] No Animator Controller — falling back to procedural LateUpdate");
        }

        FindBones();

        // Check if we found at least the critical bones
        if (_rightUpperArm != null)
        {
            // Store the T-pose rotations (what the Animator writes each frame)
            _tposeRightUpper = _rightUpperArm.localRotation;
            _tposeRightLower = _rightLowerArm != null ? _rightLowerArm.localRotation : Quaternion.identity;
            _tposeLeftUpper = _leftUpperArm != null ? _leftUpperArm.localRotation : Quaternion.identity;
            _tposeLeftLower = _leftLowerArm != null ? _leftLowerArm.localRotation : Quaternion.identity;

            // Calculate rest pose: T-pose + correction to bring arms down
            // RPM humanoid: UpperArm Z ∓50° is a relaxed arms-at-sides pose
            _restRightUpper = _tposeRightUpper * Quaternion.Euler(8f, 0f, -50f);
            _restRightLower = _tposeRightLower * Quaternion.Euler(0f, -3f, -8f);
            _restLeftUpper = _tposeLeftUpper * Quaternion.Euler(8f, 0f, 50f);
            _restLeftLower = _tposeLeftLower * Quaternion.Euler(0f, 3f, 8f);

            _bonesReady = true;
            Debug.Log("[AvatarAnimations] Rest pose initialized successfully.");
        }
        else
        {
            Debug.LogError("[AvatarAnimations] CRITICAL: RightUpperArm bone not found! Animations will not work. Check bone names in Hierarchy.");
        }
    }

    /// <summary>
    /// Standard Unity pattern: LateUpdate runs AFTER the Animator.
    /// We override arm bones here to correct T-pose or apply procedural animation.
    /// </summary>
    void LateUpdate()
    {
        if (_hasAnimatorController || !_bonesReady) return;

        // Apply whatever override is active (rest pose or procedural)
        if (_rightUpperArm != null)
            _rightUpperArm.localRotation = _overrideRightUpper ?? _restRightUpper;
        if (_rightLowerArm != null)
            _rightLowerArm.localRotation = _overrideRightLower ?? _restRightLower;
        if (_rightHand != null && _overrideRightHand.HasValue)
            _rightHand.localRotation = _overrideRightHand.Value;
        if (_leftUpperArm != null)
            _leftUpperArm.localRotation = _overrideLeftUpper ?? _restLeftUpper;
        if (_leftLowerArm != null)
            _leftLowerArm.localRotation = _overrideLeftLower ?? _restLeftLower;
    }

    private void FindBones()
    {
        var animator = GetComponentInChildren<Animator>();
        
        // Strategy A: Humanoid Mapping (only works if 'Avatar' slot is filled)
        if (animator != null && animator.avatar != null && animator.isHuman)
        {
            Debug.Log("[AvatarAnimations] Using Humanoid Avatar mapping");
            _rightUpperArm = animator.GetBoneTransform(HumanBodyBones.RightUpperArm);
            _rightLowerArm = animator.GetBoneTransform(HumanBodyBones.RightLowerArm);
            _rightHand = animator.GetBoneTransform(HumanBodyBones.RightHand);
            _leftUpperArm = animator.GetBoneTransform(HumanBodyBones.LeftUpperArm);
            _leftLowerArm = animator.GetBoneTransform(HumanBodyBones.LeftLowerArm);
            _headBone = animator.GetBoneTransform(HumanBodyBones.Head);
            _neckBone = animator.GetBoneTransform(HumanBodyBones.Neck);
        }

        // Strategy B: Name-based search (Fallback for GLBs without an Avatar asset)
        if (_rightUpperArm == null)
        {
            Debug.Log("[AvatarAnimations] Searching hierarchy for bone names...");
            foreach (var t in GetComponentsInChildren<Transform>())
            {
                string n = t.name;
                // Ready Player Me bone naming conventions
                if (n.EndsWith("RightUpperArm")) _rightUpperArm = t;
                else if (n.EndsWith("RightLowerArm") || n.EndsWith("RightForeArm")) _rightLowerArm = t;
                else if (n.EndsWith("RightHand")) _rightHand = t;
                else if (n.EndsWith("LeftUpperArm")) _leftUpperArm = t;
                else if (n.EndsWith("LeftLowerArm") || n.EndsWith("LeftForeArm")) _leftLowerArm = t;
                else if (n.EndsWith("Head") && !n.Contains("Top")) _headBone = t;
                else if (n.EndsWith("Neck")) _neckBone = t;
            }
        }

        Debug.Log($"[AvatarAnimations] Discovery Results: \n" +
                  $"- Right Arm: {(_rightUpperArm != null ? "FOUND (" + _rightUpperArm.name + ")" : "MISSING")}\n" +
                  $"- Left Arm: {(_leftUpperArm != null ? "FOUND" : "MISSING")}\n" +
                  $"- Head: {(_headBone != null ? "FOUND" : "MISSING")}");
    }

    // ── Public API ───────────────────────────────────────────────────────

    public void PlayIdle()
    {
        if (TrySetTrigger("idle")) return;
        StopProcedural();
    }

    public void PlayWave()
    {
        if (TrySetTrigger("wave")) return;
        StopProcedural();
        _currentProcedural = StartCoroutine(ProceduralWave());
    }

    public void PlayTalk()
    {
        if (TrySetTrigger("talk")) return;
        StopProcedural();
        _currentProcedural = StartCoroutine(ProceduralTalkGesture());
    }

    public void PlayNod()
    {
        if (TrySetTrigger("nod")) return;
        StopProcedural();
        _currentProcedural = StartCoroutine(ProceduralNod());
    }

    public void PlayByName(string animName)
    {
        if (!TrySetTrigger(animName))
            Debug.LogWarning($"[AvatarAnimations] No trigger '{animName}' found");
    }

    // ── Helpers ──────────────────────────────────────────────────────────

    private bool TrySetTrigger(string name)
    {
        if (!_hasAnimatorController || _parameterHashes == null) return false;
        int hash = Animator.StringToHash(name);
        if (_parameterHashes.Contains(hash))
        {
            _animator.SetTrigger(hash);
            return true;
        }
        return false;
    }

    private void StopProcedural()
    {
        if (_currentProcedural != null)
        {
            StopCoroutine(_currentProcedural);
            _currentProcedural = null;
        }
        // Clear all overrides → LateUpdate will apply rest pose
        _overrideRightUpper = null;
        _overrideRightLower = null;
        _overrideRightHand = null;
        _overrideLeftUpper = null;
        _overrideLeftLower = null;
        CurrentAnimName = "";
    }

    private static float Smoothstep(float t)
    {
        t = Mathf.Clamp01(t);
        return t * t * (3f - 2f * t);
    }

    // ── Procedural animations ────────────────────────────────────────────
    // All animations set _override* quaternions; LateUpdate applies them.
    // This prevents the Animator from overwriting our work.

    private IEnumerator ProceduralWave()
    {
        if (_rightUpperArm == null) yield break;
        CurrentAnimName = "wave";

        // Wave target: raise right arm up and out (relative to T-pose, not rest)
        Quaternion raisedUpper = _tposeRightUpper * Quaternion.Euler(-60f, 0f, 10f);
        Quaternion bentLower = _tposeRightLower * Quaternion.Euler(0f, 0f, -40f);

        // ── Raise arm (0.4s)
        float elapsed = 0f;
        while (elapsed < 0.4f)
        {
            float s = Smoothstep(elapsed / 0.4f);
            _overrideRightUpper = Quaternion.Slerp(_restRightUpper, raisedUpper, s);
            _overrideRightLower = Quaternion.Slerp(_restRightLower, bentLower, s);
            elapsed += Time.deltaTime;
            yield return null;
        }

        // ── Wave oscillation (1.6s)
        float waveStart = elapsed;
        while (elapsed - waveStart < 1.6f)
        {
            float wt = (elapsed - waveStart) / 1.6f;
            float wave = Mathf.Sin(wt * Mathf.PI * 4f) * 15f;
            _overrideRightHand = Quaternion.Euler(0f, 0f, wave);
            elapsed += Time.deltaTime;
            yield return null;
        }

        // ── Lower arm (0.5s)
        float lowerStart = elapsed;
        Quaternion curUpper = _overrideRightUpper ?? raisedUpper;
        Quaternion curLower = _overrideRightLower ?? bentLower;
        while (elapsed - lowerStart < 0.5f)
        {
            float s = Smoothstep((elapsed - lowerStart) / 0.5f);
            _overrideRightUpper = Quaternion.Slerp(curUpper, _restRightUpper, s);
            _overrideRightLower = Quaternion.Slerp(curLower, _restRightLower, s);
            _overrideRightHand = Quaternion.identity;
            elapsed += Time.deltaTime;
            yield return null;
        }

        // Done — clear overrides (LateUpdate will apply rest pose)
        _overrideRightUpper = null;
        _overrideRightLower = null;
        _overrideRightHand = null;
        CurrentAnimName = "";
        _currentProcedural = null;
        Debug.Log("[AvatarAnimations] Wave complete");
    }

    private IEnumerator ProceduralNod()
    {
        if (_headBone == null) yield break;
        CurrentAnimName = "nod";

        Quaternion orig = _headBone.localRotation;
        float elapsed = 0f;

        while (elapsed < 0.8f)
        {
            float t = elapsed / 0.8f;
            float nod = Mathf.Sin(t * Mathf.PI * 2f) * 8f;
            _headBone.localRotation = orig * Quaternion.Euler(nod, 0f, 0f);
            elapsed += Time.deltaTime;
            yield return null;
        }

        _headBone.localRotation = orig;
        CurrentAnimName = "";
        _currentProcedural = null;
        Debug.Log("[AvatarAnimations] Nod complete");
    }

    private IEnumerator ProceduralTalkGesture()
    {
        if (_rightUpperArm == null) yield break;
        CurrentAnimName = "talk";

        // Subtle arm raise for talking — much smaller than wave
        Quaternion talkUpper = _restRightUpper * Quaternion.Euler(-15f, 0f, -10f);
        Quaternion talkLower = _restRightLower * Quaternion.Euler(0f, -20f, 0f);

        float elapsed = 0f;

        // ── Raise (0.3s)
        while (elapsed < 0.3f)
        {
            float s = Smoothstep(elapsed / 0.3f);
            _overrideRightUpper = Quaternion.Slerp(_restRightUpper, talkUpper, s);
            _overrideRightLower = Quaternion.Slerp(_restRightLower, talkLower, s);
            elapsed += Time.deltaTime;
            yield return null;
        }

        // ── Gesticulate (2.0s)
        float gestStart = elapsed;
        while (elapsed - gestStart < 2.0f)
        {
            float gt = elapsed - gestStart;
            float gx = Mathf.Sin(gt * 3f) * 3f;
            float gy = Mathf.Cos(gt * 2f) * 4f;
            _overrideRightUpper = talkUpper * Quaternion.Euler(gx, gy, 0f);
            elapsed += Time.deltaTime;
            yield return null;
        }

        // ── Lower (0.4s)
        float lowerStart = elapsed;
        Quaternion cur = _overrideRightUpper ?? talkUpper;
        Quaternion curLower = _overrideRightLower ?? talkLower;
        while (elapsed - lowerStart < 0.4f)
        {
            float s = Smoothstep((elapsed - lowerStart) / 0.4f);
            _overrideRightUpper = Quaternion.Slerp(cur, _restRightUpper, s);
            _overrideRightLower = Quaternion.Slerp(curLower, _restRightLower, s);
            elapsed += Time.deltaTime;
            yield return null;
        }

        _overrideRightUpper = null;
        _overrideRightLower = null;
        CurrentAnimName = "";
        _currentProcedural = null;
        Debug.Log("[AvatarAnimations] Talk gesture complete");
    }
}
