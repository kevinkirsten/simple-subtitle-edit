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
    /// <summary>Keyboard and mouse shortcuts: (keys, what they do). "Mod" is Cmd on macOS, Ctrl elsewhere.</summary>
    public required (string Keys, string What)[] Shortcuts { get; init; }
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
    public required string BackToSimple { get; init; }
    public required string SaveWhere { get; init; }
    public required string SaveWhereHelp { get; init; }
    public required string SaveSideFile { get; init; }
    public required string SaveInsideVideo { get; init; }
    public required string Embedding { get; init; }
    public required string SaveLanguageLabel { get; init; }
    public required string SaveWillReplace { get; init; }
    public required string SaveWillKeep { get; init; }
    public required string SaveNothingInLanguage { get; init; }
    public required string TrackText { get; init; }
    public required string SaveLanguageInfo { get; init; }
    public required string OnlineLanguageInfo { get; init; }
    public required string TrackImage { get; init; }
    public required string Embedded { get; init; }
    public required string PlexNotified { get; init; }
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
        Shortcuts =
        [
            ("SPACE", "play / pause"),
            ("← →", "back / forward 1 s"),
            ("PgUp PgDn", "previous / next video"),
            (", .", "offset −0.1 s / +0.1 s"),
            ("< >", "offset −1 s / +1 s"),
            ("+ −", "zoom in / out"),
            ("Mod+S", "save"),
            ("drag yellow lane", "move the whole subtitle"),
            ("Mod + click/drag", "move the red cursor"),
            ("wheel", "scroll · Mod+wheel zooms"),
        ],
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
        BackToSimple = "◀ Simple mode",
        SaveWhere = "Where should this subtitle go?",
        SaveWhereHelp = "NEXT TO THE VIDEO writes a .srt with the video's name. INSIDE THE VIDEO adds it to the mkv as a text track in the language above (can be turned off, and edited here again) and deletes the .srt. The mkv is rewritten (a few seconds per GB) and checked before it replaces the original.",
        SaveSideFile = "NEXT TO THE VIDEO (.srt)",
        SaveInsideVideo = "INSIDE THE VIDEO ({0})",
        Embedding = "Writing the subtitle into the video… (rewrites the mkv)",
        SaveLanguageLabel = "LANGUAGE",
        SaveWillReplace = "⚠ Already inside in this language, will be REPLACED: {0}.",
        SaveWillKeep = "Also inside, kept as they are: {0}.",
        SaveNothingInLanguage = "Nothing in this language inside the video yet.",
        TrackText = "text",
        SaveLanguageInfo = "The language is written as the track's tag (pt-BR, en, es-419…), which Plex and players use to pick and name the subtitle. Variants are different tracks: saving pt-BR never touches a pt-PT track. Only mkv files can hold it; other formats save the .srt next to the video.",
        OnlineLanguageInfo = "Which subtitles FIND ONLINE asks OpenSubtitles.com for. Some languages have their own code there (Latin American Spanish is \"ea\", Chinese is \"zh-cn\" / \"zh-tw\"); the app converts it, you only pick the name.",
        TrackImage = "image",
        Embedded = "Saved inside the video as track {0}.",
        PlexNotified = " Plex was told to reload it.",
        OnlineSettingsTitle = "OpenSubtitles.com",
        OnlineApiKey = "API KEY",
        OnlineAppName = "APP NAME",
        OnlineUsername = "USERNAME",
        OnlinePassword = "PASSWORD",
        OnlineLanguage = "LANGUAGE",
        OnlineHelp = "Create a free account at opensubtitles.com, then an API key in Profile › API consumers. APP NAME is the consumer name you chose there. Searching needs only the key; downloading needs the login and counts against your daily limit (each subtitle is downloaded once, then cached).",
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
        Shortcuts =
        [
            ("ESPAÇO", "toca / pausa"),
            ("← →", "volta / avança 1 s"),
            ("PgUp PgDn", "vídeo anterior / próximo"),
            (", .", "offset −0,1 s / +0,1 s"),
            ("< >", "offset −1 s / +1 s"),
            ("+ −", "aproxima / afasta"),
            ("Mod+S", "salva"),
            ("arraste a faixa amarela", "move a legenda inteira"),
            ("Mod + clique/arraste", "move o cursor vermelho"),
            ("rodinha", "rola · Mod+rodinha dá zoom"),
        ],
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
        BackToSimple = "◀ Modo simples",
        SaveWhere = "Onde salvar esta legenda?",
        SaveWhereHelp = "AO LADO DO VÍDEO grava um .srt com o nome do vídeo. DENTRO DO VÍDEO coloca a legenda no mkv como faixa de texto no idioma acima (dá para desligar e editar aqui de novo) e apaga o .srt. O mkv é regravado (alguns segundos por GB) e conferido antes de substituir o original.",
        SaveSideFile = "AO LADO DO VÍDEO (.srt)",
        SaveInsideVideo = "DENTRO DO VÍDEO ({0})",
        Embedding = "Gravando a legenda dentro do vídeo… (regrava o mkv)",
        SaveLanguageLabel = "IDIOMA",
        SaveWillReplace = "⚠ Já existe dentro do vídeo neste idioma e será SUBSTITUÍDA: {0}.",
        SaveWillKeep = "Também dentro, ficam como estão: {0}.",
        SaveNothingInLanguage = "Ainda não há nada neste idioma dentro do vídeo.",
        TrackText = "texto",
        SaveLanguageInfo = "O idioma vira a marca da faixa (pt-BR, en, es-419…), que o Plex e os players usam para escolher e nomear a legenda. Variantes são faixas diferentes: salvar pt-BR nunca mexe numa faixa pt-PT. Só arquivos mkv guardam a faixa; nos outros formatos o .srt fica ao lado do vídeo.",
        OnlineLanguageInfo = "Em qual idioma o BUSCAR ONLINE procura legendas no OpenSubtitles.com. Alguns idiomas têm código próprio lá (espanhol latino é \"ea\", chinês é \"zh-cn\" / \"zh-tw\"); o app converte, você só escolhe o nome.",
        TrackImage = "imagem",
        Embedded = "Salva dentro do vídeo como faixa {0}.",
        PlexNotified = " O Plex foi avisado para recarregar.",
        OnlineSettingsTitle = "OpenSubtitles.com",
        OnlineApiKey = "CHAVE DE API",
        OnlineAppName = "NOME DO APP",
        OnlineUsername = "USUÁRIO",
        OnlinePassword = "SENHA",
        OnlineLanguage = "IDIOMA",
        OnlineHelp = "Crie uma conta grátis em opensubtitles.com e uma chave em Profile › API consumers. NOME DO APP é o nome que você deu para essa chave. Buscar só precisa da chave; baixar precisa do login e gasta do seu limite diário (cada legenda é baixada uma vez e fica guardada).",
        OnlineSearching = "Buscando no OpenSubtitles…",
        OnlineFound = "{0} legendas online. Escolha uma em LEGENDA para baixar.",
        OnlineNone = "Nada encontrado online para este vídeo.",
        OnlineDownloading = "Baixando {0}…",
        OnlineDownloaded = "Baixada. Restam {0} downloads hoje.",
    };
}
