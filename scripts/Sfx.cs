using Godot;
using System.Collections.Generic;

namespace GrowDaGarden;

/// <summary>
/// Tiny procedural sound engine — every sound is synthesised at runtime from
/// soft sine tones (no audio files needed). Call <see cref="Play"/> with a sound
/// name for an action or event. A gentle wind loop plays underneath for ambience.
/// </summary>
public partial class Sfx : Node
{
    private const int MixRate = 44100;

    private readonly Dictionary<string, AudioStreamWav> _bank = new();
    private AudioStreamPlayer[] _players = System.Array.Empty<AudioStreamPlayer>();
    private int _next;

    public override void _Ready()
    {
        BuildBank();

        // A small pool so overlapping actions don't cut each other off.
        _players = new AudioStreamPlayer[6];
        for (int i = 0; i < _players.Length; i++)
        {
            var p = new AudioStreamPlayer { VolumeDb = -9f };
            AddChild(p);
            _players[i] = p;
        }

        // Soft looping ambient wind.
        var amb = new AudioStreamPlayer { VolumeDb = -27f, Stream = Wind() };
        AddChild(amb);
        amb.Play();
    }

    public void Play(string name, float volumeDb = -9f)
    {
        if (!_bank.TryGetValue(name, out AudioStreamWav? s))
            return;
        AudioStreamPlayer p = _players[_next];
        _next = (_next + 1) % _players.Length;
        p.Stream = s;
        p.VolumeDb = volumeDb;
        p.Play();
    }

    // ---- sound bank (freq Hz, dur s, amp 0..1, decay rate) ----------------

    private void BuildBank()
    {
        _bank["select"]     = Build((0f, 900f, 0.05f, 0.45f, 30f));
        _bank["plant"]      = Build((0f, 196f, 0.16f, 0.6f, 11f), (0f, 392f, 0.08f, 0.2f, 16f));
        _bank["harvest"]    = Build((0f, 587f, 0.10f, 0.5f, 12f), (0.07f, 784f, 0.16f, 0.5f, 9f));
        _bank["harvestMut"] = Build((0f, 784f, 0.09f, 0.45f, 12f), (0.06f, 1046f, 0.09f, 0.45f, 12f), (0.12f, 1318f, 0.20f, 0.5f, 8f));
        _bank["sell"]       = Build((0f, 1318f, 0.07f, 0.45f, 16f), (0.05f, 1760f, 0.16f, 0.45f, 9f));
        _bank["error"]      = Build((0f, 150f, 0.2f, 0.45f, 6f), (0f, 151.5f, 0.2f, 0.25f, 6f));
        _bank["gateOpen"]   = Build((0f, 300f, 0.10f, 0.4f, 10f), (0.08f, 460f, 0.18f, 0.4f, 8f));
        _bank["gateClose"]  = Build((0f, 460f, 0.10f, 0.4f, 10f), (0.08f, 300f, 0.18f, 0.4f, 8f));
        _bank["grow"]       = Build((0f, 523f, 0.10f, 0.4f, 11f), (0.08f, 659f, 0.10f, 0.4f, 11f), (0.16f, 784f, 0.22f, 0.45f, 8f));
        _bank["rainbow"]    = Build((0f, 523f, 0.08f, 0.4f, 12f), (0.06f, 659f, 0.08f, 0.4f, 12f), (0.12f, 784f, 0.08f, 0.4f, 12f), (0.18f, 1046f, 0.24f, 0.5f, 7f));
        _bank["clear"]      = Build((0f, 784f, 0.10f, 0.35f, 11f), (0.08f, 587f, 0.2f, 0.35f, 9f));
        _bank["unlock"]     = Build((0f, 523f, 0.8f, 0.4f, 2.2f), (0f, 659f, 0.8f, 0.4f, 2.2f), (0f, 784f, 0.8f, 0.45f, 2.2f),
                                    (0.16f, 1046f, 0.6f, 0.4f, 3f), (0.32f, 1318f, 0.6f, 0.35f, 3f));
        _bank["storm"]      = Storm();
        _bank["thunder"]    = Thunder();
    }

    // ---- synthesis --------------------------------------------------------

