import { UIAbility } from '@kit.AbilityKit';
import { window } from '@kit.ArkUI';

export default class EntryAbility extends UIAbility {
  onWindowStageCreate(windowStage: window.WindowStage): void {
    const mainWindow = windowStage.getMainWindowSync();
    mainWindow.setWindowLayoutFullScreen(true)
      .then(() => mainWindow.setWindowSystemBarEnable([]))
      .catch((error: Error) => console.error(`HUStudio fullscreen: ${error}`));
    windowStage.loadContent('pages/Index');
  }
}
