package com.visionforgex.unity;

/**
 * How the bridge talks back to Unity: one JSON message at a time (the keys are listed in IVfxBridge.cs).
 * Implemented in C# by an AndroidJavaProxy; called on Android's main thread.
 */
public interface VfxUnityListener {
    void onMessage(String json);
}
