namespace Glance;

internal static class PersonalityThemes
{
    // Curated pools resolve against the installed game's catalog; absent commands are skipped.
    public static readonly Dictionary<Personality, string[]> Commands = new()
    {
        [Personality.Shy] = ["/blush", "/shuteyes", "/lookaway", "/fidget", "/smile"],
        [Personality.Confident] = ["/smirk", "/grin", "/pose", "/vpose", "/thumbsup", "/flex"],
        [Personality.Cheerful] = ["/beam", "/biggrin", "/smile", "/joy", "/happy", "/chuckle", "/laugh"],
        [Personality.Mischievous] = ["/wink", "/fakesmile", "/smirk", "/taunt", "/sneer", "/cackle"],
        [Personality.Thoughtful] = ["/think", "/ponder", "/hmm", "/doubt", "/question", "/contemplate"],
        [Personality.Affectionate] = ["/puckerup", "/smile", "/blowkiss", "/dote", "/hug", "/heart"],
        [Personality.Melancholic] = ["/sad", "/upset", "/worried", "/sigh", "/cry", "/sulk"],
        [Personality.Theatrical] = ["/amazed", "/surprised", "/shock", "/stagger", "/gasp", "/panic"],
        [Personality.Celebratory] = ["/joy", "/cheer", "/clap", "/huzzah", "/victory", "/vpose", "/highfive"],
        [Personality.DanceParty] = ["/dance", "/stepdance", "/harvestdance", "/balldance", "/mandervilledance", "/beesknees", "/sundropdance", "/moonlift", "/sidestep"],
    };

    public static readonly HashSet<string> FacialCommands = new(StringComparer.OrdinalIgnoreCase)
    {
        "/wink", "/smile", "/fakesmile", "/puckerup", "/amazed", "/taunt",
        "/beam", "/grin", "/biggrin", "/smirk", "/sneer", "/angry", "/annoyed",
        "/furious", "/sad", "/upset", "/worried", "/scared", "/shuteyes", "/alert",
    };

    public static bool Matches(Personality mood, string command, bool body)
        => mood == Personality.FacialShuffle ? !body
            : Commands.TryGetValue(mood, out var commands) && commands.Contains(command, StringComparer.OrdinalIgnoreCase);
}
