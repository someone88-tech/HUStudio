export const configure: (commonNpk: ArrayBuffer, projectNpk: ArrayBuffer,
                         storageDir?: string, projectDir?: string) => boolean;
export const status: () => string;
export const requiresExternalCommon: () => boolean;
export interface NetworkCommand {
  kind: string;
  id: number;
  url: string;
  method: string;
  headers: Record<string, string>;
  data: ArrayBuffer;
  text: string;
  code: number;
  reason: string;
  binary: boolean;
}
export const registerNetwork: (handler: (command: NetworkCommand) => void) => void;
export const completeHttp: (id: number, status: number, headers: Record<string, string>,
                            body: ArrayBuffer, error: string) => void;
export const websocketEvent: (id: number, type: number, data: string | ArrayBuffer,
                              code: number, reason: string, binary: boolean) => void;
export const inputText: (text: string) => void;
export const inputKey: (keyCode: number) => void;
export const evalScript: (source: string) => void;
export const nativeToUnity: (gameObject: string, method: string, message: string) => boolean;
