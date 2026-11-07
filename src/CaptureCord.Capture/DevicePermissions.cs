// SPDX-License-Identifier: GPL-3.0-or-later
using System.Diagnostics;
using System.Runtime.InteropServices;
using CaptureCord.Core;

namespace CaptureCord.Capture;

/// <summary>Documented AppCapability ABI; no registry access or permission bypass.
/// Unpackaged/older Windows installations may not expose this API for this process.</summary>
public static class DevicePermissions
{
    public static DevicePermission Check(bool camera)
    {
        try
        {
            using var apartment = new RuntimeApartment();
            using var capability = Create(camera);
            return new(camera ? "Camera / video capture" : "Microphone / HDMI audio input", ReadStatus(capability));
        }
        catch (Exception e) when (e is COMException or DllNotFoundException or EntryPointNotFoundException or UnauthorizedAccessException)
        {
            Log.Write("permissions.check.unavailable", e);
            return new(camera ? "Camera / video capture" : "Microphone / HDMI audio input", PermissionStatus.Unknown);
        }
    }

    // Invoke from the UI thread after an explicit user gesture. Polling the WinRT
    // operation yields to the dispatcher without holding unmanaged pointer locals across awaits.
    public static async Task RequestAsync(bool camera)
    {
        try
        {
            using var apartment = new RuntimeApartment();
            using var capability = Create(camera);
            if (ReadStatus(capability) == PermissionStatus.UserPromptRequired)
            {
                using var operation = BeginRequest(capability);
                using var info = AsyncInfo(operation);
                var timeout = Stopwatch.StartNew();
                while (OperationStatus(info) == 0)
                {
                    if (timeout.Elapsed > TimeSpan.FromMinutes(2))
                    {
                        Cancel(info);
                        throw new TimeoutException("The Windows permission request timed out. Open privacy settings and retry.");
                    }
                    await Task.Delay(100);
                }
                if (RequestResult(operation) == PermissionStatus.Allowed) return;
            }
        }
        catch (Exception e) when (e is COMException or DllNotFoundException or EntryPointNotFoundException or UnauthorizedAccessException)
        { Log.Write("permissions.request.fallback", e); }
        OpenSettings(camera);
    }

    public static void OpenSettings(bool camera) => Process.Start(new ProcessStartInfo(camera
        ? "ms-settings:privacy-webcam" : "ms-settings:privacy-microphone") { UseShellExecute = true });

    private static unsafe Com Create(bool camera)
    {
        const string className = "Windows.Security.Authorization.AppCapabilityAccess.AppCapability";
        var iid = new Guid("7c353e2a-46ee-44e5-af3d-6ad3fc49bd22");
        Native.Check(WindowsCreateString(className, (uint)className.Length, out var name));
        try
        {
            Native.Check(RoGetActivationFactory(name, ref iid, out var factoryPointer));
            using var factory = new Com(factoryPointer);
            string capabilityName = camera ? "webcam" : "microphone";
            Native.Check(WindowsCreateString(capabilityName, (uint)capabilityName.Length, out var key));
            try
            {
                nint result;
                Native.Check(((delegate* unmanaged[Stdcall]<nint, nint, nint*, int>)factory.Slot(8))(factory.Pointer, key, &result));
                return new(result);
            }
            finally { WindowsDeleteString(key); }
        }
        finally { WindowsDeleteString(name); }
    }
    private static unsafe PermissionStatus ReadStatus(Com capability)
    {
        int status;
        Native.Check(((delegate* unmanaged[Stdcall]<nint, int*, int>)capability.Slot(9))(capability.Pointer, &status));
        return Enum.IsDefined((PermissionStatus)status) ? (PermissionStatus)status : PermissionStatus.Unknown;
    }
    private static unsafe Com BeginRequest(Com capability)
    {
        nint result;
        Native.Check(((delegate* unmanaged[Stdcall]<nint, nint*, int>)capability.Slot(8))(capability.Pointer, &result));
        return new(result);
    }
    private static unsafe Com AsyncInfo(Com operation)
    {
        var iid = new Guid("00000036-0000-0000-c000-000000000046");
        nint result;
        Native.Check(((delegate* unmanaged[Stdcall]<nint, Guid*, nint*, int>)operation.Slot(0))(operation.Pointer, &iid, &result));
        return new(result);
    }
    private static unsafe int OperationStatus(Com info)
    {
        int status;
        Native.Check(((delegate* unmanaged[Stdcall]<nint, int*, int>)info.Slot(7))(info.Pointer, &status));
        return status;
    }
    private static unsafe void Cancel(Com info) => Native.Check(((delegate* unmanaged[Stdcall]<nint, int>)info.Slot(9))(info.Pointer));
    private static unsafe PermissionStatus RequestResult(Com operation)
    {
        int status;
        Native.Check(((delegate* unmanaged[Stdcall]<nint, int*, int>)operation.Slot(8))(operation.Pointer, &status));
        return (PermissionStatus)status;
    }
    private sealed class RuntimeApartment : IDisposable
    {
        private readonly bool initialized;
        public RuntimeApartment()
        {
            var hr = RoInitialize(Thread.CurrentThread.GetApartmentState() == ApartmentState.STA ? 0u : 1u);
            Native.Check(hr);
            initialized = true;
        }
        public void Dispose() { if (initialized) RoUninitialize(); }
    }
    [DllImport("combase.dll", ExactSpelling = true)] private static extern int RoInitialize(uint type);
    [DllImport("combase.dll", ExactSpelling = true)] private static extern void RoUninitialize();
    [DllImport("combase.dll", ExactSpelling = true)] private static extern int RoGetActivationFactory(nint name, ref Guid iid, out nint factory);
    [DllImport("combase.dll", ExactSpelling = true, CharSet = CharSet.Unicode)] private static extern int WindowsCreateString(string text, uint length, out nint value);
    [DllImport("combase.dll", ExactSpelling = true)] private static extern int WindowsDeleteString(nint value);
}
