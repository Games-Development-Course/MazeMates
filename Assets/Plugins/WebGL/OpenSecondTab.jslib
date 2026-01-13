mergeInto(LibraryManager.library, {
  OpenSecondTabWithJoinCode: function (joinCodePtr) {
    var code = UTF8ToString(joinCodePtr);
    var url = new URL(window.location.href);
    url.searchParams.set("joinCode", code);
    window.open(url.toString(), "_blank");
  }
});
