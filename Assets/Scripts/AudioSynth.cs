using UnityEngine;

// Procedurally generated retro SFX so the project needs no audio assets.
public static class AudioSynth
{
    const int SR = 22050;

    public static AudioClip Sweep(string name, float f0, float f1, float dur, float vol = 0.35f)
    {
        int n = (int)(SR * dur);
        var data = new float[n];
        float phase = 0f;
        for (int i = 0; i < n; i++)
        {
            float t = i / (float)n;
            float f = Mathf.Lerp(f0, f1, t);
            phase += 2f * Mathf.PI * f / SR;
            float env = Mathf.Sin(Mathf.PI * t);
            data[i] = Mathf.Sign(Mathf.Sin(phase)) * vol * env;
        }
        return MakeClip(name, data);
    }

    public static AudioClip Arpeggio(string name, float[] freqs, float noteDur, float vol = 0.3f)
    {
        int perNote = (int)(SR * noteDur);
        var data = new float[perNote * freqs.Length];
        int idx = 0;
        foreach (float f in freqs)
        {
            float phase = 0f;
            for (int i = 0; i < perNote; i++, idx++)
            {
                phase += 2f * Mathf.PI * f / SR;
                float env = 1f - i / (float)perNote;
                data[idx] = Mathf.Sign(Mathf.Sin(phase)) * vol * env;
            }
        }
        return MakeClip(name, data);
    }

    // Seamless sine loop rising and falling between two frequencies.
    public static AudioClip SirenLoop(string name, float f0, float f1, float dur, float vol = 0.08f)
    {
        int n = (int)(SR * dur);
        var data = new float[n];
        float phase = 0f;
        for (int i = 0; i < n; i++)
        {
            float t = i / (float)n;
            float f = Mathf.Lerp(f0, f1, 0.5f - 0.5f * Mathf.Cos(2f * Mathf.PI * t));
            phase += 2f * Mathf.PI * f / SR;
            data[i] = Mathf.Sin(phase) * vol;
        }
        return MakeClip(name, data);
    }

    static AudioClip MakeClip(string name, float[] data)
    {
        var clip = AudioClip.Create(name, data.Length, 1, SR, false);
        clip.SetData(data, 0);
        return clip;
    }
}
