using Dalamud.Hooking;
using Dalamud.Game.Config;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.Game.Control;
using FFXIVClientStructs.FFXIV.Client.Game.Object;
using NativeVector = FFXIVClientStructs.FFXIV.Common.Math.Vector3;

namespace Glance;

internal sealed unsafe class ExtendedCloseUp : IDisposable
{
    private readonly Hook<CameraBase.Delegates.ShouldDrawGameObject> visibility;
    private readonly CloseUpLimit limit = new();
    private readonly CameraAngleSetting angleSetting = new();
    private readonly IGameConfig gameConfig;
    private readonly IGameInteropProvider interop;
    private readonly Func<float, bool> beginAngleOverride;
    private readonly Func<bool> canRestoreAngle;
    private readonly Action<bool> endAngleOverride;
    private Hook<Camera.Delegates.GetZoomSpeed>? zoomSpeed;
    private bool fineZoom;
    private float zoomThreshold;
    private float zoomDelta;
    private float angleBlend;
    private float requestedTilt = CloseUpAngle.Angle50;
    private int mismatchedFrames;
    public bool PossibleAngleConflict => mismatchedFrames >= 15;
    public bool AngleActive => angleSetting.Original.HasValue;
    private nint cameraAddress;
    private nint playerAddress;
    public bool Active => limit.Active;

    public ExtendedCloseUp(IGameInteropProvider interop, IGameConfig gameConfig,
        Func<float, bool> beginAngleOverride, Func<bool> canRestoreAngle, Action<bool> endAngleOverride)
    {
        this.gameConfig = gameConfig;
        this.interop = interop;
        this.beginAngleOverride = beginAngleOverride;
        this.canRestoreAngle = canRestoreAngle;
        this.endAngleOverride = endAngleOverride;
        visibility = interop.HookFromAddress<CameraBase.Delegates.ShouldDrawGameObject>(
            CameraBase.MemberFunctionPointers.ShouldDrawGameObject, ShouldDraw);
        // Enable only while a normal third-person camera is eligible.
    }

    public void Update(bool eligible, nint player, float minimum, bool overrideAngle, float angleValue, float angleDistance,
        bool fineZoomEnabled, float closeUpDistance, float delta, float dt, float transitionSeconds)
    {
        var manager = CameraManager.Instance();
        var camera = manager == null ? null : manager->Camera;
        if (AngleActive && (!canRestoreAngle() || (playerAddress != 0 && playerAddress != player))) RestoreAngle();
        if (cameraAddress != (nint)camera)
        {
            RestoreAngle();
            zoomSpeed?.Dispose(); zoomSpeed = null;
            // A replaced native object is no longer safe to dereference.
            limit.Forget(); cameraAddress = 0; playerAddress = 0;
        }
        if (!eligible || camera == null || manager->ActiveCameraIndex != 0
            || camera->ZoomMode != CameraZoomMode.ThirdPerson || camera->IsEventCameraAutoControl
            || camera->ControlMode == CameraControlMode.LockonThirdPerson
            || !float.IsFinite(camera->MinDistance) || camera->MinDistance <= 0)
        { Restore(); return; }

        cameraAddress = (nint)camera;
        playerAddress = player;
        fineZoom = fineZoomEnabled;
        zoomThreshold = closeUpDistance;
        zoomDelta = CloseUpZoom.Sanitize(delta);
        if (fineZoom)
        {
            zoomSpeed ??= interop.HookFromAddress<Camera.Delegates.GetZoomSpeed>(camera->VirtualTable->GetZoomSpeed, GetZoomSpeed);
            if (!zoomSpeed.IsEnabled) zoomSpeed.Enable();
        }
        else if (zoomSpeed?.IsEnabled == true) zoomSpeed.Disable();
        camera->MinDistance = limit.Apply(camera->MinDistance, minimum);
        // Raising the slider must also move an already closer camera back out.
        ClampDistance(camera, camera->MinDistance);
        if (!visibility.IsEnabled) visibility.Enable();
        var wasClose = AngleActive;
        if (wasClose && gameConfig.TryGet(UiControlOption.TiltOffset, out float configured)
            && MathF.Abs(configured - requestedTilt) > 0.002f)
            mismatchedFrames = Math.Min(mismatchedFrames + 1, 120);
        else mismatchedFrames = Math.Max(0, mismatchedFrames - 1);
        var targetBlend = overrideAngle ? CloseUpTransition.Weight(camera->Distance, angleDistance) : 0f;
        angleBlend = CloseUpTransition.Smooth(angleBlend, targetBlend, dt, transitionSeconds);
        if (overrideAngle && (targetBlend > 0 || angleBlend > 0))
        {
            if (gameConfig.TryGet(UiControlOption.TiltOffset, out float current))
            {
                // Commit the backup before the first persistent game-setting write.
                if (!AngleActive && !beginAngleOverride(current)) return;
                angleSetting.Apply(current, current, WriteAngleSetting);
                var baseline = angleSetting.Original ?? current;
                requestedTilt = baseline + (CloseUpAngle.ToNative(angleValue) - baseline) * angleBlend;
            }
            ApplyAngleSetting();
            camera->UpdateTiltOffset();
        }
        else if (wasClose) RestoreAngle(restore: true);
    }

