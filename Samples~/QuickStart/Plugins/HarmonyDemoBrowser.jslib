mergeInto(LibraryManager.library, {
  HarmonyDemoBrowserApis: function (kindPtr, websocketUrlPtr) {
    var kind = UTF8ToString(kindPtr);
    var report = function (message) {
      console.log('[HarmonyDemo] ' + message);
      if (typeof SendMessage === 'function')
        SendMessage('HarmonyDemoController', 'OnDemoResult', message);
    };
    if (kind === 'storage') {
      try {
        var previous = Number(localStorage.getItem('HarmonyDemo') || '0');
        localStorage.setItem('HarmonyDemo', String(previous + 1));
        report('localStorage: previous=' + previous + ', current=' + (previous + 1));
      } catch (e) { report('localStorage ERROR: ' + e); }
      try {
        var request = indexedDB.open('HarmonyDemo', 1);
        request.onupgradeneeded = function (event) {
          var db = event.target.result;
          if (!db.objectStoreNames.contains('items')) db.createObjectStore('items');
        };
        request.onsuccess = function (event) {
          var db = event.target.result;
          var readTx = db.transaction('items', 'readonly');
          var get = readTx.objectStore('items').get('probe');
          get.onsuccess = function () {
            var previous = Number(get.result || '0');
            readTx.oncomplete = function () {
              var writeTx = db.transaction('items', 'readwrite');
              writeTx.objectStore('items').put(String(previous + 1), 'probe');
              writeTx.oncomplete = function () {
                report('IndexedDB: previous=' + previous + ', current=' + (previous + 1));
                db.close();
              };
            };
          };
          get.onerror = function () { report('IndexedDB get ERROR'); db.close(); };
        };
        request.onerror = function () { report('IndexedDB open ERROR'); };
      } catch (e) { report('IndexedDB ERROR: ' + e); }
      return;
    }
    if (kind === 'worker') {
      try {
        var blob = new Blob(['onmessage=function(e){postMessage(e.data)}'],
                            { type: 'application/javascript' });
        var url = URL.createObjectURL(blob);
        var worker = new Worker(url);
        worker.onmessage = function (event) {
          report('Worker: ' + event.data);
          worker.terminate();
          URL.revokeObjectURL(url);
        };
        worker.onerror = function (event) { report('Worker ERROR: ' + event.message); };
        worker.postMessage('echo-ok');
      } catch (e) { report('Worker ERROR: ' + e); }
      return;
    }
    if (kind === 'websocket') {
      try {
        var socket = new WebSocket(UTF8ToString(websocketUrlPtr));
        socket.onopen = function () { socket.send('harmony-echo'); };
        socket.onmessage = function (event) {
          report('WebSocket: ' + event.data);
          socket.close();
        };
        socket.onerror = function () { report('WebSocket ERROR'); };
      } catch (e) { report('WebSocket ERROR: ' + e); }
    }
  }
});
