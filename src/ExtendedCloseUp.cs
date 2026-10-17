using Dalamud.Hooking;
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
    private readonly IGameInteropProvider interop;
    private Hook<Camera.Delegates.UpdateTiltOffset>? tilt;
    private float originalTilt;
    private nint cameraAddress;
    private nint playerAddress;
    public bool Active => limit.Active;

    public ExtendedCloseUp(IGameInteropProvider interop)
    {
        this.interop = interop;
        visibility = interop.HookFromAddress<CameraBase.Delegates.ShouldDrawGameObject>(
            CameraBase.MemberFunctionPointers.ShouldDrawGameObject, ShouldDraw);
        // Enable only while a normal third-person camera is eligible.
    }

    public void Update(bool eligible, nint player, float minimum)
    {
        var manager = CameraManager.Instance();
        var camera = manager == null ? null : manager->Camera;
        if (cameraAddress != (nint)camera)
        {
            RestoreAngle();
            tilt?.Dispose(); tilt = null;
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
        tilt ??= interop.HookFromAddress<Camera.Delegates.UpdateTiltOffset>(
            camera->VirtualTable->UpdateTiltOffset, UpdateTilt);
        camera->MinDistance = limit.Apply(camera->MinDistance, minimum);
        // Raising the slider must also move an already closer camera back out.
        ClampDistance(camera, camera->MinDistance);
        var wasClose = angle.Active;
        if (angle.Update(camera->Distance, limit.Original!.Value))
        {
            if (!wasClose) originalTilt = camera->TiltOffset;
            if (!tilt.IsEnabled) tilt.Enable();
            camera->TiltOffset = CloseUpAngle.Angle50;
        }
        else if (wasClose) RestoreAngle(restore: true);
        if (!visibility.IsEnabled) visibility.Enable();
    }

    private bool UpdateTilt(Camera* camera)
    {
        var result = tilt!.Original(camera);
        if (angle.Active && (nint)camera == cameraAddress
            && camera->ZoomMode == CameraZoomMode.ThirdPerson && !camera->IsEventCameraAutoControl)
            camera->TiltOffset = CloseUpAngle.Angle50;
        return result;
    }

    private void RestoreAngle(bool restore = false)
    {
        if (tilt?.IsEnabled == true) tilt.Disable();
        var manager = CameraManager.Instance();
        if ((angle.Active || restore) && manager != null && (nint)manager->Camera == cameraAddress)
        {
            // Re-read the game's saved setting, including any edits made while close.
            // UpdateTiltOffset reads Character Configuration; Glance never writes it.
            manager->Camera->TiltOffset = originalTilt;
            manager->Camera->UpdateTiltOffset();
        }
        angle.Reset();
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
        tilt?.Dispose();
    }
}