    private static AudioStreamWav Build(params (float At, float Freq, float Dur, float Amp, float Decay)[] notes)
    {
        float total = 0f;
        foreach (var n in notes)
            total = Mathf.Max(total, n.At + n.Dur);

        int len = (int)(total * MixRate) + 1;
        var buf = new float[len];

        foreach (var n in notes)
        {
            int start = (int)(n.At * MixRate);
            int count = (int)(n.Dur * MixRate);
            const float attack = 0.006f;
            for (int i = 0; i < count && start + i < len; i++)
            {
                float t = i / (float)MixRate;
                float env = t < attack ? t / attack : Mathf.Exp(-(t - attack) * n.Decay);
                float s = Mathf.Sin(t * n.Freq * Mathf.Tau) * 0.75f
                        + Mathf.Sin(t * n.Freq * 2f * Mathf.Tau) * 0.18f; // a touch of warmth
                buf[start + i] += s * env * n.Amp;
            }
        }

        return FromSamples(buf, loop: false);
    }

    /// <summary>A soft low rumble (low sines + filtered noise) for the storm event.</summary>
    private static AudioStreamWav Storm()
    {
        int len = (int)(0.9f * MixRate);
        var buf = new float[len];
        float lp = 0f;
        for (int i = 0; i < len; i++)
        {
            float t = i / (float)MixRate;
            float env = t < 0.12f ? t / 0.12f : Mathf.Exp(-(t - 0.12f) * 2.2f);
            float noise = GD.Randf() * 2f - 1f;
            lp += 0.04f * (noise - lp);
            float s = Mathf.Sin(t * 70f * Mathf.Tau) * 0.5f
                    + Mathf.Sin(t * 46f * Mathf.Tau) * 0.4f
                    + lp * 0.5f;
            buf[i] = s * env * 0.5f;
        }
        return FromSamples(buf, loop: false);
    }

    /// <summary>A sharp crack followed by a rolling boom for lightning strikes.</summary>
    private static AudioStreamWav Thunder()
    {
        int len = (int)(1.1f * MixRate);
        var buf = new float[len];
        float lp = 0f;
        for (int i = 0; i < len; i++)
        {
            float t = i / (float)MixRate;
            float crack = t < 0.05f ? 1f - t / 0.05f : 0f;   // sharp opening transient
            float env = Mathf.Exp(-t * 3.2f);
            float noise = GD.Randf() * 2f - 1f;
            lp += 0.25f * (noise - lp);
            float boom = Mathf.Sin(t * 55f * Mathf.Tau) * 0.5f;
            buf[i] = (lp * 0.7f + boom * 0.5f) * env + (GD.Randf() * 2f - 1f) * crack * 0.6f;
        }
        return FromSamples(buf, loop: false);
    }

    /// <summary>A gentle, loopable wind bed.</summary>
    private static AudioStreamWav Wind()
    {
        int len = (int)(3f * MixRate);
        var buf = new float[len];
        float lp = 0f;
        for (int i = 0; i < len; i++)
        {
            float t = i / (float)MixRate;
            float noise = GD.Randf() * 2f - 1f;
            lp += 0.012f * (noise - lp);                          // low-pass -> airy hiss
            float swell = 0.6f + 0.4f * Mathf.Sin(t * 0.5f * Mathf.Tau); // slow rise and fall
            buf[i] = lp * swell * 0.5f;
        }

        // Crossfade the head with the tail so the loop point isn't a click.
        int xf = (int)(0.25f * MixRate);
        for (int i = 0; i < xf; i++)
        {
            float k = i / (float)xf;
            buf[i] = Mathf.Lerp(buf[len - xf + i], buf[i], k);
        }
        return FromSamples(buf, loop: true);
    }

    private static AudioStreamWav FromSamples(float[] s, bool loop)
    {
        var data = new byte[s.Length * 2];
        for (int i = 0; i < s.Length; i++)
        {
            short v = (short)(Mathf.Clamp(s[i], -1f, 1f) * 32000f);
            data[i * 2] = (byte)(v & 0xff);
            data[i * 2 + 1] = (byte)((v >> 8) & 0xff);
        }

        var w = new AudioStreamWav
        {
            Format = AudioStreamWav.FormatEnum.Format16Bits,
            MixRate = MixRate,
            Stereo = false,
            Data = data,
        };
        if (loop)
        {
            w.LoopMode = AudioStreamWav.LoopModeEnum.Forward;
            w.LoopBegin = 0;
            w.LoopEnd = s.Length;
        }
        return w;
    }
}
