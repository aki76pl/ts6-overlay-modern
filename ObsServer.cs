using System.Net;
using System.Text;
using System.Text.Json;

namespace TS6Overlay;

/// <summary>
/// Strona dla OBS (źródło „Przeglądarka”): http://localhost:PORT/
/// Parametry: ?only=talking (tylko mówiący), ?scale=1.5, ?notices=0 (bez powiadomień).
/// </summary>
public sealed class ObsServer : IDisposable
{
    HttpListener? _http;
    volatile string _stateJson = "{}";
    readonly List<object> _notices = new();
    int _noticeId;

    public int Port { get; private set; }
    public string Url => $"http://localhost:{Port}/";
    public string? Error { get; private set; }

    public bool Start(int port)
    {
        Stop();
        Port = port;
        try
        {
            _http = new HttpListener();
            _http.Prefixes.Add($"http://localhost:{port}/");
            _http.Prefixes.Add($"http://127.0.0.1:{port}/");
            _http.Start();
            _ = Task.Run(Loop);
            Error = null;
            return true;
        }
        catch (Exception ex)
        {
            Error = ex.Message;
            _http = null;
            return false;
        }
    }

    public void Stop()
    {
        try { _http?.Stop(); _http?.Close(); } catch { }
        _http = null;
    }

    /// <summary>Wywoływane z wątku UI przy każdej zmianie.</summary>
    public void Update(OverlayState s, Theme t, Config cfg, GameProfile? profile = null)
    {
        object Member(ClientInfo m, int myId) => new
        {
            nick = string.IsNullOrWhiteSpace(cfg.FavoriteFor(m.Uid)?.Alias) ? m.Nickname : cfg.FavoriteFor(m.Uid)!.Alias,
            id = m.Id, uid = m.Uid, channelId = m.ChannelId,
            talking = m.Talking,
            whisper = m.Whisper,
            me = m.Id == myId,
            muted = m.InputMuted,
            deaf = m.OutputMuted, country = cfg.ShowFlags && m.Country.Length == 2 ? m.Country.ToLowerInvariant() : null, recording = m.Recording,
            color = cfg.FavoriteFor(m.Uid) is { } favourite ? CssColor(favourite.Color) : null,
        };
        var servers = s.Servers.Select(v => new
        {
            id = v.ConnectionId,
            server = v.Server,
            channel = v.Channel,
            members = v.Members.Select(m => Member(m, v.MyId)),
            watched = v.Watched.Select(w => new { name = w.Name, members = w.Members.Select(m => Member(m, 0)) }),
        });
        object[] notices;
        lock (_notices) notices = _notices.ToArray();
        _stateJson = JsonSerializer.Serialize(new
        {
            connected = s.Servers.Count > 0,
            minimal = profile?.MinimalMode ?? cfg.MinimalMode,
            horizontal = profile?.HorizontalLayout ?? cfg.HorizontalLayout,
            showChannelList = cfg.ShowChannelList,
            speakerFade = cfg.SpeakerFade,
            hold = Math.Clamp(cfg.SpeakerHoldSeconds, 0, 10) * 1000,
            fade = Math.Clamp(cfg.SpeakerFadeMilliseconds, 100, 2000),
            servers,
            texts = new { disconnected = L.T("TeamSpeak: brak połączenia"), me = L.T(" (ty)") },
            notices,
            noticeSeconds = cfg.EventSeconds,
            messageSeconds = cfg.MessageSeconds,
            theme = Theme.ColorFields.ToDictionary(f => f.Prop, f => CssColor(t.GetColor(f.Prop))),
            font = t.Font,
            radius = t.Radius,
            userSpacing = Math.Clamp(cfg.UserSpacing, 0, 20),
            borderWidth = t.BorderWidth,
            square = t.SquareDots,
            shadow = t.TextShadow,
            glow = t.Glow,
        });
    }

    public void AddNotice(Notice n)
    {
        lock (_notices)
        {
            _notices.Add(new { id = ++_noticeId, text = n.Text, kind = n.Kind.ToString(), at = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() });
            while (_notices.Count > 8) _notices.RemoveAt(0);
        }
    }

