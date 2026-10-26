using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using System.Runtime.InteropServices;
using System.Collections;

/// <summary>
/// 挂载到带有 InputField 的 GameObject 上，
/// 自动在 InputField 获得焦点时通知 HarmonyOS NEXT SDK 弹出平台软键盘。
/// SDK 会根据输入框位置自动判断是否需要上推画布，避免键盘遮挡。
///
/// 使用方法：
///   1. 将整个 HarmonyKeyboard 文件夹复制到 Unity 工程的 Assets/ 目录下
///   2. 在 InputField 的 GameObject 上 Add Component → HarmonyKeyboardBridge
///   3. 重新 Build WebGL
/// </summary>
public class HarmonyKeyboardBridge : MonoBehaviour, ISelectHandler, IDeselectHandler, IPointerClickHandler
{
    private InputField inputField;
    private RectTransform inputRect;
    private bool keyboardRequested;

    private void Awake()
    {
        inputField = GetComponent<InputField>();
        inputRect = GetComponent<RectTransform>();
    }

    private void Update()
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        if (!keyboardRequested || inputRect == null) return;
        bool pointerDown = Input.GetMouseButtonDown(0) ||
            (Input.touchCount > 0 && Input.GetTouch(0).phase == TouchPhase.Began);
        if (!pointerDown) return;
        Vector2 pointer = Input.touchCount > 0 ? Input.GetTouch(0).position : (Vector2)Input.mousePosition;
        if (!RectTransformUtility.RectangleContainsScreenPoint(inputRect, pointer, GetCanvasCamera()))
            StartCoroutine(ClearFocusIfOutside());
#endif
    }

    private IEnumerator ClearFocusIfOutside()
    {
        // Let the EventSystem select another input field before clearing this one.
        yield return null;
        if (!keyboardRequested || inputField == null || !inputField.isFocused) yield break;
        EventSystem eventSystem = EventSystem.current;
        if (eventSystem != null && eventSystem.currentSelectedGameObject != gameObject) yield break;
        inputField.DeactivateInputField();
        if (eventSystem != null) eventSystem.SetSelectedGameObject(null);
        RequestHideKeyboard();
    }

    private void OnDisable()
    {
        RequestHideKeyboard();
    }

    private void RequestHideKeyboard()
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        if (!keyboardRequested) return;
        keyboardRequested = false;
        HarmonyHideKeyboard();
#endif
    }
#if UNITY_WEBGL && !UNITY_EDITOR
    [DllImport("__Internal")]
    private static extern void HarmonyShowKeyboard(int screenY, int screenHeight, int screenTotalHeight);

    [DllImport("__Internal")]
    private static extern void HarmonyHideKeyboard();
#endif

    public void OnSelect(BaseEventData eventData)
    {
        RequestShowKeyboard();
    }

    /// <summary>
    /// 点击已选中的 InputField 时也重新请求键盘。
    /// 处理用户通过软键盘下拉按钮收起键盘后，再次点击同一个 InputField 的情况。
    /// </summary>
    public void OnPointerClick(PointerEventData eventData)
    {
        RequestShowKeyboard();
#if UNITY_WEBGL && !UNITY_EDITOR
        if (eventData.button == PointerEventData.InputButton.Left && inputField != null)
        {
            int caret = GetClickedCaretPosition(eventData.position, eventData.pressEventCamera);
            StartCoroutine(SetCaretAfterActivation(caret));
        }
#endif
    }

#if UNITY_WEBGL && !UNITY_EDITOR
    private IEnumerator SetCaretAfterActivation(int caret)
    {
        // UGUI activates a newly selected InputField in LateUpdate. Its first
        // OnPointerDown deliberately leaves the caret at the default position.
        yield return null;
        if (inputField != null && inputField.isFocused &&
            EventSystem.current != null && EventSystem.current.currentSelectedGameObject == gameObject)
        {
            inputField.caretPosition = Mathf.Clamp(caret, 0, inputField.text.Length);
            TouchScreenKeyboard keyboard = inputField.touchScreenKeyboard;
            if (keyboard != null && keyboard.canSetSelection)
                keyboard.selection = new RangeInt(inputField.caretPosition, 0);
            inputField.ForceLabelUpdate();
        }
    }

    private int GetClickedCaretPosition(Vector2 screenPosition, Camera eventCamera)
    {
        Text label = inputField.textComponent;
        if (label == null || string.IsNullOrEmpty(inputField.text)) return 0;

        Vector2 localPosition;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            label.rectTransform, screenPosition, eventCamera, out localPosition);
        TextGenerator generator = label.cachedTextGenerator;
        if (generator.lineCount == 0) return 0;

        int line = 0;
        if (inputField.lineType != InputField.LineType.SingleLine)
        {
            float y = localPosition.y * label.pixelsPerUnit;
            line = generator.lineCount;
            for (int i = 0; i < generator.lineCount; i++)
            {
                if (y > generator.lines[i].topY - generator.lines[i].height)
                {
                    line = i;
                    break;
                }
            }
        }

        int visibleIndex = generator.characterCountVisible;
        if (line < generator.lineCount)
        {
            int start = generator.lines[line].startCharIdx;
            int end = line + 1 < generator.lineCount
                ? generator.lines[line + 1].startCharIdx - 1
                : generator.characterCountVisible;
            visibleIndex = end;
            for (int i = start; i < end && i < generator.characterCountVisible; i++)
            {
                UICharInfo info = generator.characters[i];
                float charStart = info.cursorPos.x / label.pixelsPerUnit;
                float charEnd = charStart + info.charWidth / label.pixelsPerUnit;
                if (localPosition.x < (charStart + charEnd) * 0.5f)
                {
                    visibleIndex = i;
                    break;
                }
            }
        }

        // UGUI renders only a substring when a single-line field scrolls.
        string visibleText = label.text;
        int drawStart = string.IsNullOrEmpty(visibleText)
            ? 0 : inputField.text.LastIndexOf(visibleText, System.StringComparison.Ordinal);
        if (drawStart < 0) drawStart = 0;
        return Mathf.Clamp(drawStart + visibleIndex, 0, inputField.text.Length);
    }
#endif

    public void OnDeselect(BaseEventData eventData)
    {
        RequestHideKeyboard();
    }

    private void RequestShowKeyboard()
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        keyboardRequested = true;
        RectTransform rt = GetComponent<RectTransform>();
        if (rt != null)
        {
            Vector3[] corners = new Vector3[4];
            rt.GetWorldCorners(corners);
            Camera cam = GetCanvasCamera();
            Vector3 screenPos = cam != null
                ? cam.WorldToScreenPoint(corners[0])
                : corners[0];
            int bottomY = (int)(Screen.height - screenPos.y);
            int height = (int)(cam != null
                ? (cam.WorldToScreenPoint(corners[2]) - screenPos).y
                : (corners[2] - corners[0]).y);
            HarmonyShowKeyboard(bottomY, height, Screen.height);
        }
        else
        {
            HarmonyShowKeyboard(Screen.height / 2, 40, Screen.height);
        }
#endif
    }

    private Camera GetCanvasCamera()
    {
        Canvas canvas = GetComponentInParent<Canvas>();
        if (canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay)
            return canvas.worldCamera;
        return null;
    }
}
