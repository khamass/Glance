using System.Numerics;
using System.IO;
using System.Text.Json;
using Dalamud.Bindings.ImGui;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Game.Command;
using Dalamud.Game.Config;
using Dalamud.IoC;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.Game.Control;
using NativeCharacter = FFXIVClientStructs.FFXIV.Client.Game.Character.Character;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Client.System.String;

namespace Glance;

public sealed unsafe class Plugin : IDalamudPlugin
{
    [PluginService] internal static IDalamudPluginInterface Interface { get; private set; } = null!;
    [PluginService] internal static IFramework Framework { get; private set; } = null!;
    [PluginService] internal static IObjectTable Objects { get; private set; } = null!;
    [PluginService] internal static IPlayerState PlayerState { get; private set; } = null!;
    [PluginService] internal static IClientState Client { get; private set; } = null!;
    [PluginService] internal static ICondition Condition { get; private set; } = null!;
    [PluginService] internal static ICommandManager Commands { get; private set; } = null!;
    [PluginService] internal static IPluginLog Log { get; private set; } = null!;
    [PluginService] internal static ITextureProvider Textures { get; private set; } = null!;
    [PluginService] internal static IGameInteropProvider Interop { get; private set; } = null!;
    [PluginService] internal static IGameConfig GameConfig { get; private set; } = null!;
    [PluginService] internal static IDataManager Data { get; private set; } = null!;
    [PluginService] internal static IUnlockState Unlocks { get; private set; } = null!;

    private readonly Configuration config;
    private readonly SettingsSaveDebounce settingsSave = new();
    private bool recoveryRequired;
    private int recoveryRequest; // 1: restore saved angle, 2: keep current angle
    private bool disposed;
    private readonly GposeSession gpose = new();
    private bool open;
    private bool active;
    private nint lastPlayer;
    private Vector3 lastPosition;
    private float idleSeconds;
    private float distance;
    private string status = "Waiting for player";
    private bool ownsGaze;
    private bool attemptedGaze;
    private int gazeSource = -1;
    private int captureFrames;
    private Vector3 gazeVector;
    private readonly WinkCadence winkCadence = new();
    private int restoreGazeFrames;
    private bool winkPending;
    private float zoomStillSeconds;
    private float previousZoom = float.NaN;
    private readonly RotationCadence rotationCadence = new();
    private bool closeZoomSeen;
    private readonly ExpressionPlayback expressions = new();
    private readonly ExpressionPicker expressionPicker = new();
    private ExtendedCloseUp? closeUp;
    private string? closeUpError;
    private readonly EmoteCatalog catalog;
    private string emoteSearch = "";

    public Plugin()
    {
        catalog = new EmoteCatalog(Data, Unlocks);
        config = LoadConfiguration();
        config.Sanitize();
        recoveryRequired = config.PendingAngleRecovery != null;
        open = recoveryRequired;
        SaveConfiguration();
        Commands.AddHandler("/glance", new CommandInfo(OnCommand) { HelpMessage = "Open Glance. /glance on | off | toggle" });
        Interface.UiBuilder.Draw += Draw;
        Interface.UiBuilder.OpenConfigUi += Open;
        Interface.UiBuilder.OpenMainUi += Open;
        Framework.Update += Update;
    }

    private void SaveConfiguration()
    {
        Interface.SavePluginConfig(config);
        settingsSave.Saved();
    }

    private bool BeginAngleOverride(float original)
    {
        if (recoveryRequired || config.PendingAngleRecovery != null || !Client.IsLoggedIn) return false;
        if (!PlayerState.IsLoaded
            || !GameConfig.TryGet(SystemConfigOption.PadMode, out uint systemMode)
            || !GameConfig.TryGet(UiConfigOption.PadMode, out uint characterMode)) return false;
        var backup = new AngleRecovery { ContentId = PlayerState.ContentId, Original = original,
            SystemPadMode = systemMode, CharacterPadMode = characterMode };
        if (!backup.IsValid) return false;
        config.PendingAngleRecovery = backup;
        try { SaveConfiguration(); }
        catch { config.PendingAngleRecovery = null; throw; }
        return true;
    }