    /// <summary>#AARRGGBB → rgba() dla CSS.</summary>
    static string CssColor(string hex)
    {
        var c = Theme.C(hex);
        return $"rgba({c.R},{c.G},{c.B},{(c.A / 255.0).ToString("0.###", System.Globalization.CultureInfo.InvariantCulture)})";
    }

    async Task Loop()
    {
        var http = _http;
        while (http != null && http.IsListening)
        {
            HttpListenerContext ctx;
            try { ctx = await http.GetContextAsync(); }
            catch { return; }
            try
            {
                string path = ctx.Request.Url?.AbsolutePath ?? "/";
                byte[] body;
                if (path == "/state")
                {
                    body = Encoding.UTF8.GetBytes(_stateJson);
                    ctx.Response.ContentType = "application/json; charset=utf-8";
                    ctx.Response.Headers["Cache-Control"] = "no-store";
                }
                else if (path == "/")
                {
                    body = Encoding.UTF8.GetBytes(Page);
                    ctx.Response.ContentType = "text/html; charset=utf-8";
                }
                else
                {
                    ctx.Response.StatusCode = 404;
                    body = Array.Empty<byte>();
                }
                ctx.Response.ContentLength64 = body.Length;
                await ctx.Response.OutputStream.WriteAsync(body);
            }
            catch { }
            finally { try { ctx.Response.Close(); } catch { } }
        }
    }

    public void Dispose() => Stop();

