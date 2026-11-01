using System;
using System.Collections;
using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Networking;
using UnityEngine.UI;

public sealed class HarmonyDemoController : MonoBehaviour
{
    [SerializeField] private InputField httpUrlField;
    [SerializeField] private InputField websocketUrlField;
    [SerializeField] private InputField keyboardField;
    [SerializeField] private Text outputText;
    private AudioClip soundClip;

#if UNITY_WEBGL && !UNITY_EDITOR
    [DllImport("__Internal")]
    private static extern void HarmonyDemoBrowserApis(string kind, string websocketUrl);

    [DllImport("__Internal")]
    private static extern void HarmonyHideKeyboard();

#endif

    public void Configure(InputField http, InputField websocket, InputField keyboard, Text output)
    {
        httpUrlField = http;
        websocketUrlField = websocket;
        keyboardField = keyboard;
        outputText = output;
    }

    private void Log(string value)
    {
        outputText.text += "\n" + value;
        Debug.Log("[HarmonyDemo] " + value);
    }

    public void OnDemoResult(string value) { Log(value); }

    public void OnNativeMessage(string value) { Log("Native→Unity: " + value); }

    public void OnMessageClicked()
    {
        string message = "Hello from Unity " + System.DateTime.UtcNow.ToString("HH:mm:ss");
        Log("Unity→Native: " + message);
#if UNITY_WEBGL && !UNITY_EDITOR
        HarmonyMessageBridge.UnityToNative(message);
#else
        OnNativeMessage("Native echo: " + message);
#endif
    }

    public void OnSoundClicked()
    {
        if (soundClip == null) soundClip = Resources.Load<AudioClip>("sound/sound1");
        AudioSource source = GetComponent<AudioSource>();
        if (soundClip == null || source == null)
        {
            Log("Sound: sound/sound1 or AudioSource is missing");
            return;
        }
        source.PlayOneShot(soundClip);
        Log("Sound: playing sound1.wav (" + soundClip.length.ToString("F2") + "s)");
    }

    private void RunBrowser(string kind)
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        HarmonyDemoBrowserApis(kind, websocketUrlField.text);
#else
        Log(kind + " 需要 WebGL 构建");
#endif
    }

    public void OnHttpClicked() { StartCoroutine(TestHttp()); }
    public void OnWebSocketClicked() { RunBrowser("websocket"); }
    public void OnWorkerClicked() { RunBrowser("worker"); }
    public void OnStorageClicked()
    {
        int previous = PlayerPrefs.GetInt("HarmonyDemoRuns", 0);
        PlayerPrefs.SetInt("HarmonyDemoRuns", previous + 1);
        PlayerPrefs.Save();
        Log("PlayerPrefs: previous=" + previous + ", current=" + (previous + 1));
        RunBrowser("storage");
    }

    public void OnImeClicked()
    {
        keyboardField.Select();
        keyboardField.ActivateInputField();
        Log("IME requested. Enter text, then confirm on the keyboard.");
    }

    public void OnHideImeClicked()
    {
        keyboardField.DeactivateInputField();
        if (EventSystem.current != null && EventSystem.current.currentSelectedGameObject == keyboardField.gameObject)
            EventSystem.current.SetSelectedGameObject(null);
#if UNITY_WEBGL && !UNITY_EDITOR
        HarmonyHideKeyboard();
#endif
    }

    private IEnumerator TestHttp()
    {
        string httpUrl = httpUrlField.text;
        using (var request = UnityWebRequest.Get(httpUrl))
        {
            yield return request.SendWebRequest();
            Log("HTTP GET: " + request.responseCode + " / " + request.result +
                " / " + (request.error ?? request.downloadHandler.text));
        }
        byte[] body = System.Text.Encoding.UTF8.GetBytes("unity-demo=鸿蒙");
        using (var request = new UnityWebRequest(httpUrl.Replace("/get", "/post"), "POST"))
        {
            request.uploadHandler = new UploadHandlerRaw(body);
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "text/plain; charset=utf-8");
            yield return request.SendWebRequest();
            Log("HTTP POST: " + request.responseCode + " / " + request.result +
                " / " + (request.error ?? request.downloadHandler.text));
        }
    }
}
