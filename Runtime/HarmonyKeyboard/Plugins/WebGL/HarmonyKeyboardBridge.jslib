mergeInto(LibraryManager.library, {
    HarmonyShowKeyboard: function(screenY, screenHeight, screenTotalHeight) {
        if (typeof native !== 'undefined' && native.sendMessage) {
            native.sendMessage('keyboard', 'show:' + screenY + ':' + screenHeight + ':' + screenTotalHeight);
        }
    },
    HarmonyHideKeyboard: function() {
        if (typeof native !== 'undefined' && native.sendMessage) {
            native.sendMessage('keyboard', 'hide');
        }
    },
    HarmonyUnityToNative: function(message) {
        if (typeof native !== 'undefined' && native.sendMessage) {
            native.sendMessage('unityToNative', UTF8ToString(message));
        }
    }
});
