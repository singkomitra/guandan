using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;

// ---------------------------------------------------------------------------
// Types
// ---------------------------------------------------------------------------

public enum AnnouncementType { Error, Warning, Info, Bomb, RoundWin }

// ---------------------------------------------------------------------------
// AnnouncementOverlay
// ---------------------------------------------------------------------------

/// <summary>
/// Singleton MonoBehaviour that displays large, dramatic center-screen text.
/// Every new announcement interrupts the current one immediately.
/// Static entry point: AnnouncementOverlay.Show(AnnouncementType.Bomb, "BOMB!");
///
/// Animation: decaying horizontal shake only.
///
/// Scene setup:
///   1. Create a new Canvas (Sort Order 100, Screen Space Overlay).
///   2. Add a CanvasGroup component to the Canvas root.
///   3. Add a child RectTransform anchored center-center; add a TextMeshProUGUI component.
///   4. Attach this script to the Canvas root and wire _label + _canvasGroup.
/// </summary>
public class AnnouncementOverlay : MonoBehaviour
{
    // -----------------------------------------------------------------------
    // Per-type config
    // -----------------------------------------------------------------------

    [Serializable]
    public struct TypeConfig
    {
        public AnnouncementType type;
        public Color            color;
        [Range(0.5f, 3f)]
        public float            fontScale;      // multiplier on base font size
        public float            displayDuration; // total seconds text is visible (must be >= shakeDuration)
        public float            fadeDuration;    // seconds to fade out at the end of displayDuration
        public float            shakeDuration;   // seconds of horizontal shake (subset of displayDuration)
        public float            shakeAmplitude;  // peak pixels of horizontal offset
        public float            shakeFrequency;  // oscillations per second
    }

    // -----------------------------------------------------------------------
    // History
    // -----------------------------------------------------------------------

    public struct AnnouncementRecord
    {
        public AnnouncementType Type;
        public string           Message;
        public DateTime         Timestamp;
    }

    // -----------------------------------------------------------------------
    // Inspector fields
    // -----------------------------------------------------------------------

    [SerializeField] private TMP_Text    _label;
    [SerializeField] private CanvasGroup _canvasGroup;
    [SerializeField] private float       _baseFontSize = 72f;

    [SerializeField] private TypeConfig[] _typeConfigs;

    // -----------------------------------------------------------------------
    // Singleton
    // -----------------------------------------------------------------------

    public static AnnouncementOverlay Instance { get; private set; }

    // -----------------------------------------------------------------------
    // Editor defaults — called when component is first added or Reset clicked
    // -----------------------------------------------------------------------

    private void Reset()
    {
        _baseFontSize = 72f;
        _typeConfigs  = new TypeConfig[]
        {
            new TypeConfig {
                type            = AnnouncementType.Error,
                color           = new Color(0.95f, 0.2f, 0.2f),
                fontScale       = 1f,
                displayDuration = 1.2f,
                fadeDuration    = 0.3f,
                shakeDuration   = 0.4f,
                shakeAmplitude  = 14f,
                shakeFrequency  = 28f,
            },
            new TypeConfig {
                type            = AnnouncementType.Warning,
                color           = new Color(1f, 0.75f, 0f),
                fontScale       = 1f,
                displayDuration = 1.2f,
                fadeDuration    = 0.3f,
                shakeDuration   = 0.3f,
                shakeAmplitude  = 9f,
                shakeFrequency  = 22f,
            },
            new TypeConfig {
                type            = AnnouncementType.Info,
                color           = new Color(0.85f, 0.85f, 0.85f),
                fontScale       = 0.85f,
                displayDuration = 1.0f,
                fadeDuration    = 0.3f,
                shakeDuration   = 0.2f,
                shakeAmplitude  = 4f,
                shakeFrequency  = 16f,
            },
            new TypeConfig {
                type            = AnnouncementType.Bomb,
                color           = new Color(1f, 0.4f, 0f),
                fontScale       = 1.5f,
                displayDuration = 2.0f,
                fadeDuration    = 0.4f,
                shakeDuration   = 0.55f,
                shakeAmplitude  = 22f,
                shakeFrequency  = 32f,
            },
            new TypeConfig {
                type            = AnnouncementType.RoundWin,
                color           = new Color(0.2f, 0.95f, 0.45f),
                fontScale       = 1.3f,
                displayDuration = 2.0f,
                fadeDuration    = 0.4f,
                shakeDuration   = 0.35f,
                shakeAmplitude  = 10f,
                shakeFrequency  = 18f,
            },
        };
    }

    // -----------------------------------------------------------------------
    // Public API
    // -----------------------------------------------------------------------

    /// <summary>Display an announcement, interrupting any currently playing one.</summary>
    public static void Show(AnnouncementType type, string message)
    {
        if (Instance == null)
        {
            Debug.LogWarning("[AnnouncementOverlay] No instance in scene — announcement dropped.");
            return;
        }
        Instance.Interrupt(type, message);
    }

    /// <summary>In-memory log of every announcement that has been shown.</summary>
    public static IReadOnlyList<AnnouncementRecord> History => Instance?._history;

    // -----------------------------------------------------------------------
    // Private state
    // -----------------------------------------------------------------------

    private readonly List<AnnouncementRecord> _history = new();
    private Coroutine _active;

    // -----------------------------------------------------------------------
    // Unity lifecycle
    // -----------------------------------------------------------------------

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        if (_canvasGroup != null)
            _canvasGroup.alpha = 0f;
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    // -----------------------------------------------------------------------
    // Playback
    // -----------------------------------------------------------------------

    private void Interrupt(AnnouncementType type, string message)
    {
        _history.Add(new AnnouncementRecord
        {
            Type      = type,
            Message   = message,
            Timestamp = DateTime.Now,
        });

        if (_active != null)
            StopCoroutine(_active);

        _active = StartCoroutine(Play(type, message));
    }

    private IEnumerator Play(AnnouncementType type, string message)
    {
        var cfg    = GetConfig(type);
        var labelRt = _label.rectTransform;
        var basePos = labelRt.anchoredPosition;

        _label.text     = message;
        _label.color    = cfg.color;
        _label.fontSize = _baseFontSize * cfg.fontScale;
        _canvasGroup.alpha = 1f;
        labelRt.anchoredPosition = basePos;

        // Shake + hold
        float elapsed = 0f;
        float duration = Mathf.Max(cfg.displayDuration, cfg.shakeDuration);
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;

            float offset = 0f;
            if (elapsed < cfg.shakeDuration)
            {
                float decay = 1f - Mathf.Clamp01(elapsed / cfg.shakeDuration);
                offset = Mathf.Sin(elapsed * cfg.shakeFrequency * Mathf.PI * 2f)
                         * cfg.shakeAmplitude * decay;
            }
            labelRt.anchoredPosition = basePos + new Vector2(offset, 0f);

            yield return null;
        }

        // Fade out
        labelRt.anchoredPosition = basePos;
        elapsed = 0f;
        while (elapsed < cfg.fadeDuration)
        {
            elapsed += Time.deltaTime;
            _canvasGroup.alpha = 1f - Mathf.Clamp01(elapsed / cfg.fadeDuration);
            yield return null;
        }

        _canvasGroup.alpha = 0f;
        _active = null;
    }

    // -----------------------------------------------------------------------
    // Helpers
    // -----------------------------------------------------------------------

    private TypeConfig GetConfig(AnnouncementType type)
    {
        foreach (var cfg in _typeConfigs)
            if (cfg.type == type) return cfg;

        return _typeConfigs.Length > 0 ? _typeConfigs[0] : default;
    }
}