    private bool CanRestoreAngle() => Client.IsLoggedIn && PlayerState.IsLoaded
        && GameConfig.TryGet(SystemConfigOption.PadMode, out uint systemMode)
        && GameConfig.TryGet(UiConfigOption.PadMode, out uint characterMode)
        && config.PendingAngleRecovery?.Matches(PlayerState.ContentId, systemMode, characterMode) == true;

    private void EndAngleOverride(bool restored)
    {
        if (restored)
        {
            var backup = config.PendingAngleRecovery;
            config.PendingAngleRecovery = null;
            try { SaveConfiguration(); }
            catch
            {
                config.PendingAngleRecovery = backup;
                recoveryRequired = true; open = true;
                throw;
            }
        }
        else { recoveryRequired = true; open = true; }
    }

    private void ProcessAngleRecovery()
    {
        var request = recoveryRequest;
        recoveryRequest = 0;
        if (!recoveryRequired || request == 0 || !CanRestoreAngle()) return;
        if (request == 1)
        {
            GameConfig.Set(UiControlOption.TiltOffset, config.PendingAngleRecovery!.Original);
            var manager = CameraManager.Instance();
            if (manager != null && manager->Camera != null) manager->Camera->UpdateTiltOffset();
        }
        // Require an explicit re-enable, so recovery is not immediately overwritten in a close-up.
        config.OverrideCloseUpAngle = false;
        config.PendingAngleRecovery = null;
        SaveConfiguration();
        recoveryRequired = false;
    }

    private void DrawAngleRecovery()
    {
        if (!recoveryRequired) return;
        ImGui.TextWrapped("A previous camera-angle override did not finish. Angle overrides are paused until you choose how to recover.");
        var backup = config.PendingAngleRecovery;
        if (backup?.IsValid != true)
        {
            ImGui.TextWrapped("The saved backup is invalid. Keep your current angle to discard it.");
            if (ImGui.Button("Discard invalid camera backup"))
            {
                config.PendingAngleRecovery = null;
                config.OverrideCloseUpAngle = false;
                SaveConfiguration();
                recoveryRequired = false;
            }
        }
        else if (CanRestoreAngle())
        {
            var original = Math.Clamp((backup.Original + 0.08f) / 0.29f * 100f, 0, 100);
            ImGui.TextUnformatted($"Saved camera angle: {original:0}");
            if (ImGui.Button("Restore saved angle")) recoveryRequest = 1;
            ImGui.SameLine();
            if (ImGui.Button("Keep current angle")) recoveryRequest = 2;
        }
        else ImGui.TextWrapped("Log in to the original character and use the same keyboard/gamepad control mode to restore or discard this backup.");
        ImGui.Separator();
    }

    private void Open() => open = true;

    private static Configuration LoadConfiguration()
    {
        if (Interface.GetPluginConfig() is Configuration current) return current;
        // Import the previous identity once, leaving the original file intact.
        var previous = Path.Combine(Interface.ConfigFile.DirectoryName!, "ZoomFace.json");
        if (!Interface.ConfigFile.Exists && File.Exists(previous))
        {
            try
            {
                var imported = JsonSerializer.Deserialize<Configuration>(File.ReadAllText(previous),
                    new JsonSerializerOptions { IncludeFields = true, PropertyNameCaseInsensitive = true });
                if (imported != null) return imported;
            }
            catch (Exception ex) { Log.Warning(ex, "Could not import previous plugin settings; using defaults."); }
        }
        return new Configuration();
    }
    private void OnCommand(string command, string args)
    {
        switch (args.Trim().ToLowerInvariant())
        {
            case "on": config.Enabled = true; break;
            case "off": config.Enabled = false; break;
            case "toggle": config.Enabled = !config.Enabled; break;
            default: open = !open; return;
        }
        Pause(config.Enabled ? "Enabled" : "Disabled");
        if (!config.Enabled) closeUp?.Restore();
        SaveConfiguration();
    }