    const string Page = """
<!doctype html>
<html lang="pl"><head><meta charset="utf-8"><title>TS6 Overlay — OBS</title>
<style>
  html,body{margin:0;background:transparent;overflow:hidden}
  #root{display:inline-block;padding:8px 10px;min-width:180px;transform-origin:0 0}
  .hdr{font-size:12px;font-weight:600;margin-bottom:4px}
  .row{display:flex;align-items:center;gap:7px;font-size:14px;padding:2px 4px;margin:calc(var(--user-spacing, 2px) / 2) -4px}
  .dot{width:10px;height:10px;border-radius:50%;flex:none}
  .sq .dot{border-radius:0}
  .ic{font-size:11px}
  .n{border-radius:5px;padding:4px 8px;margin:4px 0 0;font-size:13px;transition:opacity .6s}
  .gone{opacity:0}
</style></head>
<body><div id="root"></div>
<script>
const q = new URLSearchParams(location.search);
const onlyTalking = q.get('only') === 'talking';
const speakers = new Map();
const showNotices = q.get('notices') !== '0';
const scale = parseFloat(q.get('scale') || '1');
const root = document.getElementById('root');
root.style.transform = 'scale(' + scale + ')';
const esc = s => s.replace(/[&<>"]/g, c => ({'&':'&amp;','<':'&lt;','>':'&gt;','"':'&quot;'}[c]));
async function tick() {
  try {
    const s = await (await fetch('/state', {cache: 'no-store'})).json();
    const t = s.theme || {};
    const now = performance.now(), present = new Set();
    const minimal = !!s.minimal;
    const opacity = (m, server) => {
      const key = JSON.stringify([server, m.id, m.uid, m.channelId]); present.add(key);
      let old = speakers.get(key);
      if (m.talking) { old = {talking:true, stopped:now}; speakers.set(key, old); }
      else if (old?.talking) { old.talking = false; old.stopped = now; }
      if (!onlyTalking && s.showChannelList || m.talking) return 1;
      if (!s.speakerFade || !old) return 0;
      return Math.max(0, Math.min(1, 1 - (now - old.stopped - s.hold) / s.fade));
    };
    root.style.padding = minimal ? '2px' : '8px 10px';
    root.style.minWidth = minimal ? '0' : '180px';
    root.style.display = minimal && s.horizontal ? 'inline-flex' : 'inline-block';
    root.style.gap = '4px';
    root.style.setProperty('--user-spacing', Math.max(0, Math.min(20, s.userSpacing ?? 2)) + 'px');
    root.style.background = minimal ? 'transparent' : t.Panel; root.style.border = (minimal ? 0 : s.borderWidth) + 'px solid ' + t.PanelBorder;
    root.style.borderRadius = s.radius + 'px'; root.style.fontFamily = "'" + s.font + "', 'Segoe UI', sans-serif";
    root.className = s.square ? 'sq' : '';
    const shadow = s.shadow ? 'text-shadow:0 1px 4px #000;' : '';
    let h = '';
    const row = (m, small, alpha) => {
        const dc = m.talking ? (m.whisper ? t.Whisper : t.Talk) : t.Idle;
        const glow = m.talking && s.glow ? 'box-shadow:0 0 8px ' + dc + ';' : '';
        const nc = m.color || (m.talking ? t.TextTalking : t.Text);
        return '<div class="row" style="opacity:' + alpha + ';background:' + (!minimal && m.talking ? t.TalkRow : 'transparent') + (small ? ';font-size:12px' : '') + '">'
          + '<span class="dot" style="background:' + dc + ';' + glow + '"></span>'
          + (!minimal && m.country ? '<img src="https://flagcdn.com/20x15/' + m.country + '.png" width="18" height="13" style="margin-right:2px">' : '')
          + '<span style="color:' + nc + ';font-weight:' + (m.talking ? 700 : 400) + ';' + shadow + '">' + (!minimal && m.color ? '★ ' : '') + esc(m.nick) + (m.me && !minimal ? s.texts.me : '') + '</span>'
          + (m.recording ? '<span class="ic" style="color:#f33">⏺</span>' : '')
          + (!minimal && m.muted ? '<span class="ic" style="color:' + t.Muted + '">🎙✕</span>' : '')
          + (!minimal && m.deaf ? '<span class="ic" style="color:' + t.Muted + '">🔇</span>' : '')
          + '</div>';
    };
    if (!s.connected) h = '<div class="hdr" style="color:' + t.Header + '">' + esc(s.texts.disconnected) + '</div>';
    else {
      const multi = s.servers.length > 1;
      for (const v of s.servers) {
        if (multi && !minimal) h += '<div class="hdr" style="color:' + t.Header + ';opacity:.75;font-size:11px;margin-top:6px">🖧 ' + esc(v.server) + '</div>';
        if (!minimal) h += '<div class="hdr" style="color:' + t.Header + ';' + shadow + '">🔊 ' + esc(v.channel) + '</div>';
        for (const m of v.members) { const alpha = opacity(m, v.id); if (alpha > 0) h += row(m, false, alpha); }
        for (const w of v.watched) {
          if (!minimal) h += '<div class="hdr" style="color:' + t.Header + ';font-size:11px;margin-top:6px">👁 ' + esc(w.name) + ' (' + w.members.length + ')</div>';
          for (const m of w.members) { const alpha = opacity(m, v.id); if (alpha > 0) h += row(m, true, alpha); }
        }
      }
      if (showNotices) {
        const now = Date.now();
        for (const n of s.notices) {
          const life = ((n.kind === 'Message' || n.kind === 'Poke') ? s.messageSeconds : s.noticeSeconds) * 1000;
          const age = now - n.at;
          if (age > life + 600) continue;
          const bg = {Join: t.Join, Leave: t.Leave, Message: t.Message, Poke: t.Poke, Friend: t.Join}[n.kind] || t.Info;
          const pre = {Join: '➜ ', Leave: '← ', Message: '✉ ', Poke: '👉 '}[n.kind] || '• ';
          h += '<div class="n' + (age > life ? ' gone' : '') + '" style="background:' + bg + ';color:' + t.NoticeText + '">' + pre + esc(n.text) + '</div>';
        }
      }
    }
    for (const key of speakers.keys()) if (!present.has(key)) speakers.delete(key);
    root.innerHTML = h;
  } catch (e) { root.innerHTML = ''; }
}
setInterval(tick, 250); tick();
</script></body></html>
""";
}
