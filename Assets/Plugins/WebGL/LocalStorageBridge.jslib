/*
 * LocalStorageBridge.jslib
 * Unity WebGL JavaScript plugin – localStorage olvasás/törlés C#-ból.
 *
 * Ezeket a függvényeket hívja a GoogleAuthWebGL.cs:
 *   ReadLocalStorage(key)   → visszaadja az értéket, vagy üres stringet
 *   RemoveLocalStorage(key) → törli a kulcsot
 */
mergeInto(LibraryManager.library, {

    ReadLocalStorage: function(keyPtr) {
        var key   = UTF8ToString(keyPtr);
        var value = localStorage.getItem(key);
        if (value === null) value = "";
        var bufSize = lengthBytesUTF8(value) + 1;
        var buf     = _malloc(bufSize);
        stringToUTF8(value, buf, bufSize);
        return buf;
    },

    RemoveLocalStorage: function(keyPtr) {
        var key = UTF8ToString(keyPtr);
        localStorage.removeItem(key);
    }

});
