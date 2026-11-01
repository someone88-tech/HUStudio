console.log('[UnityDemo] Starting WebGL test scene');
var unityDemoInstance = null;
var unityPendingMessages = [];
globalThis.nativeToUnity = function (gameObject, method, message) {
    if (unityDemoInstance) {
        unityDemoInstance.SendMessage(gameObject, method, message);
    } else {
        unityPendingMessages.push([gameObject, method, message]);
    }
};
var canvas = document.querySelector('#unity-canvas');
canvas.width = screenWidth || 960;
canvas.height = screenHeight || 600;
var loaderCode = __readFileText('Build/out.loader.js');
if (!loaderCode) {
    console.error('[UnityDemo] Build/out.loader.js missing');
} else {
    eval(loaderCode);
    createUnityInstance(canvas, {
        dataUrl: 'Build/out.data',
        frameworkUrl: 'Build/out.framework.js',
        codeUrl: 'Build/out.wasm',
        streamingAssetsUrl: 'StreamingAssets',
        companyName: 'HUStudio',
        productName: '__APP_NAME__',
        productVersion: '1.0',
        showBanner: function (message, type) {
            console.log('[UnityDemo] ' + (type || 'info') + ': ' + message);
        }
    }, function (progress) {
        console.log('[UnityDemo] Loading ' + Math.round(progress * 100) + '%');
    }).then(function (instance) {
        unityDemoInstance = instance;
        while (unityPendingMessages.length > 0) {
            var pending = unityPendingMessages.shift();
            instance.SendMessage(pending[0], pending[1], pending[2]);
        }
        console.log('[UnityDemo] Unity instance created');
    }).catch(function (error) {
        console.error('[UnityDemo] Unity startup failed: ' + error);
    });
}
