using System.IO;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using NAudio.Wave;

namespace TS6Overlay;

internal static class NotificationSoundChecks
{
    sealed class FakePlayer(bool fail = false) : IWavePlayer
    {
        IWaveProvider _provider = null!;
        public float Volume { get; set; } = 1;
        public PlaybackState PlaybackState { get; private set; }
        public WaveFormat OutputWaveFormat => _provider.WaveFormat;
        public byte[] Samples = Array.Empty<byte>();
        public event EventHandler<StoppedEventArgs>? PlaybackStopped;
        public void Init(IWaveProvider provider)
        {
            if (fail) throw new IOException("Simulated audio device failure");
            _provider = provider;
            if (provider.WaveFormat.Encoding != WaveFormatEncoding.Pcm || provider.WaveFormat.BitsPerSample != 16 || provider.WaveFormat.Channels != 2 || provider.WaveFormat.SampleRate != 44100)
                throw new InvalidOperationException("Notification must use PCM16 stereo at 44.1 kHz.");
        }
        public void Play()
        {
            PlaybackState = PlaybackState.Playing;
            using var data = new MemoryStream(); var buffer = new byte[4096]; int read;
            while ((read = _provider.Read(buffer, 0, buffer.Length)) > 0)
            {
                data.Write(buffer, 0, read);
                if (data.Length > 44100 * 4 * 9) throw new InvalidOperationException("Notification did not end.");
            }
            Samples = data.ToArray();
            if (!Samples.Any(b => b != 0)) throw new InvalidOperationException("Notification is silent.");
        }
        public void Pause() => PlaybackState = PlaybackState.Paused;
        public void Stop() { PlaybackState = PlaybackState.Stopped; PlaybackStopped?.Invoke(this, new StoppedEventArgs()); }
        public void Dispose() { }
    }

    internal static void Run(string output)
    {
        var factory = Sounds.OutputFactory; int volume = Sounds.Volume;
        var files = Sounds.Files; string device = Sounds.OutputDeviceName;
        try
        {
            FakePlayer? player = null;
            Sounds.OutputFactory = () => player = new FakePlayer(); Sounds.Volume = 60;
            Sounds.Files = new(); Sounds.OutputDeviceName = "";
            if (!Sounds.Play(SoundKind.Join)) throw new InvalidOperationException(Sounds.LastError);
            var join = player!.Samples;
            if (!Sounds.Play(SoundKind.Leave) || join.SequenceEqual(player!.Samples)) throw new InvalidOperationException("Join and leave sounds must be distinct and non-silent.");
            string invalid = Path.Combine(output, "invalid-sound.wav"); File.WriteAllText(invalid, "not a wave file");
            Sounds.Files[SoundKind.Join.ToString()] = invalid;
            if (!Sounds.Play(SoundKind.Join)) throw new InvalidOperationException("Invalid custom audio must fall back to a built-in sound.");
            Sounds.Files.Clear(); Sounds.Volume = 0;
            if (Sounds.Play(SoundKind.Join)) throw new InvalidOperationException("Zero volume must be reported as muted.");
            Sounds.Volume = 60; Sounds.OutputFactory = () => new FakePlayer(fail: true);
            if (Sounds.Play(SoundKind.Join) || string.IsNullOrWhiteSpace(Sounds.LastError)) throw new InvalidOperationException("Device failure must be reported.");
            Sounds.OutputFactory = () => player = new FakePlayer();
            if (Sounds.ResolveDevice("headset", new[] { new Sounds.OutputDevice("headset", 2) }) != 2 || Sounds.ResolveDevice("missing", Array.Empty<Sounds.OutputDevice>()) != -1)
                throw new InvalidOperationException("Audio output selection/fallback failed.");

            var cfg = new Config { SoundOnJoin = true, SoundOnLeave = true };
            var client = new TsClient(cfg); var kinds = new List<SoundKind>();
            client.Notice += notice =>
            {
                if (NotificationSoundRouter.For(cfg, notice) is { } sound)
                {
                    kinds.Add(sound.Kind);
                    if (!Sounds.Play(sound.Kind, sound.Path)) throw new InvalidOperationException(Sounds.LastError);
                }
            };
            var handle = typeof(TsClient).GetMethod("Handle", BindingFlags.NonPublic | BindingFlags.Instance)!;
            void Feed(string json) => handle.Invoke(client, new object?[] { JsonNode.Parse(json), json });
            Feed("""{"type":"auth","payload":{"connections":[{"id":1,"clientId":1,"status":3,"clientInfos":[{"id":1,"channelId":10,"properties":{"nickname":"Me","uniqueIdentifier":"self"}},{"id":2,"channelId":20,"properties":{"nickname":"Other","uniqueIdentifier":"other"}}]}]}}""");
            Feed("""{"type":"clientMoved","payload":{"connectionId":1,"clientId":2,"oldChannelId":20,"newChannelId":10}}""");
            Feed("""{"type":"clientMoved","payload":{"connectionId":1,"clientId":2,"newChannelId":20}}""");
            if (!kinds.SequenceEqual(new[] { SoundKind.Join, SoundKind.Leave })) throw new InvalidOperationException("Remote events did not play join/leave, including missing old channel.");
            kinds.Clear();
            Feed("""{"type":"clientMoved","payload":{"connectionId":1,"clientId":2,"newChannelId":10}}""");
            Feed("""{"type":"clientMoved","payload":{"connectionId":1,"clientId":2,"newChannelId":0}}""");
            if (!kinds.SequenceEqual(new[] { SoundKind.Join, SoundKind.Leave })) throw new InvalidOperationException("Server disconnect must play leave.");
            kinds.Clear();
            Feed("""{"type":"clientMoved","payload":{"connectionId":1,"clientId":1,"newChannelId":30}}""");
            if (kinds.Count != 0) throw new InvalidOperationException("Moving self must not trigger join/leave sounds.");
            cfg.Favorites.Add(new Favorite { Uid = "friend", SoundPath = "custom.wav" });
            var favourite = NotificationSoundRouter.For(cfg, new Notice("join", NoticeKind.Join, new ClientInfo { Uid = "friend" }));
            if (favourite?.Kind != SoundKind.Favorite || favourite?.Path != "custom.wav") throw new InvalidOperationException("Favourite sound routing failed.");
            cfg.SoundOnLeave = false;
            if (NotificationSoundRouter.For(cfg, new Notice("leave", NoticeKind.Leave)) != null) throw new InvalidOperationException("Explicit disabled leave preference must be respected.");
            var saved = JsonSerializer.Deserialize<Config>("{\"SoundOnJoin\":false,\"SoundOnLeave\":false}")!;
            if (saved.SoundOnJoin || saved.SoundOnLeave || !new Config().SoundOnLeave) throw new InvalidOperationException("New defaults must not override saved sound preferences.");
        }
        finally { Sounds.Dispose(); Sounds.OutputFactory = factory; Sounds.Files = files; Sounds.Volume = volume; Sounds.OutputDeviceName = device; }
    }
}
