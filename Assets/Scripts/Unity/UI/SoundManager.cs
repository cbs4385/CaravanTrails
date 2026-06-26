using UnityEngine;

// Generates all game SFX and ambient procedurally — no audio assets required.
// GameController adds this to itself at runtime.
public class SoundManager : MonoBehaviour
{
    const int SR = 44100;

    AudioSource _src;
    AudioSource _ambSrc;
    AudioClip   _tick, _win, _lose, _click, _coin, _evt;

    void Awake()
    {
        _src = gameObject.AddComponent<AudioSource>();
        _src.playOnAwake = false;

        _ambSrc = gameObject.AddComponent<AudioSource>();
        _ambSrc.playOnAwake = false;
        _ambSrc.loop   = true;
        _ambSrc.volume = 0.18f;

        _tick  = GenTick();
        _win   = GenWin();
        _lose  = GenLose();
        _click = GenClick();
        _coin  = GenCoin();
        _evt   = GenEvent();
        _ambSrc.clip = GenAmbient();
    }

    public void PlayTick()    => _src.PlayOneShot(_tick,  0.50f);
    public void PlayWin()     => _src.PlayOneShot(_win,   0.65f);
    public void PlayLose()    => _src.PlayOneShot(_lose,  0.65f);
    public void PlayClick()   => _src.PlayOneShot(_click, 0.40f);
    public void PlayCoin()    => _src.PlayOneShot(_coin,  0.55f);
    public void PlayEvent()   => _src.PlayOneShot(_evt,   0.60f);
    public void PlayAmbient() { if (!_ambSrc.isPlaying) _ambSrc.Play(); }
    public void StopAmbient() => _ambSrc.Stop();

    // ── Percussive tick ───────────────────────────────────────────────────────

    static AudioClip GenTick()
    {
        int n = (int)(0.09f * SR);
        var b = new float[n];
        for (int i = 0; i < n; i++)
        {
            float t   = (float)i / SR;
            float env = Mathf.Exp(-t * 45f);
            b[i] = env * (0.65f * Mathf.Sin(2 * Mathf.PI * 920f  * t)
                        + 0.35f * Mathf.Sin(2 * Mathf.PI * 1840f * t));
        }
        return MakeClip("Tick", b);
    }

    // ── Victory fanfare: ascending C5-E5-G5-C6 ───────────────────────────────

    static AudioClip GenWin()
    {
        float[] freqs   = { 523.25f, 659.25f, 783.99f, 1046.50f };
        float   noteDur = 0.16f;
        int     n       = (int)((freqs.Length * noteDur + 0.18f) * SR);
        var     b       = new float[n];

        for (int ni = 0; ni < freqs.Length; ni++)
        {
            int   start   = (int)(ni * noteDur * SR);
            int   noteLen = (int)((noteDur + 0.16f) * SR);
            float freq    = freqs[ni];
            for (int i = 0; i < noteLen && start + i < n; i++)
            {
                float t      = (float)i / SR;
                float attack = 1f - Mathf.Exp(-t * 200f);
                float decay  = Mathf.Exp(-t * 7f);
                b[start + i] += attack * decay * 0.55f * Mathf.Sin(2 * Mathf.PI * freq * t);
            }
        }
        Normalise(b, 0.90f);
        return MakeClip("Win", b);
    }

    // ── Defeat: sub-bass thump + descending dissonant duo ────────────────────

    static AudioClip GenLose()
    {
        float dur = 0.70f;
        int   n   = (int)(dur * SR);
        var   b   = new float[n];
        float p1  = 0f, p2 = 0f;

        for (int i = 0; i < n; i++)
        {
            float t = (float)i / SR;
            b[i] += 0.50f * Mathf.Exp(-t * 14f) * Mathf.Sin(2 * Mathf.PI * 80f * t);
            float f1 = Mathf.Lerp(330f, 196f, t / dur);
            p1 += 2 * Mathf.PI * f1 / SR;
            b[i] += 0.40f * Mathf.Exp(-t * 3.5f) * Mathf.Sin(p1);
            float f2 = Mathf.Lerp(277f, 165f, t / dur);
            p2 += 2 * Mathf.PI * f2 / SR;
            b[i] += 0.25f * Mathf.Exp(-t * 4.0f) * Mathf.Sin(p2);
        }
        Normalise(b, 0.85f);
        return MakeClip("Lose", b);
    }

    // ── Soft UI tap: 600 Hz, ~40 ms ──────────────────────────────────────────

