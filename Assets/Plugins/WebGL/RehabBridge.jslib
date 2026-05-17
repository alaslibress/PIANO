// RehabBridge.jslib — puente JS↔Unity para autenticacion sin password en WebGL.
//
// Via 2 (postMessage/host vars): el host (WebView de la app movil o pagina web)
// asigna las variables antes de que Unity arranque:
//   window._rehabToken  = "JWT_TOKEN_AQUI";
//   window._rehabDni    = "12345678A";
//   window._rehabApiUrl = "https://api.rehabiapp.es";
//
// Via 1 (URL params) tiene prioridad — ver TelemetryUploader.cs.

mergeInto(LibraryManager.library, {

  $RehabUrlParam: function(name) {
    try {
      var p = new URLSearchParams(window.location.search);
      var v = p.get(name);
      return v ? v : '';
    } catch(e) { return ''; }
  },

  RehabGetToken__deps: ['$RehabUrlParam'],
  RehabGetToken: function() {
    var val = RehabUrlParam('token') || window._rehabToken || '';
    var len = lengthBytesUTF8(val) + 1;
    var buf = _malloc(len);
    stringToUTF8(val, buf, len);
    return buf;
  },

  RehabGetDni__deps: ['$RehabUrlParam'],
  RehabGetDni: function() {
    var val = RehabUrlParam('dni') || window._rehabDni || '';
    var len = lengthBytesUTF8(val) + 1;
    var buf = _malloc(len);
    stringToUTF8(val, buf, len);
    return buf;
  },

  RehabGetApiUrl__deps: ['$RehabUrlParam'],
  RehabGetApiUrl: function() {
    var val = RehabUrlParam('api') || window._rehabApiUrl || '';
    var len = lengthBytesUTF8(val) + 1;
    var buf = _malloc(len);
    stringToUTF8(val, buf, len);
    return buf;
  },

  // Notifica al frame padre (WebView) que la sesion de juego fue enviada correctamente.
  // La app movil escucha este mensaje para cerrar el WebView.
  NotificarSesionEnviada: function() {
    try {
      var msg = JSON.stringify({ type: 'rehab_session_complete', ts: Date.now() });
      if (window.parent && window.parent !== window) {
        window.parent.postMessage(msg, '*');
      }
      // Para React Native WebView que usa window.ReactNativeWebView.postMessage
      if (window.ReactNativeWebView) {
        window.ReactNativeWebView.postMessage(msg);
      }
    } catch(e) {
      console.warn('[RehabBridge] NotificarSesionEnviada error:', e);
    }
  }

});
