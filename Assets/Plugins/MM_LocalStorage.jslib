mergeInto(LibraryManager.library, {
  MM_SetItem: function (kPtr, vPtr) {
    localStorage.setItem(
      UTF8ToString(kPtr),
      UTF8ToString(vPtr)
    );
  },
  MM_GetItem: function (kPtr) {
    const v = localStorage.getItem(UTF8ToString(kPtr));
    if (!v) return 0;
    const len = lengthBytesUTF8(v) + 1;
    const ptr = _malloc(len);
    stringToUTF8(v, ptr, len);
    return ptr;
  }
});
