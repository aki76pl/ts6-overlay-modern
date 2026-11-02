namespace TS6Overlay;

internal static class NotificationSoundRouter
{
    internal static (SoundKind Kind, string? Path)? For(Config config, Notice notice)
    {
        var favourite = notice.Who == null ? null : config.FavoriteFor(notice.Who.Uid);
        return notice.Kind switch
        {
            NoticeKind.Join when config.SoundOnJoin => favourite == null ? (SoundKind.Join, null) : (SoundKind.Favorite, favourite.SoundPath),
            NoticeKind.Leave when config.SoundOnLeave => (SoundKind.Leave, null),
            NoticeKind.Friend when config.SoundOnJoin && favourite != null => (SoundKind.Favorite, favourite.SoundPath),
            NoticeKind.Message when config.SoundOnMessage => (SoundKind.Message, null),
            NoticeKind.Poke when config.SoundOnPoke => (SoundKind.Poke, null),
            NoticeKind.Recording when config.SoundOnRecording => (SoundKind.Recording, null),
            _ => null,
        };
    }
}
