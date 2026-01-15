var _mazeMatesSecondWin = null;

mergeInto(LibraryManager.library, {
  OpenSecondWindowBlank: function () {
    _mazeMatesSecondWin = window.open("about:blank", "_blank");
    if (_mazeMatesSecondWin) _mazeMatesSecondWin.focus();
  },

  NavigateSecondWindowWithJoinCode: function (joinCodePtr) {
    var code = UTF8ToString(joinCodePtr);
    var url = new URL(window.location.href);
    url.searchParams.set("joinCode", code);

    if (_mazeMatesSecondWin && !_mazeMatesSecondWin.closed) {
      _mazeMatesSecondWin.location.href = url.toString();
      _mazeMatesSecondWin.focus();
    } else {
      window.open(url.toString(), "_blank");
    }
  },

  // ✅ תאימות לאחור: כדי שה-build לא ייפול אם C# עדיין קורא לשם הזה
  OpenSecondTabWithJoinCode: function (joinCodePtr) {
    _mazeMatesSecondWin = window.open("about:blank", "_blank");
    if (_mazeMatesSecondWin) _mazeMatesSecondWin.focus();

    var code = UTF8ToString(joinCodePtr);
    var url = new URL(window.location.href);
    url.searchParams.set("joinCode", code);

    if (_mazeMatesSecondWin && !_mazeMatesSecondWin.closed) {
      _mazeMatesSecondWin.location.href = url.toString();
      _mazeMatesSecondWin.focus();
    } else {
      window.open(url.toString(), "_blank");
    }
  }
});
