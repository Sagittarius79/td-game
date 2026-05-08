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
    },

    /*
     * SetupGoogleTokenListener – figyeli a google-callback.html window.postMessage üzenetét.
     * Ha megérkezik a token, SendMessage-gel szól vissza a Unity GameObject-nek.
     */
    SetupGoogleTokenListener: function(objectNamePtr, methodNamePtr) {
        var objectName = UTF8ToString(objectNamePtr);
        var methodName = UTF8ToString(methodNamePtr);

        // Régi listener eltávolítása (ha volt)
        if (window.__googleTokenHandler) {
            window.removeEventListener('message', window.__googleTokenHandler);
        }

        window.__googleTokenHandler = function(event) {
            if (event.data && event.data.type === 'google_token' && event.data.token) {
                window.removeEventListener('message', window.__googleTokenHandler);
                window.__googleTokenHandler = null;
                SendMessage(objectName, methodName, event.data.token);
            }
        };
        window.addEventListener('message', window.__googleTokenHandler);
    }

});
