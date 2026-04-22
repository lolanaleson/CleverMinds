// Assets/Scripts/Core/DeviceIdentity.cs
using System;
using UnityEngine;

public static class DeviceIdentity
{
    private const string PREF_KEY = "CM_DEVICE_ID";

    public static string EnsureDeviceId()
    {
        string existing = PlayerPrefs.GetString(PREF_KEY, "");
        if (!string.IsNullOrEmpty(existing)) return existing;

        // 12 hex tipo "8f3c1a2b9d4e"
        string newId = Guid.NewGuid().ToString("N").Substring(0, 12);
        PlayerPrefs.SetString(PREF_KEY, newId);
        PlayerPrefs.Save();
        return newId;
    }
}
