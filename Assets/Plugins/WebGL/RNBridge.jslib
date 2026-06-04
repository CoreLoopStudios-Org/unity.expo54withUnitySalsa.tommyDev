mergeInto(LibraryManager.library, {
    // Called by ConvaiNPCBridge.cs (C# extern void SendToRN) to post events to the
    // page JS layer.  The RN webview must define window.OnConvaiEvent before Unity
    // loads, or register it on the unityInstance.on("message") equivalant.
    SendToRN: function (jsonPtr) {
        var json = UTF8ToString(jsonPtr);
        if (typeof window.OnConvaiEvent === "function") {
            try { window.OnConvaiEvent(json); } catch (e) { console.error("[RNBridge] OnConvaiEvent threw:", e); }
        }
    }
});
