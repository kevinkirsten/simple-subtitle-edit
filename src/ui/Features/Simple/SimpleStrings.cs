using System.Globalization;

namespace Nikse.SubtitleEdit.Features.Simple;

/// <summary>
/// Texts of the simple sync window. Portuguese when the system is in Portuguese, English otherwise.
/// Kept apart from Se.Language on purpose: the simple window has a handful of strings and should
/// not grow the 40-language translation files of the full editor.
/// </summary>
public sealed class SimpleStrings
{
    private static SimpleStrings? _current;

    // Resolved on first use: a field initializer here would run before English/Portuguese below.
    public static SimpleStrings Current
    {
        get => _current ??= For(CultureInfo.CurrentUICulture);
        set => _current = value;
    }

    public static SimpleStrings For(CultureInfo culture) =>
        culture.TwoLetterISOLanguageName == "pt" ? Portuguese : English;

    public required string AppTitle { get; init; }
    public required string DropHint { get; init; }
    public required string OpenVideo { get; init; }
    public required string OpenSubtitle { get; init; }
    public required string Subtitle { get; init; }
    public required string NoSubtitleFound { get; init; }
    public required string Offset { get; init; }
    public required string Reset { get; init; }
    public required string Save { get; init; }
    public required string Saved { get; init; }
    public required string SavedWithBackup { get; init; }
    public required string Audio { get; init; }
    public required string Text { get; init; }
    public required string Overview { get; init; }
    public required string LoadingWaveform { get; init; }
    public required string NoWaveform { get; init; }
    public required string CouldNotLoadSubtitle { get; init; }
    public required string ImageTracksSkipped { get; init; }
    public required string AdvancedMode { get; init; }
    public required string Help { get; init; }
    public required string UnsavedChanges { get; init; }
    public required string NoVideoPlayer { get; init; }
    public required string LinesCount { get; init; }
    public required string OpenFolder { get; init; }
    public required string Previous { get; init; }
    public required string Next { get; init; }
    public required string TagLocal { get; init; }
    public required string TagEmbedded { get; init; }
    public required string TagOnline { get; init; }
    public required string NoVideosInFolder { get; init; }
    public required string UnsavedQuestion { get; init; }
    public required string SaveAndGo { get; init; }
    public required string DiscardAndGo { get; init; }
    public required string Cancel { get; init; }
    public required string FindOnline { get; init; }
    public required string OnlineSettingsTitle { get; init; }
    public required string OnlineApiKey { get; init; }
    public required string OnlineAppName { get; init; }
    public required string OnlineUsername { get; init; }
    public required string OnlinePassword { get; init; }
    public required string OnlineLanguage { get; init; }
    public required string OnlineHelp { get; init; }
    public required string OnlineSearching { get; init; }
    public required string OnlineFound { get; init; }
    public required string OnlineNone { get; init; }
    public required string OnlineDownloading { get; init; }
    public required string OnlineDownloaded { get; init; }

    public static readonly SimpleStrings English = new()
    {
        AppTitle = "Simple Subtitle Edit",
        DropHint = "DROP A VIDEO HERE\nor click OPEN VIDEO",
        OpenVideo = "OPEN VIDEO",
        OpenSubtitle = "OTHER FILE…",
        Subtitle = "SUBTITLE",
        NoSubtitleFound = "No subtitle found next to the video or inside it. Use OTHER FILE…",
        Offset = "OFFSET",
        Reset = "RESET",
        Save = "SAVE",
        Saved = "Saved: {0}",
        SavedWithBackup = "Saved: {0} (old file kept as {1})",
        Audio = "AUDIO",
        Text = "TEXT",
        Overview = "WHOLE VIDEO",
        LoadingWaveform = "Reading audio…",
        NoWaveform = "No audio waveform (is ffmpeg installed?)",
        CouldNotLoadSubtitle = "Could not read this subtitle.",
        ImageTracksSkipped = "Image subtitles (PGS/VobSub) need OCR: use the advanced mode.",
        AdvancedMode = "ADVANCED MODE",
        Help = "PgUp/PgDn prev/next video · SPACE play/pause · ← → seek 1s · , . offset ∓0.1s · < > offset ∓1s · + − zoom · Ctrl+S save · drag the yellow lane to move the subtitle",
        UnsavedChanges = "unsaved",
        NoVideoPlayer = "Video player not found. Install mpv (see README).",
        LinesCount = "{0} · {1} lines",
        OpenFolder = "OPEN FOLDER",
        Previous = "◀ PREV",
        Next = "NEXT ▶",
        TagLocal = "LOCAL",
        TagEmbedded = "IN VIDEO",
        TagOnline = "ONLINE",
        NoVideosInFolder = "No videos in this folder.",
        UnsavedQuestion = "The offset of this subtitle was changed and not saved.",
        SaveAndGo = "SAVE AND GO",
        DiscardAndGo = "DISCARD",
        Cancel = "CANCEL",
        FindOnline = "FIND ONLINE",
        OnlineSettingsTitle = "OpenSubtitles.com",
        OnlineApiKey = "API KEY",
        OnlineAppName = "APP NAME",
        OnlineUsername = "USERNAME",
        OnlinePassword = "PASSWORD",
        OnlineLanguage = "LANGUAGE",
        OnlineHelp = "Create a free account at opensubtitles.com, then an API key in Profile › API consumers. APP NAME is the consumer name you chose there. Searching needs only the key; downloading needs the login and counts against your daily limit (each subtitle is downloaded once, then cached). LANGUAGE: pt-br, en, es…",
        OnlineSearching = "Searching OpenSubtitles…",
        OnlineFound = "{0} subtitles online. Pick one in SUBTITLE to download it.",
        OnlineNone = "Nothing found online for this video.",
        OnlineDownloading = "Downloading {0}…",
        OnlineDownloaded = "Downloaded. {0} downloads left today.",
    };