    private void ReleaseGaze()
    {
        // Never dereference a cached actor pointer after a logout or actor replacement.
        var player = Objects.LocalPlayer;
        if (ownsGaze && player != null && player.Address == lastPlayer)
        {
            var character = (NativeCharacter*)player.Address;
            if (character->LookAt.IsFacingCamera && Client.IsLoggedIn)
                ToggleFaceCamera();
        }
        ownsGaze = false;
    }

    private static bool ToggleFaceCamera() => ExecuteGameCommand("/facecamera");

    private static bool ExecuteGameCommand(string text)
    {
        var ui = UIModule.Instance();
        if (ui == null) return false;
        var command = Utf8String.FromString(text);
        try { ui->ProcessChatBoxEntry(command); }
        finally { command->Dtor(true); }
        return true;
    }

    private void Pause(string reason)
    {
        CancelExpression();
        winkPending = false;
        rotationCadence.ResetMotion();
        restoreGazeFrames = 0;
        ReleaseGaze(); attemptedGaze = false; gazeSource = -1;
        captureFrames = 0; active = false; status = reason;
    }

    private bool SendExpression(string command)
    {
        var sent = ExecuteGameCommand(command);
        if (sent && ownsGaze) restoreGazeFrames = 2;
        return sent;
    }

    private void CancelExpression(bool forceNeutral = false)
    {
        var player = Objects.LocalPlayer;
        expressions.Cancel(Client.IsLoggedIn && player != null ? player.Address : 0,
            SendExpression, forceNeutral);
    }

    private void HandoffToGpose()
    {
        expressions.Discard();
        ownsGaze = false; attemptedGaze = false; gazeSource = -1; captureFrames = 0;
        restoreGazeFrames = 0; active = false; winkPending = false; closeZoomSeen = false;
        winkCadence.Reset(); rotationCadence.Reset(); previousZoom = float.NaN; zoomStillSeconds = 0;
        status = "GPose handoff: gaze and expression preserved; Glance is paused";
        Log.Information("GPose handoff: discarded scheduled commands without releasing gaze or expression.");
    }

    private void Update(IFramework framework)
    {
        if (disposed) return;
        try
        {
            // Handle GPose before any scheduled expression or zoom-exit cleanup can execute.
            var gposeTransition = gpose.Update(Client.IsGPosing, config.KeepPoseOnGpose && config.Enabled,
                Objects.LocalPlayer?.Address ?? 0, ownsGaze);
            if (Client.IsGPosing)
            {
                if (gposeTransition.Entered)
                {
                    if (gposeTransition.Preserve) HandoffToGpose();
                    else Pause("Paused in group pose");
                    closeUp?.Restore();
                }
                if (settingsSave.Due(Environment.TickCount64)) SaveConfiguration();
                return;
            }
            if (gposeTransition.Exited)
            {
                // Reclaim only the normal-world gaze that Glance originally owned.
                ownsGaze = gposeTransition.ReclaimGaze;
                Pause("Leaving group pose; waiting for idle");
                idleSeconds = 0;
            }
            ProcessAngleRecovery();
            if (settingsSave.Due(Environment.TickCount64)) SaveConfiguration();
            UpdateCloseUp();
            Tick((float)framework.UpdateDelta.TotalSeconds);
        }
        catch (Exception ex)
        {
            config.Enabled = false;
            Log.Error(ex, "Glance update failed; disabled until manually enabled.");
            try { SaveConfiguration(); }
            catch (Exception saveError) { Log.Error(saveError, "Could not persist disabled state."); }
            try { closeUp?.Restore(); }
            catch (Exception restoreError) { Log.Error(restoreError, "Could not restore camera settings; recovery backup retained."); }
            Pause("Disabled after an error; see /xllog");
        }
    }

