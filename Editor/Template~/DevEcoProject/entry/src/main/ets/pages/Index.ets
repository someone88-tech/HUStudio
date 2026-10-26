import sdk from 'libufhn.so';
import { NetworkBridge } from '../NetworkBridge';
import { fileIo } from '@kit.CoreFileKit';
import { unityFiles } from '../UnityFiles';
import { window } from '@kit.ArkUI';

@Entry
@Component
struct Index {
  @State imeVisible: boolean = false;
  @State canvasOffsetVp: number = 0;
  // Keep a hidden character so Backspace still produces onChange for prefilled Unity fields.
  @State imeValue: string = '\u200B';
  private imeController: TextInputController = new TextInputController();
  private networkBridge: NetworkBridge | null = null;
  private activeWindow: window.Window | null = null;
  private canvasHeightPx: number = 0;
  private keyboardHeightPx: number = 0;
  private inputBottomY: number = -1;
  private inputHeight: number = -1;
  private unityScreenHeight: number = -1;
  private keyboardHeightChanged = (height: number): void => {
    this.keyboardHeightPx = height;
    this.updateCanvasOffset();
  };

  private updateCanvasOffset(): void {
    if (!this.imeVisible || this.keyboardHeightPx <= 0 || this.inputBottomY < 0 ||
        this.canvasHeightPx <= 0) {
      this.canvasOffsetVp = 0;
      return;
    }
    const scale = this.unityScreenHeight > 0 ? this.canvasHeightPx / this.unityScreenHeight : 1;
    const bottom = this.inputBottomY * scale;
    const padding = this.inputHeight > 0 ? this.inputHeight * scale * 0.5 : vp2px(12);
    const keyboardTop = this.canvasHeightPx - this.keyboardHeightPx;
    this.canvasOffsetVp = -px2vp(Math.max(0, bottom + padding - keyboardTop));
  }

  private ensureDirectory(path: string): void {
    if (!fileIo.accessSync(path)) fileIo.mkdirSync(path, true);
  }

  aboutToAppear(): void {
    try {
      window.getLastWindow(getContext(this)).then((appWindow: window.Window) => {
        this.activeWindow = appWindow;
        appWindow.on('keyboardHeightChange', this.keyboardHeightChanged);
      }).catch((error: Error) => console.error(`UnityDemo window listener: ${error}`));
      this.networkBridge = new NetworkBridge((enabled: boolean, bottomY: number,
        height: number, screenHeight: number) => {
        console.info(`UnityDemo IME enabled=${enabled}`);
        this.imeVisible = enabled;
        if (enabled) {
          this.imeValue = '\u200B';
          if (bottomY >= 0) {
            this.inputBottomY = bottomY;
            this.inputHeight = height;
            this.unityScreenHeight = screenHeight;
          }
          setTimeout(() => {
            console.info(`UnityDemo IME focus=${focusControl.requestFocus('UnityDemoIme')}`);
            this.imeController.caretPosition(this.imeValue.length);
          }, 80);
        } else {
          this.inputBottomY = -1;
          this.canvasOffsetVp = 0;
        }
        this.updateCanvasOffset();
      }, (message: string): void => {
        console.info(`HUStudio Unity message: ${message}`);
      });
      const resources = getContext(this).resourceManager;
      let common: Uint8Array = new Uint8Array(0);
      if (sdk.requiresExternalCommon()) {
        common = resources.getRawFileContentSync('common.npk');
      }
      const projectDir: string = getContext(this).filesDir + '/unity_webgl';
      this.ensureDirectory(projectDir);
      for (const name of unityFiles) {
        const destination: string = projectDir + '/' + name;
        const slash: number = destination.lastIndexOf('/');
        this.ensureDirectory(destination.substring(0, slash));
        const content: Uint8Array = resources.getRawFileContentSync('unity_webgl/' + name);
        const stream = fileIo.createStreamSync(destination, 'w+');
        stream.writeSync(content.buffer);
        stream.close();
      }
      if (!sdk.configure(common.buffer, new ArrayBuffer(0),
        getContext(this).filesDir, projectDir)) {
        console.error('UnityDemo NPK 配置失败');
      }
    } catch (error) {
      console.error(`UnityDemo 加载失败: ${error}`);
    }
  }

  aboutToDisappear(): void {
    if (this.activeWindow) {
      this.activeWindow.off('keyboardHeightChange', this.keyboardHeightChanged);
      this.activeWindow = null;
    }
  }

  private onImeChange(value: string): void {
    const oldValue = this.imeValue;
    if (oldValue === '\u200B' && value.length === 0) {
      sdk.inputKey(8);
      this.imeValue = '\u200B';
      return;
    }
    let prefix = 0;
    while (prefix < oldValue.length && prefix < value.length &&
           oldValue.charAt(prefix) === value.charAt(prefix)) prefix++;
    for (let i = prefix; i < oldValue.length; i++) sdk.inputKey(8);
    if (prefix < value.length) sdk.inputText(value.substring(prefix));
    this.imeValue = value;
  }

  build() {
    Stack() {
      XComponent({ id: 'UnityDemoSurface', type: 'surface', libraryname: 'ufhn' })
        .width('100%')
        .height('100%')
        .translate({ y: this.canvasOffsetVp })
      if (this.imeVisible) {
        TextInput({ text: this.imeValue, controller: this.imeController })
          .id('UnityDemoIme')
          .width(1)
          .height(1)
          .opacity(0.01)
          .focusable(true)
          .enableKeyboardOnFocus(true)
          .enablePreviewText(false)
          .onWillDelete((_deletion: DeleteValue): boolean => {
            sdk.inputKey(8);
            return false;
          })
          .onChange((value: string) => this.onImeChange(value))
          .onSubmit(() => sdk.inputKey(13))
      }
    }.width('100%').height('100%')
      .onAreaChange((oldArea, newArea) => {
        this.canvasHeightPx = vp2px(Number(newArea.height));
        this.updateCanvasOffset();
      })
  }
}
