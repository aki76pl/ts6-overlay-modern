using System.IO;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;

namespace TS6Overlay;

public enum SoundKind { Join, Leave, Favorite, Message, Poke, MuteWarning, Recording }

/// <summary>
/// Krótkie sygnały dźwiękowe z regulacją głośności. Domyślne dźwięki są generowane w pamięci.
/// Własne dźwięki przypisuje się w ustawieniach (kopia trafia do %APPDATA%\TS6Overlay\sounds);
/// starsze pliki join.wav / leave.wav … w %APPDATA%\TS6Overlay też działają.
/// </summary>
public static class Sounds
{
    const int Rate = 44100;
    static readonly Dictionary<SoundKind, float[]> Cache = new();
    static IWavePlayer? _out;
    static IDisposable? _reader;
    static readonly object Lock = new();

    public static int Volume { get; set; } = 60;
    /// <summary>Dłuższe pliki są ucinane — powiadomienie nie może grać w nieskończoność.</summary>
    public const double MaxSeconds = 8;
    public static readonly string[] Extensions = { ".wav", ".mp3", ".wma", ".aiff", ".m4a" };
    public static string Dir => Path.Combine(Config.Dir, "sounds");

    /// <summary>Przypisania z ustawień (ustawiane przez App przy każdej zmianie konfiguracji).</summary>
    public static Dictionary<string, string> Files { get; set; } = new();

    public static string? AssignedFile(SoundKind kind) =>
        Files.TryGetValue(kind.ToString(), out var f) && File.Exists(f) ? f : null;

    /// <summary>
    /// Kopiuje wybrany plik do folderu dźwięków pod nazwą <paramref name="key"/> i sprawdza, czy da się go odtworzyć.
    /// Zwraca ścieżkę kopii i długość nagrania.
    /// </summary>
    public static (string path, TimeSpan length) Import(string source, string key)
    {
        string ext = Path.GetExtension(source).ToLowerInvariant();
        if (!Extensions.Contains(ext)) throw new InvalidDataException(L.T("Obsługiwane formaty: {0}", string.Join(", ", Extensions)));
        TimeSpan len;
        using (var test = new AudioFileReader(source)) len = test.TotalTime;
        if (len <= TimeSpan.Zero) throw new InvalidDataException(L.T("Plik nie zawiera dźwięku."));

        Directory.CreateDirectory(Dir);
        string safe = string.Concat(key.Select(c => Path.GetInvalidFileNameChars().Contains(c) ? '_' : c));
        foreach (var old in Directory.GetFiles(Dir, safe + ".*")) TryDelete(old);
        string dest = Path.Combine(Dir, safe + ext);
        File.Copy(source, dest, true);
        return (dest, len);
    }

    public static void TryDelete(string? path)
    {
        try { if (!string.IsNullOrEmpty(path) && path.StartsWith(Dir, StringComparison.OrdinalIgnoreCase) && File.Exists(path)) File.Delete(path); }
        catch { }
    }

    public static TimeSpan Length(string path)
    {
        try { using var r = new AudioFileReader(path); return r.TotalTime; }
        catch { return TimeSpan.Zero; }
    }

    public sealed record OutputDevice(string Name, int Number);
    public static string OutputDeviceName { get; set; } = "";
    public static string LastStatus { get; private set; } = "";
    public static string LastError { get; private set; } = "";
    public static event Action<string>? PlaybackError;
    internal static Func<IWavePlayer> OutputFactory { get; set; } = () => new WaveOutEvent
    {
        DeviceNumber = ResolveDevice(OutputDeviceName, Devices()),
    };

    public static List<OutputDevice> Devices()
    {
        var result = new List<OutputDevice>();
        try
        {
            for (int i = 0; i < WaveOut.DeviceCount; i++) result.Add(new(WaveOut.GetCapabilities(i).ProductName, i));
        }
        catch (Exception ex) { ReportError(ex); }
        return result;
    }
    internal static int ResolveDevice(string name, IReadOnlyList<OutputDevice> devices) =>
        string.IsNullOrWhiteSpace(name) ? -1 : devices.FirstOrDefault(d => d.Name == name)?.Number ?? -1;