    private void UpdateCloseUp()
    {
        var player = Objects.LocalPlayer;
        var eligible = config.Enabled && config.ExtendedCloseUp && player != null && Client.IsLoggedIn
            && !Client.IsGPosing && !Client.IsPvP && !player.IsDead
            && !Condition[ConditionFlag.InCombat] && !Condition[ConditionFlag.Mounted]
            && !Condition[ConditionFlag.WatchingCutscene] && !Condition[ConditionFlag.WatchingCutscene78]
            && !Condition[ConditionFlag.OccupiedInCutSceneEvent] && !Condition[ConditionFlag.OccupiedInEvent]
            && !Condition[ConditionFlag.OccupiedInQuestEvent] && !Condition[ConditionFlag.BetweenAreas]
            && !Condition[ConditionFlag.BetweenAreas51] && !Condition[ConditionFlag.Casting]
            && !Condition[ConditionFlag.Occupied];
        if (eligible && closeUp == null && closeUpError == null)
        {
            try { closeUp = new ExtendedCloseUp(Interop, GameConfig, BeginAngleOverride, CanRestoreAngle, EndAngleOverride); }
            catch (Exception ex)
            {
                closeUpError = "Extended close-up unavailable; see /xllog";
                Log.Error(ex, "Could not initialize close-up visibility hook; camera limits unchanged.");
            }
        }
        closeUp?.Update(eligible, player?.Address ?? 0, config.MinimumCameraDistance,
            config.OverrideCloseUpAngle && !recoveryRequired, config.CloseUpCameraAngle, config.CloseUpAngleDistance,
            config.FineCloseUpZoom, config.Distance, config.CloseUpZoomDelta,
            (float)Framework.UpdateDelta.TotalSeconds, config.CloseUpTransitionSeconds);
    }

