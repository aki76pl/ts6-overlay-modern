namespace TS6Overlay;

// Monotonic timestamps; no timers or WPF dependencies, so transitions can be tested deterministically.
internal sealed class SpeakerVisibility
{
    sealed class Entry
    {
        public bool Talking;
        public long Stopped;
    }
    readonly Dictionary<(int, int, string, int), Entry> _entries = new();
    static (int, int, string, int) Key(int connection, ClientInfo member) => (connection, member.Id, member.Uid, member.ChannelId);

    public void Observe(OverlayState state, long now)
    {
        var present = new HashSet<(int, int, string, int)>();
        foreach (var server in state.Servers)
        foreach (var member in server.Members.Concat(server.Watched.SelectMany(w => w.Members)))
        {
            var key = Key(server.ConnectionId, member);
            present.Add(key);
            if (member.Talking)
            {
                if (!_entries.TryGetValue(key, out var entry)) _entries[key] = entry = new Entry();
                entry.Talking = true;
            }
            else if (_entries.TryGetValue(key, out var entry) && entry.Talking)
            {
                entry.Talking = false;
                entry.Stopped = now;
            }
        }
        foreach (var key in _entries.Keys.Where(k => !present.Contains(k)).ToList()) _entries.Remove(key);
    }

    public double Opacity(int connection, ClientInfo member, Config cfg, long now)
    {
        if (cfg.ShowChannelList || member.Talking) return 1;
        if (!cfg.SpeakerFade || !_entries.TryGetValue(Key(connection, member), out var entry)) return 0;
        double elapsed = Math.Max(0, now - entry.Stopped);
        double hold = Math.Clamp(cfg.SpeakerHoldSeconds, 0, 10) * 1000.0;
        double fade = Math.Clamp(cfg.SpeakerFadeMilliseconds, 100, 2000);
        return Math.Clamp(1 - (elapsed - hold) / fade, 0, 1);
    }
}
