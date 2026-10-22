using Dalamud.Game;
using Dalamud.Plugin.Services;
using Lumina.Excel.Sheets;

namespace Glance;

internal sealed class EmoteCatalog
{
    private readonly IDataManager data;
    private readonly IUnlockState unlocks;
    private readonly List<(Emote Row, ExpressionDefinition Definition, string EnglishCommand)> entries = new();
    private IReadOnlyList<ExpressionDefinition>? cachedPool;
    private IReadOnlyList<ExpressionDefinition>? cachedAll;
    private IReadOnlyList<ExpressionDefinition>? cachedFacial;
    private (Personality, ExpressionChoices, bool) poolKey;
    private long refreshAt;
    private HashSet<uint> unlockedIds = new();
    public EmoteCatalog(IDataManager data, IUnlockState unlocks) { this.data = data; this.unlocks = unlocks; }

    public void Load()
    {
        if (entries.Count != 0) return;
        var english = data.GetExcelSheet<Emote>(ClientLanguage.English);
        foreach (var row in data.GetExcelSheet<Emote>())
        {
            if (row.RowId == 0 || !row.TextCommand.IsValid) continue;
            var command = row.TextCommand.Value.Command.ToString().Trim();
            var name = row.Name.ToString();
            if (string.IsNullOrWhiteSpace(name) || !ValidCommand(command)) continue;
            var englishRow = english.GetRow(row.RowId);
            var englishCommand = englishRow.TextCommand.IsValid ? englishRow.TextCommand.Value.Command.ToString().Trim() : command;
            // Neutral and state/perspective controls are not personality performances.
            if (englishCommand is "/straightface" or "/facecamera" or "/cpose" or "/sit" or "/groundsit") continue;
            var legacy = Expressions.All.FirstOrDefault(e => e.Command == englishCommand + " motion");
            var body = !PersonalityThemes.FacialCommands.Contains(englishCommand);
            var definition = new ExpressionDefinition(legacy?.Kind ?? ExpressionKind.GameEmote,
                name, command + " motion", legacy?.Hold ?? (body ? 8f : 2f), body, row.RowId);
            entries.Add((row, definition, englishCommand));
        }
        entries.Sort((a, b) => string.Compare(a.Definition.Name, b.Definition.Name, StringComparison.CurrentCultureIgnoreCase));
        Invalidate();
    }

    private static bool ValidCommand(string command) => command.StartsWith('/') && command.Length > 1
        && !command.Any(char.IsWhiteSpace) && !command.Contains(';');

    public IReadOnlyList<ExpressionDefinition> Available(bool allowBody)
    {
        RefreshUnlocks();
        if (allowBody) return cachedAll ??= BuildAvailable(true);
        return cachedFacial ??= BuildAvailable(false);
    }

    private IReadOnlyList<ExpressionDefinition> BuildAvailable(bool allowBody) =>
        entries.Where(e => (allowBody || !e.Definition.BodyMotion) && unlockedIds.Contains(e.Row.RowId))
            .Select(e => e.Definition).ToArray();

    public void InvalidatePool() => cachedPool = null;
    private void InvalidateCaches() { InvalidatePool(); cachedAll = cachedFacial = null; }
    public void Invalidate() { refreshAt = 0; unlockedIds.Clear(); InvalidateCaches(); }
    private void RefreshUnlocks()
    {
        if (Environment.TickCount64 < refreshAt) return;
        var refreshed = entries.Where(e => unlocks.IsEmoteUnlocked(e.Row)).Select(e => e.Row.RowId).ToHashSet();
        if (!unlockedIds.SetEquals(refreshed))
        {
            unlockedIds = refreshed;
            InvalidateCaches();
        }
        refreshAt = Environment.TickCount64 + 1000;
    }

    public IReadOnlyList<ExpressionDefinition> Pool(Configuration config)
    {
        if (config.Personality == Personality.Off) return [];
        RefreshUnlocks();
        var key = (config.Personality, config.CustomExpressions, config.AllowBodyMotions);
        if (cachedPool != null && key == poolKey) return cachedPool;
        poolKey = key;
        var legacy = Expressions.Pool(config.Personality, config.CustomExpressions);
        return cachedPool = entries.Where(e => (config.AllowBodyMotions || !e.Definition.BodyMotion)
            && (config.Personality == Personality.Custom
                ? (legacy & e.Definition.Choice) != 0 || config.CustomEmoteIds.Contains(e.Row.RowId)
                : (legacy & e.Definition.Choice) != 0 || PersonalityThemes.Matches(config.Personality, e.EnglishCommand, e.Definition.BodyMotion))
            && unlockedIds.Contains(e.Row.RowId)).Select(e => e.Definition).ToArray();
    }
}
