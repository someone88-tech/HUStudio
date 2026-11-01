using System.Runtime.InteropServices;

public static class HarmonyMessageBridge
{
#if UNITY_WEBGL && !UNITY_EDITOR
    [DllImport("__Internal")]
    private static extern void HarmonyUnityToNative(string message);
#endif

    public static void UnityToNative(string message)
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        HarmonyUnityToNative(message ?? string.Empty);
#endif
    }
}