    static AudioClip GenClick()
    {
        int n = (int)(0.04f * SR);
        var b = new float[n];
        for (int i = 0; i < n; i++)
        {
            float t = (float)i / SR;
            b[i] = Mathf.Exp(-t * 80f) * Mathf.Sin(2 * Mathf.PI * 600f * t);
        }
        Normalise(b, 0.55f);
        return MakeClip("Click", b);
    }

    // ── Coin jingle: A5 → D6, bell timbre, ~200 ms ───────────────────────────

    static AudioClip GenCoin()
    {
        float[] freqs   = { 880f, 1174.66f };
        float   noteDur = 0.08f;
        int     n       = (int)((freqs.Length * noteDur + 0.06f) * SR);
        var     b       = new float[n];

        for (int ni = 0; ni < freqs.Length; ni++)
        {
            int   start   = (int)(ni * noteDur * SR);
            int   noteLen = (int)((noteDur + 0.05f) * SR);
            float freq    = freqs[ni];
            for (int i = 0; i < noteLen && start + i < n; i++)
            {
                float t      = (float)i / SR;
                float attack = 1f - Mathf.Exp(-t * 300f);
                float decay  = Mathf.Exp(-t * 12f);
                float env    = attack * decay;
                b[start + i] += env * Mathf.Sin(2 * Mathf.PI * freq * t);
                b[start + i] += 0.30f * env * Mathf.Exp(-t * 20f)
                              * Mathf.Sin(2 * Mathf.PI * freq * 2.76f * t);
            }
        }
        Normalise(b, 0.80f);
        return MakeClip("Coin", b);
    }

    // ── Event alert: D4 → A4 two-pulse bell ──────────────────────────────────

    static AudioClip GenEvent()
    {
        float[] freqs   = { 293.66f, 440f };
        float   noteDur = 0.12f;
        int     n       = (int)((freqs.Length * noteDur + 0.10f) * SR);
        var     b       = new float[n];

        for (int ni = 0; ni < freqs.Length; ni++)
        {
            int   start   = (int)(ni * noteDur * SR);
            int   noteLen = (int)((noteDur + 0.09f) * SR);
            float freq    = freqs[ni];
            for (int i = 0; i < noteLen && start + i < n; i++)
            {
                float t   = (float)i / SR;
                float env = (1f - Mathf.Exp(-t * 150f)) * Mathf.Exp(-t * 8f);
                b[start + i] += env * Mathf.Sin(2 * Mathf.PI * freq * t);
                b[start + i] += 0.25f * env * Mathf.Exp(-t * 14f)
                              * Mathf.Sin(2 * Mathf.PI * freq * 3f * t);
            }
        }
        Normalise(b, 0.75f);
        return MakeClip("Event", b);
    }

    // ── Looping market ambient: warm C drone with detuned overtones ───────────

    static AudioClip GenAmbient()
    {
        float dur = 4.0f;
        int   n   = (int)(dur * SR);
        var   b   = new float[n];

        float[] ambFreqs   = { 65.41f, 98f, 130.81f, 196f, 261.63f };
        float[] ambAmps    = { 0.35f, 0.25f, 0.18f, 0.12f, 0.08f };
        float[] ambDetunes = { 0f, 0.30f, -0.20f, 0.50f, -0.40f };

        for (int vi = 0; vi < ambFreqs.Length; vi++)
        {
            float freq = ambFreqs[vi] + ambDetunes[vi];
            float amp  = ambAmps[vi];
            float phaseOff = vi * 1.2f;
            for (int i = 0; i < n; i++)
            {
                float t       = (float)i / SR;
                float tremolo = 1f + 0.06f * Mathf.Sin(2 * Mathf.PI * 0.15f * t + phaseOff);
                b[i] += amp * tremolo * Mathf.Sin(2 * Mathf.PI * freq * t);
            }
        }

        // Crossfade loop endpoints to eliminate click
        int fade = (int)(0.08f * SR);
        for (int i = 0; i < fade; i++)
        {
            float t = (float)i / fade;
            b[i]         = Mathf.Lerp(b[n - fade + i], b[i], t);
            b[n - fade + i] *= (1f - t);
        }

        Normalise(b, 0.60f);
        return MakeClip("Ambient", b);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    static void Normalise(float[] b, float target)
    {
        float peak = 0f;
        foreach (var s in b) peak = Mathf.Max(peak, Mathf.Abs(s));
        if (peak > 0f)
        {
            float scale = target / peak;
            for (int i = 0; i < b.Length; i++) b[i] *= scale;
        }
    }

    static AudioClip MakeClip(string name, float[] b)
    {
        var c = AudioClip.Create(name, b.Length, 1, SR, false);
        c.SetData(b, 0);
        return c;
    }
}
