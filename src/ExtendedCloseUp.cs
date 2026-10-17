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
    private readonly CloseUpAngle angle = new();
    private readonly CameraAngleSetting angleSetting = new();
    private readonly IGameConfig gameConfig;
    private float requestedTilt = CloseUpAngle.Angle50;
    private int mismatchedFrames;
    public bool PossibleAngleConflict => mismatchedFrames >= 15;
    public bool AngleActive => angle.Active;
    private nint cameraAddress;
    private nint playerAddress;
    public bool Active => limit.Active;

    public ExtendedCloseUp(IGameInteropProvider interop, IGameConfig gameConfig)
    {
        this.gameConfig = gameConfig;
        visibility = interop.HookFromAddress<CameraBase.Delegates.ShouldDrawGameObject>(
            CameraBase.MemberFunctionPointers.ShouldDrawGameObject, ShouldDraw);
        // Enable only while a normal third-person camera is eligible.
    }

    public void Update(bool eligible, nint player, float minimum, bool overrideAngle, float angleValue, float angleDistance)
    {
        var manager = CameraManager.Instance();
        var camera = manager == null ? null : manager->Camera;
        if (cameraAddress != (nint)camera)
        {
            RestoreAngle();
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
        camera->MinDistance = limit.Apply(camera->MinDistance, minimum);
        // Raising the slider must also move an already closer camera back out.
        ClampDistance(camera, camera->MinDistance);
        var wasClose = angle.Active;
        if (wasClose && gameConfig.TryGet(UiControlOption.TiltOffset, out float configured)
            && MathF.Abs(configured - requestedTilt) > 0.002f)
            mismatchedFrames = Math.Min(mismatchedFrames + 1, 120);
        else mismatchedFrames = Math.Max(0, mismatchedFrames - 1);
        requestedTilt = CloseUpAngle.ToNative(angleValue);
        if (overrideAngle && angle.Update(camera->Distance, angleDistance))
        {
            ApplyAngleSetting();
            camera->UpdateTiltOffset();
        }
        else if (wasClose) RestoreAngle(restore: true);
        if (!visibility.IsEnabled) visibility.Enable();
    }

    private void ApplyAngleSetting()
    {
        if (gameConfig.TryGet(UiControlOption.TiltOffset, out float value))
            angleSetting.Apply(value, requestedTilt, WriteAngleSetting);
    }

    private void WriteAngleSetting(float value) => gameConfig.Set(UiControlOption.TiltOffset, value);

    private void RestoreAngle(bool restore = false)
    {
        angleSetting.Restore(WriteAngleSetting);
        var manager = CameraManager.Instance();
        if ((angle.Active || restore) && manager != null && (nint)manager->Camera == cameraAddress)
        {
            // Recalculate framing from the restored Character Configuration value.
            manager->Camera->UpdateTiltOffset();
        }
        angle.Reset();
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
        RestoreAngle();
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

    public void Dispose()
    {
        Restore();
        visibility.Dispose();
    }
}