    private float GetZoomSpeed(Camera* camera)
    {
        var original = zoomSpeed!.Original(camera);
        var manager = CameraManager.Instance();
        var eligible = Active && fineZoom && (nint)camera == cameraAddress
            && manager != null && manager->ActiveCameraIndex == 0 && (nint)manager->Camera == cameraAddress
            && camera->ZoomMode == CameraZoomMode.ThirdPerson && !camera->IsEventCameraAutoControl
            && camera->ControlMode != CameraControlMode.LockonThirdPerson;
        return CloseUpZoom.Apply(original, eligible, camera->Distance, zoomThreshold, zoomDelta);
    }

    private void ApplyAngleSetting()
    {
        if (AngleActive && gameConfig.TryGet(UiControlOption.TiltOffset, out float value))
            angleSetting.Apply(value, requestedTilt, WriteAngleSetting);
    }

    private void WriteAngleSetting(float value) => gameConfig.Set(UiControlOption.TiltOffset, value);

    private void RestoreAngle(bool restore = false)
    {
        var hadAngle = AngleActive;
        if (hadAngle)
        {
            var restoreHere = canRestoreAngle();
            if (restoreHere) angleSetting.Restore(WriteAngleSetting);
            else angleSetting.Forget();
            endAngleOverride(restoreHere);
        }
        var manager = CameraManager.Instance();
        if ((hadAngle || restore) && manager != null && (nint)manager->Camera == cameraAddress)
        {
            // Recalculate framing from the restored Character Configuration value.
            manager->Camera->UpdateTiltOffset();
        }
        angleBlend = 0;
        mismatchedFrames = 0;
    }

    private bool ShouldDraw(CameraBase* camera, GameObject* actor, NativeVector* position, NativeVector* lookAt)
    {
        var manager = CameraManager.Instance();
        if (Active && (nint)camera == cameraAddress && (nint)actor == playerAddress
            && manager != null && manager->ActiveCameraIndex == 0 && (nint)manager->Camera == cameraAddress
            && manager->Camera->ZoomMode == CameraZoomMode.ThirdPerson
            && !manager->Camera->IsEventCameraAutoControl
            && manager->Camera->Distance <= 3f)
            return true;
        return visibility.Original(camera, actor, position, lookAt);
    }

    private static void ClampDistance(Camera* camera, float minimum)
    {
        if (float.IsFinite(camera->Distance) && camera->Distance < minimum) camera->Distance = minimum;
        if (float.IsFinite(camera->InterpDistance) && camera->InterpDistance < minimum) camera->InterpDistance = minimum;
    }

    public void Restore()
    {
        fineZoom = false;
        if (zoomSpeed?.IsEnabled == true) zoomSpeed.Disable();
        try { RestoreAngle(); }
        finally
        {
            if (visibility.IsEnabled) visibility.Disable();
            var manager = CameraManager.Instance();
            if (limit.Active && manager != null && (nint)manager->Camera == cameraAddress)
            {
                var camera = manager->Camera;
                camera->MinDistance = limit.Restore(camera->MinDistance);
                if (camera->ZoomMode == CameraZoomMode.ThirdPerson) ClampDistance(camera, camera->MinDistance);
            }
            limit.Forget(); cameraAddress = 0; playerAddress = 0;
        }
    }

    public void Dispose()
    {
        try { Restore(); }
        finally
        {
            try { visibility.Dispose(); }
            finally { zoomSpeed?.Dispose(); }
        }
    }
}