    private void Tick(float dt)
    {
        var player = Objects.LocalPlayer;
        if (player == null || !Client.IsLoggedIn)
        {
            catalog.Invalidate();
            winkCadence.LeaveZoom();
            Pause("Waiting for player"); lastPlayer = 0; idleSeconds = 0; return;
        }
        var position = player.Position;
        if (lastPlayer != player.Address)
        {
            catalog.Invalidate();
            closeZoomSeen = false; expressions.Discard(); winkCadence.LeaveZoom();
            ReleaseGaze(); lastPlayer = player.Address; lastPosition = position; idleSeconds = 0;
            Pause("Waiting for idle"); return;
        }
        catalog.Load();
        // Observe the physical zoom exit even if movement already paused gaze.
        // Cancel the expression sequence before sending neutral: /smile followed
        // immediately by /straightface can hit the game's emote lock.
        var zoomExited = false;
        var exitCameraManager = CameraManager.Instance();
        if (!config.Enabled) closeZoomSeen = false;
        else if (exitCameraManager != null && exitCameraManager->ActiveCameraIndex == 0
            && exitCameraManager->Camera != null)
        {
            var zoom = exitCameraManager->Camera->Distance;
            if (float.IsFinite(zoom) && zoom <= config.Distance) closeZoomSeen = true;
            else if (float.IsFinite(zoom) && zoom > config.Distance + 0.35f && closeZoomSeen)
            {
                closeZoomSeen = false;
                zoomExited = true;
                winkPending = false;
                CancelExpression(forceNeutral: catalog.Pool(config).Count > 0);
            }
        }
        if (!zoomExited) expressions.Update(dt, player.Address, SendExpression);
        // Observe zoom separately from idle/activity pauses: walking or changing
        // settings while close must not count as another zoom-in.
        var zoomManager = CameraManager.Instance();
        if (zoomManager != null && zoomManager->ActiveCameraIndex == 0 && zoomManager->Camera != null)
        {
            var currentZoom = zoomManager->Camera->Distance;
            zoomStillSeconds = float.IsFinite(previousZoom) && float.IsFinite(currentZoom)
                && MathF.Abs(currentZoom - previousZoom) <= 0.001f
                ? zoomStillSeconds + Math.Max(dt, 0f) : 0;
            previousZoom = currentZoom;
            if (currentZoom > config.Distance + 0.35f) winkCadence.LeaveZoom();
        }
        else { previousZoom = float.NaN; zoomStillSeconds = 0; }
        var moved = Vector3.DistanceSquared(position, lastPosition) > 0.000001f;
        lastPosition = position;
        idleSeconds = moved ? 0 : idleSeconds + Math.Clamp(dt, 0f, 0.1f);
        if (!config.Enabled) { Pause("Disabled"); return; }
        if (Client.IsPvP || Condition[ConditionFlag.InCombat] || Condition[ConditionFlag.Mounted]
            || Condition[ConditionFlag.WatchingCutscene] || Condition[ConditionFlag.WatchingCutscene78]
            || Condition[ConditionFlag.OccupiedInCutSceneEvent] || Condition[ConditionFlag.OccupiedInEvent]
            || Condition[ConditionFlag.OccupiedInQuestEvent] || Condition[ConditionFlag.BetweenAreas]
            || Condition[ConditionFlag.BetweenAreas51] || Condition[ConditionFlag.Casting]
            || Condition[ConditionFlag.Occupied] || player.IsDead)
        { idleSeconds = 0; Pause("Paused by game activity"); return; }
        if (idleSeconds < 0.4f) { Pause("Waiting for idle"); return; }
        var manager = CameraManager.Instance();
        if (manager == null || manager->ActiveCameraIndex != 0 || manager->Camera == null)
        { Pause("Waiting for normal camera"); return; }
        var camera = manager->Camera;
        distance = camera->Distance;
        if (camera->ZoomMode != CameraZoomMode.ThirdPerson || camera->IsEventCameraAutoControl
            || camera->ControlMode == CameraControlMode.LockonThirdPerson)
        { Pause("Paused by camera mode"); return; }
        if (!FacingMath.InRange(distance, config.Distance, active)) { Pause("Zoom in to activate"); return; }
        var native = (NativeCharacter*)player.Address;
        var pool = catalog.Pool(config);
        if (rotationCadence.Update(camera->DirH, camera->DirV, dt, config.ExpressionFrequency)
            && pool.Count > 0)
            winkPending = true;
        if (restoreGazeFrames > 0 && --restoreGazeFrames == 0 && ownsGaze
            && !native->LookAt.IsFacingCamera)
        {
            // An expression can cancel gaze. Restore it once, without toggling
            // an already-active gaze off or repeatedly fighting manual input.
            if (!ToggleFaceCamera()) { Pause("Waiting for game UI"); return; }
        }
        // Activate once. Keep gaze enabled while updating its native camera vector.
        // Preserve gaze that the player enabled manually before entering range.
        if (!attemptedGaze)
        {
            if (!native->LookAt.IsFacingCamera)
            {
                if (!ToggleFaceCamera()) { Pause("Waiting for game UI"); return; }
                ownsGaze = true;
            }
            attemptedGaze = true;
            captureFrames = 2;
            // Capture immediately while the camera still matches the command.
            // Waiting several frames can miss the match during an active orbit.
            CaptureGazeSource(native, camera);
            if (gazeSource >= 0) captureFrames = 0;
        }
        else if (ownsGaze && native->LookAt.IsFacingCamera)
        {
            // Only write the world-position format after a matching native capture.
            if (captureFrames > 0)
            {
                captureFrames--;
                CaptureGazeSource(native, camera);
            }
            else if (gazeSource >= 0)
            {
                var target = CameraPosition(camera);
                if (float.IsFinite(target.X) && float.IsFinite(target.Y) && float.IsFinite(target.Z))
                {
                    gazeVector = Vector3.Lerp(gazeVector, target,
                        1f - MathF.Exp(-config.Response * Math.Clamp(dt, 0f, 0.1f)));
                    native->LookAt.CameraVector = new() { X = gazeVector.X, Y = gazeVector.Y, Z = gazeVector.Z };
                }
            }
        }
        active = true;
        if (captureFrames == 0 && native->LookAt.IsFacingCamera
            && winkCadence.EnterZoom(config.ExpressionFrequency) && pool.Count > 0)
            winkPending = true;
        if (winkPending && zoomStillSeconds >= config.ExpressionDelay
            && rotationCadence.StillSeconds >= config.ExpressionDelay
            && !expressions.IsBusy && native->LookAt.IsFacingCamera)
        {
            winkPending = false;
            var selected = expressionPicker.Pick(pool, Random.Shared);
            if (selected != null)
                expressions.Start(selected, selected.Kind == ExpressionKind.GameEmote
                    ? config.EmoteHolds.GetValueOrDefault(selected.EmoteId, selected.Hold)
                    : config.ExpressionHolds[selected.Kind], player.Address, SendExpression);
        }
        status = ownsGaze && captureFrames == 0 && gazeSource < 0 ? "Gaze held; camera vector format not recognized"
            : native->LookAt.IsFacingCamera ? "Native /facecamera gaze active"
            : "Gaze cancelled or unavailable; zoom out and back in to retry";
    }

