mergeInto(LibraryManager.library, {
  StrangerCopyCode: function (value, target) {
    var code = UTF8ToString(value);
    var receiver = UTF8ToString(target);
    function report(ok) {
      Module.SendMessage(receiver, 'CopyCodeResult', ok ? 'ok' : 'failed');
    }
    function fallback() {
      var field = document.createElement('textarea');
      field.value = code;
      field.style.position = 'fixed';
      field.style.opacity = '0';
      document.body.appendChild(field);
      field.select();
      var copied = false;
      try { copied = document.execCommand('copy'); }
      finally {
        field.remove();
        Module.canvas.focus();
        report(copied);
      }
    }
    if (navigator.clipboard && navigator.clipboard.writeText) {
      navigator.clipboard.writeText(code).then(function () { report(true); }, fallback);
    } else {
      fallback();
    }
  }
});