    public static readonly SimpleStrings Portuguese = new()
    {
        AppTitle = "Simple Subtitle Edit",
        DropHint = "ARRASTE UM VÍDEO AQUI\nou clique em ABRIR VÍDEO",
        OpenVideo = "ABRIR VÍDEO",
        OpenSubtitle = "OUTRO ARQUIVO…",
        Subtitle = "LEGENDA",
        NoSubtitleFound = "Nenhuma legenda ao lado do vídeo nem dentro dele. Use OUTRO ARQUIVO…",
        Offset = "OFFSET",
        Reset = "ZERAR",
        Save = "SALVAR",
        Saved = "Salvo: {0}",
        SavedWithBackup = "Salvo: {0} (o arquivo antigo virou {1})",
        Audio = "ÁUDIO",
        Text = "TEXTO",
        Overview = "VÍDEO INTEIRO",
        LoadingWaveform = "Lendo o áudio…",
        NoWaveform = "Sem onda de áudio (o ffmpeg está instalado?)",
        CouldNotLoadSubtitle = "Não deu para ler essa legenda.",
        ImageTracksSkipped = "Legendas de imagem (PGS/VobSub) precisam de OCR: use o modo avançado.",
        AdvancedMode = "MODO AVANÇADO",
        Help = "PgUp/PgDn vídeo anterior/próximo · ESPAÇO toca/pausa · ← → pula 1s · , . offset ∓0,1s · < > offset ∓1s · + − zoom · Ctrl+S salva · arraste a faixa amarela para mover a legenda",
        UnsavedChanges = "não salvo",
        NoVideoPlayer = "Player de vídeo não encontrado. Instale o mpv (veja o README).",
        LinesCount = "{0} · {1} falas",
        OpenFolder = "ABRIR PASTA",
        Previous = "◀ ANTERIOR",
        Next = "PRÓXIMO ▶",
        TagLocal = "LOCAL",
        TagEmbedded = "NO VÍDEO",
        TagOnline = "ONLINE",
        NoVideosInFolder = "Nenhum vídeo nesta pasta.",
        UnsavedQuestion = "O offset desta legenda mudou e não foi salvo.",
        SaveAndGo = "SALVAR E IR",
        DiscardAndGo = "DESCARTAR",
        Cancel = "CANCELAR",
        FindOnline = "BUSCAR ONLINE",
        OnlineSettingsTitle = "OpenSubtitles.com",
        OnlineApiKey = "CHAVE DE API",
        OnlineAppName = "NOME DO APP",
        OnlineUsername = "USUÁRIO",
        OnlinePassword = "SENHA",
        OnlineLanguage = "IDIOMA",
        OnlineHelp = "Crie uma conta grátis em opensubtitles.com e uma chave em Profile › API consumers. NOME DO APP é o nome que você deu para essa chave. Buscar só precisa da chave; baixar precisa do login e gasta do seu limite diário (cada legenda é baixada uma vez e fica guardada). IDIOMA: pt-br, en, es…",
        OnlineSearching = "Buscando no OpenSubtitles…",
        OnlineFound = "{0} legendas online. Escolha uma em LEGENDA para baixar.",
        OnlineNone = "Nada encontrado online para este vídeo.",
        OnlineDownloading = "Baixando {0}…",
        OnlineDownloaded = "Baixada. Restam {0} downloads hoje.",
    };
}
