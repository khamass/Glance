using System.Numerics;
using System.IO;
using System.Text.Json;
using Dalamud.Bindings.ImGui;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Game.Command;
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
    [PluginService] internal static IClientState Client { get; private set; } = null!;
    [PluginService] internal static ICondition Condition { get; private set; } = null!;
    [PluginService] internal static ICommandManager Commands { get; private set; } = null!;
    [PluginService] internal static IPluginLog Log { get; private set; } = null!;
    [PluginService] internal static ITextureProvider Textures { get; private set; } = null!;

    private readonly Configuration config;
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
    private float smileRemaining;
    private nint winkPlayer;
    private int restoreGazeFrames;
    private float straightfaceRemaining;
    private nint smilePlayer;
    private float smileRetryRemaining;
    private bool winkPending;
    private float zoomStillSeconds;
    private float previousZoom = float.NaN;
    private readonly RotationCadence rotationCadence = new();

    public Plugin()
    {
        config = LoadConfiguration();
        config.Sanitize();
        Interface.SavePluginConfig(config);
        Commands.AddHandler("/glance", new CommandInfo(OnCommand) { HelpMessage = "Open Glance. /glance on | off | toggle" });
        Interface.UiBuilder.Draw += Draw;
        Interface.UiBuilder.OpenConfigUi += Open;
        Interface.UiBuilder.OpenMainUi += Open;
        Framework.Update += Update;
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
        Interface.SavePluginConfig(config);
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
        FinishWink();
        FinishSmile();
        winkPending = false;
        rotationCadence.ResetMotion();
        restoreGazeFrames = 0;
        ReleaseGaze(); attemptedGaze = false; gazeSource = -1;
        captureFrames = 0; active = false; status = reason;
    }

    private void FinishWink(bool resumeGaze = false)
    {
        if (winkPlayer == 0) return;
        var player = Objects.LocalPlayer;
        if (Client.IsLoggedIn && player != null && player.Address == winkPlayer
            && ExecuteGameCommand("/smile motion"))
        {
            smilePlayer = player.Address;
            straightfaceRemaining = 2f;
            // Dispatch does not confirm acceptance. A facial-emote lock may
            // reject the 0.3s attempt; retry once at the former 0.65s timing.
            smileRetryRemaining = resumeGaze ? 0.35f : 0;
            if (resumeGaze && ownsGaze) restoreGazeFrames = 2;
        }
        winkPlayer = 0;
        smileRemaining = 0;
    }

    private void FinishSmile(bool resumeGaze = false)
    {
        if (smilePlayer == 0) return;
        var player = Objects.LocalPlayer;
        if (Client.IsLoggedIn && player != null && player.Address == smilePlayer
            && ExecuteGameCommand("/straightface motion") && resumeGaze && ownsGaze)
            restoreGazeFrames = 2;
        smilePlayer = 0;
        straightfaceRemaining = 0;
        smileRetryRemaining = 0;
    }

    private void Update(IFramework framework)
    {
        try { Tick((float)framework.UpdateDelta.TotalSeconds); }
        catch (Exception ex)
        {
            config.Enabled = false;
            Pause("Disabled after an error; see /xllog");
            Log.Error(ex, "Glance update failed; disabled until manually enabled.");
        }
    }

    private void Tick(float dt)
    {
        var player = Objects.LocalPlayer;
        if (player == null || !Client.IsLoggedIn)
        {
            winkCadence.LeaveZoom();
            Pause("Waiting for player"); lastPlayer = 0; idleSeconds = 0; return;
        }
        var position = player.Position;
        if (lastPlayer != player.Address)
        {
            FinishWink(); FinishSmile(); winkCadence.LeaveZoom();
            ReleaseGaze(); lastPlayer = player.Address; lastPosition = position; idleSeconds = 0;
            Pause("Waiting for idle"); return;
        }
        if (smilePlayer != 0)
        {
            if (smileRetryRemaining > 0)
            {
                smileRetryRemaining -= Math.Max(dt, 0f);
                if (smileRetryRemaining <= 0 && player.Address == smilePlayer
                    && ExecuteGameCommand("/smile motion"))
                {
                    straightfaceRemaining = 2f;
                    if (ownsGaze) restoreGazeFrames = 2;
                }
            }
            straightfaceRemaining -= Math.Max(dt, 0f);
            if (straightfaceRemaining <= 0) FinishSmile(resumeGaze: true);
        }
        if (winkPlayer != 0)
        {
            smileRemaining -= Math.Max(dt, 0f);
            if (smileRemaining <= 0) FinishWink(resumeGaze: true);
        }
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
        if (Client.IsGPosing) { Pause("Paused in group pose"); return; }
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
        if (rotationCadence.Update(camera->DirH, camera->DirV, dt))
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
            var captured = native->LookAt.CameraVector;
            var original = new Vector3(captured.X, captured.Y, captured.Z);
            // Infer representation from the value the game actually captured.
            // Do not assume that the portrait's world-position format applies here.
            if (captureFrames > 0)
            {
                captureFrames--;
                CaptureGazeSource(native, camera);
            }
            else if (gazeSource >= 0)
            {
                var target = CameraVector(camera, gazeSource);
                if (float.IsFinite(target.X) && float.IsFinite(target.Y) && float.IsFinite(target.Z))
                {
                    gazeVector = Vector3.Lerp(gazeVector, target,
                        1f - MathF.Exp(-config.Response * Math.Clamp(dt, 0f, 0.1f)));
                    native->LookAt.CameraVector = new() { X = gazeVector.X, Y = gazeVector.Y, Z = gazeVector.Z };
                }
            }
        }
        active = true;
        if (captureFrames == 0 && native->LookAt.IsFacingCamera && winkCadence.EnterZoom())
            winkPending = true;
        if (winkPending && zoomStillSeconds >= 2f && rotationCadence.StillSeconds >= 2f
            && winkPlayer == 0 && smilePlayer == 0 && native->LookAt.IsFacingCamera
            && ExecuteGameCommand("/wink motion"))
        {
            winkPending = false;
            winkPlayer = player.Address;
            smileRemaining = 0.3f;
            if (ownsGaze) restoreGazeFrames = 2;
        }
        status = ownsGaze && captureFrames == 0 && gazeSource < 0 ? "Gaze held; camera vector format not recognized"
            : native->LookAt.IsFacingCamera ? "Native /facecamera gaze active"
            : "Gaze cancelled or unavailable; zoom out and back in to retry";
    }

    private void CaptureGazeSource(NativeCharacter* native, Camera* camera)
    {
        var captured = native->LookAt.CameraVector;
        var original = new Vector3(captured.X, captured.Y, captured.Z);
        var bestError = 0.0001f;
        for (var i = 0; i < 10; i++)
        {
            var error = Vector3.DistanceSquared(original, CameraVector(camera, i));
            if (error < bestError) { bestError = error; gazeSource = i; }
        }
        gazeVector = original;
    }

    private static Vector3 CameraVector(Camera* camera, int source)
    {
        var scene = &camera->SceneCamera;
        var direction = new Vector3(scene->LookAtVector.X - scene->Position.X,
            scene->LookAtVector.Y - scene->Position.Y, scene->LookAtVector.Z - scene->Position.Z);
        if (direction.LengthSquared() > 0.0001f) direction = Vector3.Normalize(direction);
        return source switch
        {
            0 => new(scene->Rotation.X, scene->Rotation.Y, scene->Rotation.Z),
            1 => direction,
            2 => -direction,
            3 => new(scene->Vector_1.X, scene->Vector_1.Y, scene->Vector_1.Z),
            4 => new(scene->LookAtVector.X, scene->LookAtVector.Y, scene->LookAtVector.Z),
            5 => new(camera->DirV, camera->DirH, 0),
            6 => new(camera->DirH, camera->DirV, 0),
            7 => new(0, camera->DirH, camera->DirV),
            8 => new(scene->Position.X, scene->Position.Y, scene->Position.Z),
            _ => new(-scene->Rotation.X, -scene->Rotation.Y, -scene->Rotation.Z),
        };
    }

    private void Draw()
    {
        if (!open) return;
        ImGui.SetNextWindowSize(new Vector2(420, 280), ImGuiCond.FirstUseEver);
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
            var changed = ImGui.Checkbox("Meet my gaze", ref config.Enabled);
            changed |= ImGui.SliderFloat("Close-up distance", ref config.Distance, 1f, 10f, "%.1f yalms");
            changed |= ImGui.SliderFloat("Gaze responsiveness", ref config.Response, 5f, 90f, "%.0f");
            if (ImGui.Button("Use current zoom") && float.IsFinite(distance) && distance >= 1f)
            { config.Distance = Math.Clamp(distance, 1f, 10f); changed = true; }
            if (changed) { Pause("Settings updated"); config.Sanitize(); Interface.SavePluginConfig(config); }
            ImGui.Separator();
            ImGui.TextColored(active ? new Vector4(0.39f, 0.84f, 0.79f, 1)
                : new Vector4(0.73f, 0.76f, 0.82f, 1), status);
            if (ownsGaze && gazeSource >= 0)
                ImGui.TextUnformatted($"Camera tracking source: {gazeSource}");
            ImGui.TextUnformatted($"Last camera distance: {distance:0.00} yalms");
            ImGui.TextWrapped("Stand still and zoom in for eye contact. Only your head and eyes follow the camera. Movement and game activity pause the gaze automatically.");
            ImGui.TextWrapped("Every fourth zoom-in or camera turn: wait 2 seconds after the camera settles, briefly wink, smile, then return to a straight face. A continuous drag counts as one turn.");
        }
        ImGui.End();
    }

    public void Dispose()
    {
        FinishWink();
        FinishSmile();
        ReleaseGaze();
        Framework.Update -= Update;
        Interface.UiBuilder.Draw -= Draw;
        Interface.UiBuilder.OpenConfigUi -= Open;
        Interface.UiBuilder.OpenMainUi -= Open;
        Commands.RemoveHandler("/glance");
    }
}

