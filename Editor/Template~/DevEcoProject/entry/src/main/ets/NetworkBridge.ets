import { http, webSocket } from '@kit.NetworkKit';
import sdk from 'libufhn.so';

interface NetworkCommand {
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

export class NetworkBridge {
  private sockets: Map<number, webSocket.WebSocket> = new Map();
  private onIme: (enabled: boolean, bottomY: number, height: number, screenHeight: number) => void;
  private onUnityMessage: (message: string) => void;

  constructor(onIme: (enabled: boolean, bottomY: number, height: number, screenHeight: number) => void,
    onUnityMessage: (message: string) => void = () => {}) {
    this.onIme = onIme;
    this.onUnityMessage = onUnityMessage;
    sdk.registerNetwork((command: NetworkCommand): void => this.dispatch(command));
  }

  private dispatch(command: NetworkCommand): void {
    switch (command.kind) {
      case 'ime':
        this.onIme(command.binary, command.id, command.code, Number(command.text));
        break;
      case 'unityToNative':
        this.onUnityMessage(command.text);
        break;
      case 'http':
        this.requestHttp(command);
        break;
      case 'wsConnect':
        this.connectSocket(command);
        break;
      case 'wsSend': {
        const socket = this.sockets.get(command.id);
        if (socket) {
          socket.send(command.binary ? command.data : command.text).catch((error: Error) => {
            sdk.websocketEvent(command.id, 2, String(error), 0, String(error), false);
          });
        }
        break;
      }
      case 'wsClose': {
        const socket = this.sockets.get(command.id);
        if (socket) {
          socket.close({ code: command.code, reason: command.reason }).catch(() => {});
          this.sockets.delete(command.id);
        }
        break;
      }
    }
  }

  private method(name: string): http.RequestMethod {
    switch (name.toUpperCase()) {
      case 'POST': return http.RequestMethod.POST;
      case 'PUT': return http.RequestMethod.PUT;
      case 'DELETE': return http.RequestMethod.DELETE;
      case 'HEAD': return http.RequestMethod.HEAD;
      case 'OPTIONS': return http.RequestMethod.OPTIONS;
      case 'TRACE': return http.RequestMethod.TRACE;
      case 'CONNECT': return http.RequestMethod.CONNECT;
      default: return http.RequestMethod.GET;
    }
  }

  private requestHttp(command: NetworkCommand): void {
    const request = http.createHttp();
    const options: http.HttpRequestOptions = {
      method: this.method(command.method),
      header: command.headers,
      expectDataType: http.HttpDataType.ARRAY_BUFFER,
      usingCache: false,
      readTimeout: 60000,
      connectTimeout: 30000
    };
    if (command.data.byteLength > 0) options.extraData = command.data;
    request.request(command.url, options).then((response: http.HttpResponse) => {
      sdk.completeHttp(command.id, response.responseCode,
        response.header as Record<string, string>, response.result as ArrayBuffer, '');
    }).catch((error: Error) => {
      sdk.completeHttp(command.id, 0, {}, new ArrayBuffer(0), String(error));
    }).finally(() => request.destroy());
  }

  private connectSocket(command: NetworkCommand): void {
    const socket = webSocket.createWebSocket();
    this.sockets.set(command.id, socket);
    socket.on('open', (error) => {
      if (error) {
        sdk.websocketEvent(command.id, 2, String(error), 0, String(error), false);
      } else {
        sdk.websocketEvent(command.id, 0, '', 0, '', false);
      }
    });
    socket.on('message', (error, value) => {
      if (error) {
        sdk.websocketEvent(command.id, 2, String(error), 0, String(error), false);
      } else if (value instanceof ArrayBuffer) {
        sdk.websocketEvent(command.id, 1, value, 0, '', true);
      } else {
        sdk.websocketEvent(command.id, 1, value, 0, '', false);
      }
    });
    socket.on('close', (error, result) => {
      const code = result ? result.code : 0;
      const reason = result ? result.reason : String(error);
      sdk.websocketEvent(command.id, 3, '', code, reason, false);
      this.sockets.delete(command.id);
    });
    socket.on('error', (error) => {
      sdk.websocketEvent(command.id, 2, String(error), 0, String(error), false);
    });
    socket.connect(command.url).catch((error: Error) => {
      sdk.websocketEvent(command.id, 2, String(error), 0, String(error), false);
      this.sockets.delete(command.id);
    });
  }
}