    private void CaptureGazeSource(NativeCharacter* native, Camera* camera)
    {
        var captured = native->LookAt.CameraVector;
        var original = new Vector3(captured.X, captured.Y, captured.Z);
        var position = CameraPosition(camera);
        gazeSource = GazeTracking.Matches(original, position) ? 8 : -1;
        Log.Debug("Native gaze capture matched world position: {Matched}", gazeSource == 8);
        gazeVector = original;
    }

    private static Vector3 CameraPosition(Camera* camera) =>
        new(camera->SceneCamera.Position.X, camera->SceneCamera.Position.Y, camera->SceneCamera.Position.Z);

    private void Draw()
    {
        if (disposed || !open) return;
        ImGui.SetNextWindowSize(new Vector2(480, 380), ImGuiCond.FirstUseEver);
        if (ImGui.Begin("Glance###Glance", ref open, ImGuiWindowFlags.AlwaysAutoResize))
        {
            var iconPath = Path.Combine(Interface.AssemblyLocation.DirectoryName!, "images", "icon.png");
            var icon = Textures.GetFromFile(iconPath).GetWrapOrEmpty();
            ImGui.Image(icon.Handle, new Vector2(64, 64));
            ImGui.SameLine();
            ImGui.BeginGroup();
            ImGui.TextColored(new Vector4(0.92f, 0.78f, 0.49f, 1), "GLANCE");
            ImGui.TextUnformatted("A little eye contact.");
            ImGui.TextUnformatted("A little personality.");
            ImGui.EndGroup();
            ImGui.Spacing();
            ImGui.Separator();
            DrawAngleRecovery();
            var enabledBefore = config.Enabled;
            var personalityChanged = false;
            var changed = ImGui.Checkbox("Meet my gaze", ref config.Enabled);
            changed |= ImGui.SliderFloat("Close-up distance", ref config.Distance, 1f, 10f, "%.1f yalms");
            changed |= ImGui.SliderFloat("Gaze responsiveness", ref config.Response, 5f, 90f, "%.0f");
            changed |= ImGui.Checkbox("Keep gaze and expression when entering GPose", ref config.KeepPoseOnGpose);
            ImGui.TextWrapped("Hands off gaze and expression to GPose without sending reset commands. Glance resumes after leaving GPose.");
            ImGui.Spacing();
            changed |= ImGui.Checkbox("Extended close-up", ref config.ExtendedCloseUp);
            if (config.ExtendedCloseUp)
            {
                changed |= ImGui.SliderFloat("Closest camera distance", ref config.MinimumCameraDistance, 0.1f, 1.5f, "%.2f yalms");
                changed |= ImGui.Checkbox("Fine close-up zoom", ref config.FineCloseUpZoom);
                if (config.FineCloseUpZoom)
                {
                    changed |= ImGui.SliderFloat("Close-up zoom delta", ref config.CloseUpZoomDelta, 0.01f, 1f, "%.2f");
                    ImGui.TextWrapped("Smaller delta gives finer zoom steps. Blends into fine zoom over the 0.75 yalms before Close-up distance, in both scroll directions.");
                }
                changed |= ImGui.SliderFloat("Close-up transition time", ref config.CloseUpTransitionSeconds, 0.1f, 1.5f, "%.2f seconds");
                ImGui.TextWrapped("Scroll closer than the normal game limit. Smaller values allow a tighter close-up. Keeps your character visible in third person; scroll back out before switching to first person.");
                ImGui.TextWrapped("Pauses during combat, mounts, cutscenes, group pose and other game activity. Original camera limits return when disabled.");
                changed |= ImGui.Checkbox("Override close-up camera angle", ref config.OverrideCloseUpAngle);
                if (config.OverrideCloseUpAngle)
                {
                    changed |= ImGui.SliderFloat("Close-up camera angle", ref config.CloseUpCameraAngle, 0f, 100f, "%.0f");
                    changed |= ImGui.SliderFloat("Angle activation distance", ref config.CloseUpAngleDistance, 0.2f, 3f, "%.2f yalms");
                    ImGui.TextWrapped("Camera angle eases in over the 0.75 yalms before this distance. Zooming out eases back to the captured setting. Disable other plugins' angle controls if they conflict.");
                    ImGui.TextUnformatted(closeUp?.AngleActive == true ? "Camera angle override: active" : "Camera angle override: waiting");
                }
                ImGui.TextWrapped("The activation distance is independent of camera limits modified by other plugins. If framing changes unexpectedly, compare with other camera plugins disabled.");
                if (closeUpError != null) ImGui.TextWrapped(closeUpError);
            }
            if (ImGui.Button("Use current zoom") && float.IsFinite(distance) && distance >= 1f)
            { config.Distance = Math.Clamp(distance, 1f, 10f); changed = true; }
            ImGui.Spacing();
            ImGui.Separator();
            ImGui.TextColored(new Vector4(0.92f, 0.78f, 0.49f, 1), "PERSONALITY");
            var personality = (int)config.Personality;
            if (ImGui.Combo("Mood", ref personality, "Wink (classic)\0Playful\0Friendly\0Dramatic\0Custom\0Off - gaze only\0Expressive (joy / pose)\0Shy\0Confident\0Cheerful\0Mischievous\0Thoughtful\0Affectionate\0Melancholic\0Theatrical\0Celebratory\0Dance party\0Facial shuffle\0"))
            { config.Personality = (Personality)personality; changed = personalityChanged = true; }
            personalityChanged |= ImGui.Checkbox("Allow body animations", ref config.AllowBodyMotions);
            changed |= personalityChanged;
            if (config.Personality == Personality.Custom)
            {
                ImGui.InputText("Search emotes", ref emoteSearch, 120);
                ImGui.TextWrapped("Shows unlocked emotes from your installed game. Body animations may move, turn, or loop; facial cleanup does not stop their motion.");
                if (ImGui.Button("Clear custom selection"))
                { config.CustomEmoteIds.Clear(); config.CustomExpressions = ExpressionChoices.None; changed = personalityChanged = true; }
                if (ImGui.BeginChild("Emote catalog", new Vector2(450, 230), true))
                {
                    foreach (var definition in (Client.IsLoggedIn ? catalog.Available(config.AllowBodyMotions) : []).Where(e =>
                        e.Name.Contains(emoteSearch, StringComparison.CurrentCultureIgnoreCase)
                        || e.Command.Contains(emoteSearch, StringComparison.OrdinalIgnoreCase)))
                    {
                        var selected = config.CustomEmoteIds.Contains(definition.EmoteId)
                            || (config.CustomExpressions & definition.Choice) != 0;
                        if (ImGui.Checkbox($"{definition.Name} ({(definition.BodyMotion ? "body" : "face")})##{definition.EmoteId}", ref selected))
                        {
                            if (selected) config.CustomEmoteIds.Add(definition.EmoteId);
                            else
                            {
                                config.CustomEmoteIds.Remove(definition.EmoteId);
                                config.CustomExpressions &= ~definition.Choice;
                            }
                            changed = personalityChanged = true;
                        }
                    }
                }
                ImGui.EndChild();
            }
            if (personalityChanged) catalog.InvalidatePool();
            var pool = Client.IsLoggedIn ? catalog.Pool(config) : [];
            if (config.Personality != Personality.Custom && config.Personality != Personality.Off
                && pool.Count > 0 && ImGui.Button("Copy this mood to Custom"))
            {
                config.CustomEmoteIds = pool.Select(e => e.EmoteId).ToHashSet();
                config.CustomExpressions = ExpressionChoices.None;
                config.Personality = Personality.Custom;
                changed = personalityChanged = true;
            }
            if (pool.Any(e => e.BodyMotion))
                ImGui.TextWrapped("This mood includes body animations. They may move or turn the character, and some loop. Holds control facial cleanup only. Stop looping emotes manually by moving.");
            if (config.Personality != Personality.Off)
            {
                ImGui.TextWrapped(pool.Count == 0 ? "No eligible unlocked emotes. Choose Custom, allow body animations, or use another mood. Gaze stays enabled."
                    : $"{pool.Count} available: " + string.Join(", ", pool.Take(12).Select(e => e.Name)) + (pool.Count > 12 ? "…" : ""));
                changed |= ImGui.SliderInt("Every N zoom-ins / turns", ref config.ExpressionFrequency, 1, 10);
                changed |= ImGui.SliderFloat("Wait after camera settles", ref config.ExpressionDelay, 0f, 5f, "%.1f seconds");
                if (ImGui.CollapsingHeader("Expression durations"))
                {
                    foreach (var definition in pool)
                    {
                        var hold = definition.Kind == ExpressionKind.GameEmote
                            ? config.EmoteHolds.GetValueOrDefault(definition.EmoteId, definition.Hold)
                            : config.ExpressionHolds[definition.Kind];
                        var maxHold = definition.Kind == ExpressionKind.GameEmote && definition.BodyMotion ? 30f : 5f;
                        if (ImGui.SliderFloat(definition.Name + " hold##" + definition.EmoteId, ref hold, 0.1f, maxHold, "%.1f seconds"))
                        {
                            if (definition.Kind == ExpressionKind.GameEmote) config.EmoteHolds[definition.EmoteId] = hold;
                            else config.ExpressionHolds[definition.Kind] = hold;
                            changed = true;
                        }
                    }
                    ImGui.TextWrapped("Wink transitions through a smile; other expressions return directly to neutral. Game animation timing can vary.");
                }
            }
            if (changed)
            {
                if (enabledBefore != config.Enabled) Pause(config.Enabled ? "Enabled" : "Disabled");
                if (personalityChanged)
                {
                    CancelExpression(); winkPending = false;
                    winkCadence.Reset(); rotationCadence.Reset();
                    catalog.InvalidatePool();
                }
                config.Sanitize();
                settingsSave.Changed(Environment.TickCount64);
                if (!config.Enabled || !config.ExtendedCloseUp) closeUp?.Restore();
            }
            ImGui.Separator();
            ImGui.TextColored(active ? new Vector4(0.39f, 0.84f, 0.79f, 1)
                : new Vector4(0.73f, 0.76f, 0.82f, 1), status);
            if (ownsGaze && gazeSource >= 0)
                ImGui.TextUnformatted("Camera tracking: verified world position");
            ImGui.TextUnformatted($"Last camera distance: {distance:0.00} yalms");
            ImGui.TextWrapped("Stand still and zoom in for eye contact. Only your head and eyes follow the camera. Movement and game activity pause the gaze automatically.");
            if (expressions.CurrentName != null) ImGui.TextUnformatted("Expression: " + expressions.CurrentName);
            ImGui.TextWrapped("A continuous camera drag counts as one turn. Expressions return to a straight face when zooming out.");
        }
        ImGui.End();
    }

    public void Dispose()
    {
        if (disposed) return;
        disposed = true;
        Framework.Update -= Update;
        Interface.UiBuilder.Draw -= Draw;
        Interface.UiBuilder.OpenConfigUi -= Open;
        Interface.UiBuilder.OpenMainUi -= Open;
        Commands.RemoveHandler("/glance");
        try { closeUp?.Dispose(); }
        finally
        {
            CancelExpression();
            expressions.Discard();
            ReleaseGaze();
            if (settingsSave.Pending) SaveConfiguration();
        }
    }
}

