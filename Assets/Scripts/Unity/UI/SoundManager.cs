using UnityEngine;

// Generates all game SFX procedurally — no audio assets required.
// Attach to any GameObject; GameController adds it to itself at runtime.
public class SoundManager : MonoBehaviour
{
    const int SR = 44100;

    AudioSource _src;
    AudioClip   _tick, _win, _lose;

    void Awake()
    {
        _src = gameObject.AddComponent<AudioSource>();
        _src.playOnAwake = false;
        _tick = GenTick();
        _win  = GenWin();
        _lose = GenLose();
    }

    public void PlayTick() => _src.PlayOneShot(_tick, 0.50f);
    public void PlayWin()  => _src.PlayOneShot(_win,  0.65f);
    public void PlayLose() => _src.PlayOneShot(_lose, 0.65f);

    // Short percussive click: fundamental + octave overtone, fast exponential decay
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
        var c = AudioClip.Create("Tick", n, 1, SR, false);
        c.SetData(b, 0);
        return c;
    }

    // Ascending four-note chime: C5 E5 G5 C6 — victory fanfare
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
        var c = AudioClip.Create("Win", n, 1, SR, false);
        c.SetData(b, 0);
        return c;
    }

    // Low thump + two descending voices — weight and finality
    static AudioClip GenLose()
    {
        float dur = 0.70f;
        int   n   = (int)(dur * SR);
        var   b   = new float[n];
        float p1  = 0f, p2 = 0f;

        for (int i = 0; i < n; i++)
        {
            float t = (float)i / SR;

            // Sub-bass thump
            b[i] += 0.50f * Mathf.Exp(-t * 14f) * Mathf.Sin(2 * Mathf.PI * 80f * t);

            // Voice 1: 330 → 196 Hz (E4 → G3)
            float f1 = Mathf.Lerp(330f, 196f, t / dur);
            p1 += 2 * Mathf.PI * f1 / SR;
            b[i] += 0.40f * Mathf.Exp(-t * 3.5f) * Mathf.Sin(p1);

            // Voice 2: 277 → 165 Hz (C#4 → E3) — minor third below, dissonant
            float f2 = Mathf.Lerp(277f, 165f, t / dur);
            p2 += 2 * Mathf.PI * f2 / SR;
            b[i] += 0.25f * Mathf.Exp(-t * 4.0f) * Mathf.Sin(p2);
        }

        Normalise(b, 0.85f);
        var c = AudioClip.Create("Lose", n, 1, SR, false);
        c.SetData(b, 0);
        return c;
    }

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
}
