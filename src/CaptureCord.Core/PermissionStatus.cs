// SPDX-License-Identifier: GPL-3.0-or-later
namespace CaptureCord.Core;

public enum PermissionStatus
{
    Unknown = -1,
    DeniedBySystem = 0,
    NotDeclaredByApp = 1,
    DeniedByUser = 2,
    UserPromptRequired = 3,
    Allowed = 4
}

public sealed record DevicePermission(string Name, PermissionStatus Status)
{
    public bool IsDenied => Status is PermissionStatus.DeniedBySystem or PermissionStatus.DeniedByUser;
    public string Description => Status switch
    {
        PermissionStatus.Allowed => "Allowed by Windows. Device availability is checked when capture starts.",
        PermissionStatus.DeniedBySystem => "Blocked by Windows or your administrator. Check the device and desktop-app access switches.",
        PermissionStatus.DeniedByUser => "Access is turned off. Enable access in Windows privacy settings.",
        PermissionStatus.UserPromptRequired => "Windows requires your permission. Choose Grant access to continue.",
        _ => "Windows cannot report a permission for this desktop app. Check privacy settings; actual access is verified when the device opens."
    };
}