    public static bool Play(SoundKind kind, string? customPath = null)
    {
        lock (Lock)
        {
            LastError = "";
            if (Volume <= 0) { ReleaseOutput(); LastStatus = L.T("Dźwięki są wyciszone — głośność wynosi 0%."); return false; }
            AudioFileReader? reader = null;
            try
            {
                string? file = !string.IsNullOrEmpty(customPath) && File.Exists(customPath) ? customPath : AssignedFile(kind);
                if (file == null)
                {
                    string name = kind switch
                    {
                        SoundKind.Join => "join", SoundKind.Leave => "leave", SoundKind.Message => "message",
                        SoundKind.Poke => "poke", SoundKind.Favorite => "favorite", SoundKind.Recording => "recording", _ => "mute",
                    };
                    file = new[] { ".wav", ".mp3" }.Select(e => Path.Combine(Config.Dir, name + e)).FirstOrDefault(File.Exists);
                }
                ISampleProvider source;
                string warning = "";
                try
                {
                    if (file == null) source = new RawSamples(Generated(kind));
                    else { reader = new AudioFileReader(file); source = new OffsetSampleProvider(reader) { Take = TimeSpan.FromSeconds(MaxSeconds) }; }
                }
                catch (Exception)
                {
                    reader?.Dispose(); reader = null;
                    source = new RawSamples(Generated(kind));
                    warning = L.T("Nie można odczytać własnego pliku — użyto dźwięku wbudowanego. ");
                }
                if (source.WaveFormat.SampleRate != Rate) source = new WdlResamplingSampleProvider(source, Rate);
                if (source.WaveFormat.Channels == 1) source = new MonoToStereoSampleProvider(source);
                var volume = new VolumeSampleProvider(source) { Volume = Math.Clamp(Volume, 0, 100) / 100f };
                ReleaseOutput();
                _reader = reader; reader = null;
                _out = OutputFactory();
                _out.PlaybackStopped += (sender, e) => { if (e.Exception != null && ReferenceEquals(sender, _out)) ReportError(e.Exception); };
                // Use widely supported PCM16 stereo instead of IEEE-float waveOut playback.
                _out.Init(new SampleToWaveProvider16(volume));
                _out.Play();
                LastStatus = warning + L.T("Odtwarzanie powiadomienia: {0}; głośność {1}%.", kind, Volume);
                return true;
            }
            catch (Exception ex)
            {
                reader?.Dispose(); ReleaseOutput(); ReportError(ex); return false;
            }
        }
    }

    static void ReportError(Exception error)
    {
        LastError = error.Message; LastStatus = L.T("Nie udało się odtworzyć dźwięku: {0}", error.Message);
        try
        {
            Directory.CreateDirectory(Config.Dir);
            File.AppendAllText(Path.Combine(Config.Dir, "sound-errors.log"), DateTime.Now.ToString("s") + " " + error + Environment.NewLine);
        }
        catch { }
        PlaybackError?.Invoke(LastStatus);
    }
    static void ReleaseOutput()
    {
        var output = _out; _out = null;
        try { output?.Stop(); } catch { }
        try { output?.Dispose(); } catch { }
        try { _reader?.Dispose(); } catch { }
        _reader = null;
    }
    public static void Stop() { lock (Lock) { try { _out?.Stop(); } catch { } } }
    public static void Dispose() { lock (Lock) ReleaseOutput(); }

    static float[] Generated(SoundKind kind)
    {
        lock (Cache)
        {
            if (Cache.TryGetValue(kind, out var d)) return d;
            d = kind switch
            {
                SoundKind.Join => Tones(0.11, 659.25, 880.0),                 // E5 → A5
                SoundKind.Leave => Tones(0.11, 880.0, 587.33),                // A5 → D5
                SoundKind.Favorite => Tones(0.09, 659.25, 830.61, 987.77),    // E5 → G#5 → B5
                SoundKind.Message => Tones(0.14, 1046.5),                     // C6
                SoundKind.Poke => Tones(0.07, 1318.5, 0, 1318.5),             // podwójne „ding”
                SoundKind.Recording => Tones(0.12, 987.77, 0, 987.77, 0, 987.77), // trzy krótkie sygnały
                _ => Tones(0.16, 220.0, 0, 220.0),                            // niskie ostrzeżenie
            };
            Cache[kind] = d;
            return d;
        }
    }

    /// <summary>Sekwencja tonów (0 = pauza) z łagodnym wygaszaniem.</summary>
    static float[] Tones(double noteSec, params double[] freqs)
    {
        int noteLen = (int)(Rate * noteSec);
        int total = noteLen * freqs.Length + noteLen * 2;
        var s = new float[total];
        for (int n = 0; n < freqs.Length; n++)
        {
            double f = freqs[n];
            if (f <= 0) continue;
            int start = n * noteLen;
            for (int i = 0; i < noteLen * 3 && start + i < total; i++)
            {
                double t = (double)i / Rate;
                double attack = Math.Min(1, i / (Rate * 0.005));
                double env = attack * Math.Exp(-t * 18);
                double v = (Math.Sin(2 * Math.PI * f * t) + 0.25 * Math.Sin(4 * Math.PI * f * t)) / 1.25;
                s[start + i] = (float)Math.Clamp(s[start + i] + v * env * 0.6, -1, 1);
            }
        }
        return s;
    }

    sealed class RawSamples(float[] data) : ISampleProvider
    {
        int _pos;
        public WaveFormat WaveFormat { get; } = WaveFormat.CreateIeeeFloatWaveFormat(Rate, 1);

        public int Read(float[] buffer, int offset, int count)
        {
            int n = Math.Min(count, data.Length - _pos);
            Array.Copy(data, _pos, buffer, offset, n);
            _pos += n;
            return n;
        }
    }
}
