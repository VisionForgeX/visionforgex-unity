package com.visionforgex.unity;

import android.app.Activity;
import android.os.Build;
import android.os.Handler;
import android.os.Looper;
import android.util.Log;
import android.view.Gravity;
import android.view.View;
import android.view.ViewGroup;
import android.view.WindowInsets;
import android.widget.FrameLayout;

import com.visionforgex.ads.VfxBannerView;
import com.visionforgex.ads.VfxEvent;
import com.visionforgex.ads.VfxFormat;
import com.visionforgex.ads.VfxFullScreen;
import com.visionforgex.ads.VfxLogLevel;
import com.visionforgex.ads.VfxOptions;
import com.visionforgex.ads.VfxShowListener;
import com.visionforgex.ads.VfxStatus;
import com.visionforgex.ads.VisionForgeX;

import org.json.JSONArray;
import org.json.JSONObject;

import java.util.ArrayList;
import java.util.HashMap;
import java.util.List;
import java.util.Map;

/**
 * Unity's side of the VisionForgeX Android SDK (vfx-ads-nextgen or vfx-ads 1.3.0): calls in from C#
 * (VfxAndroidBridge.cs), JSON messages out through {@link VfxUnityListener}. No decision is made here: it only
 * passes calls to the SDK and its answers back. Everything that touches a view runs on Android's main thread.
 */
public final class VfxUnityBridge {
    private static final String TAG = "VisionForgeX";
    private static final Handler MAIN = new Handler(Looper.getMainLooper());
    private static volatile VfxUnityListener listener;
    private static final Map<Integer, Banner> BANNERS = new HashMap<>();
    private static boolean answered;

    private VfxUnityBridge() {}

    private static void send(JSONObject m) {
        VfxUnityListener l = listener;
        if (l == null) return;
        try { l.onMessage(m.toString()); } catch (Throwable t) { Log.w(TAG, "Unity did not take a message: " + t); }
    }

    private static JSONObject msg(String kind) {
        JSONObject m = new JSONObject();
        try { m.put("k", kind); } catch (Exception ignored) {}
        return m;
    }

    private static void put(JSONObject m, String k, Object v) {
        try { m.put(k, v == null ? JSONObject.NULL : v); } catch (Exception ignored) {}
    }

    private static void onMain(Runnable r) {
        if (Looper.myLooper() == Looper.getMainLooper()) r.run(); else MAIN.post(r);
    }

    // ------------------------------------------------------------------ start

    public static void init(final Activity activity, final String appKey, final String optionsJson, VfxUnityListener l) {
        listener = l;
        onMain(() -> {
            VfxOptions options;
            try { options = options(optionsJson); } catch (Throwable t) { Log.w(TAG, "Options not read, defaults used: " + t); options = VfxOptions.defaults(); }
            VisionForgeX.setEventListener((VfxEvent e) -> {
                JSONObject m = msg("event");
                put(m, "type", e.getType());
                put(m, "placement", e.getPlacement());
                put(m, "format", e.getFormat().getWire());
                put(m, "time", e.getTime());
                JSONObject d = new JSONObject();
                for (Map.Entry<String, Object> x : e.getDetails().entrySet()) put(d, x.getKey(), plain(x.getValue()));
                put(m, "details", d);
                if (e.getFormat() == VfxFormat.BANNER && "fill".equals(e.getType()))
                    for (Banner b : BANNERS.values()) if (b.placement.equals(e.getPlacement())) b.markLoaded();
                send(m);
            });
            VisionForgeX.setRevenueListener((placement, format, micros, currency, precision, source) -> {
                JSONObject m = msg("revenue");
                put(m, "placement", placement);
                put(m, "format", format.getWire());
                put(m, "valueMicros", micros);
                put(m, "currency", currency);
                put(m, "precision", precision);
                put(m, "source", source);
                send(m);
            });
            answered = false;
            VisionForgeX.initialize(activity, appKey == null ? "" : appKey, options, (VfxStatus st) -> {
                if (answered) return;
                answered = true;
                JSONObject m = msg("init");
                put(m, "ready", st.getReady());
                put(m, "reason", st.getReason());
                put(m, "fromCache", st.getFromCache());
                send(m);
            });
        });
    }

    private static Object plain(Object v) {
        if (v == null || v instanceof String || v instanceof Boolean || v instanceof Integer || v instanceof Long || v instanceof Double) return v;
        if (v instanceof Number) return ((Number) v).doubleValue();
        return String.valueOf(v);
    }

    static VfxOptions options(String json) throws Exception {
        JSONObject o = new JSONObject(json == null || json.isEmpty() ? "{}" : json);
        VfxOptions.Builder b = new VfxOptions.Builder();
        b.testDeviceIds(strings(o.optJSONArray("testDeviceIds")));
        String level = o.isNull("logLevel") ? "" : o.optString("logLevel", "");
        switch (level) {
            case "none": b.logLevel(VfxLogLevel.NONE); break;
            case "error": b.logLevel(VfxLogLevel.ERROR); break;
            case "warn": b.logLevel(VfxLogLevel.WARN); break;
            case "info": b.logLevel(VfxLogLevel.INFO); break;
            case "debug": b.logLevel(VfxLogLevel.DEBUG); break;
            default: break;
        }
        if (o.optBoolean("consentDebugEea", false)) b.consentDebugEea(true, strings(o.optJSONArray("consentTestDeviceHashedIds")));
        String base = o.isNull("baseUrl") ? "" : o.optString("baseUrl", "");
        if (!base.trim().isEmpty()) b.baseUrl(base.trim());
        b.autoPreload(o.optBoolean("autoPreload", true));
        if (o.optBoolean("googleDemoAds", false)) b.googleDemoAds();
        String testing = o.isNull("settingsForTesting") ? "" : o.optString("settingsForTesting", "");
        if (!testing.trim().isEmpty()) b.settingsForTesting(testing);
        return b.build();
    }

    private static List<String> strings(JSONArray a) {
        List<String> out = new ArrayList<>();
        if (a == null) return out;
        for (int i = 0; i < a.length(); i++) { String s = a.optString(i, ""); if (!s.trim().isEmpty()) out.add(s.trim()); }
        return out;
    }

    // ------------------------------------------------------------------ placements

    private static VfxFullScreen fullScreen(String placement, String format) {
        VfxFormat f = VfxFormat.fromWire(format == null ? "" : format);
        if (f == VfxFormat.REWARDED) return VisionForgeX.rewarded(placement);
        if (f == VfxFormat.REWARDED_INTERSTITIAL) return VisionForgeX.rewardedInterstitial(placement);
        if (f == VfxFormat.APP_OPEN) return VisionForgeX.appOpen(placement);
        return VisionForgeX.interstitial(placement);
    }

    public static boolean isReady() { return VisionForgeX.isReady(); }

    public static boolean fullScreenReady(String placement, String format) { return fullScreen(placement, format).isReady(); }

    public static void load(final String placement, final String format) { onMain(() -> fullScreen(placement, format).load()); }

    public static void show(final Activity activity, final int id, final String placement, final String format) {
        onMain(() -> {
            if (activity == null || activity.isFinishing()) { showMessage(id, "notShown", "no_activity", null, 0); return; }
            fullScreen(placement, format).show(activity, new VfxShowListener() {
                private boolean done;
                @Override public void onShown() { showMessage(id, "shown", null, null, 0); }
                @Override public void onClicked() { showMessage(id, "clicked", null, null, 0); }
                @Override public void onDismissed() { if (!done) { done = true; showMessage(id, "dismissed", null, null, 0); } }
                @Override public void onNotShown(String reason) { if (!done) { done = true; showMessage(id, "notShown", reason, null, 0); } }
                @Override public void onReward(String type, int amount) { showMessage(id, "reward", null, type, amount); }
            });
        });
    }

    private static void showMessage(int id, String event, String reason, String type, int amount) {
        JSONObject m = msg("show");
        put(m, "id", id);
        put(m, "e", event);
        if (reason != null) put(m, "r", reason);
        if (type != null) { put(m, "t", type); put(m, "a", amount); }
        send(m);
    }

    public static void preloadAll() { onMain(VisionForgeX::preloadAll); }

    /** {@code remove}: the key is taken off (Unity cannot pass a null string through JNI). */
    public static void setKeyValue(final String key, final String value, final boolean remove) { onMain(() -> VisionForgeX.setKeyValue(key, remove ? null : value)); }

    // ------------------------------------------------------------------ consent and tools

    public static boolean isPrivacyOptionsRequired() { return VisionForgeX.isPrivacyOptionsRequired(); }

    public static void showPrivacyOptions(final Activity activity, final int id) {
        onMain(() -> {
            if (activity == null) { consent(id, "no activity"); return; }
            VisionForgeX.showPrivacyOptions(activity, error -> consent(id, error));
        });
    }

    private static void consent(int id, String error) {
        JSONObject m = msg("consent");
        put(m, "id", id);
        put(m, "error", error);
        send(m);
    }

    public static void setMuted(boolean muted) { VisionForgeX.setMuted(muted); }

    public static void openAdInspector(final Activity activity) { onMain(() -> { if (activity != null) VisionForgeX.openAdInspector(activity); }); }

    public static boolean openDebugPanel(final Activity activity) {
        if (activity == null) return false;
        onMain(() -> VisionForgeX.openDebugPanel(activity));
        return true;
    }

    public static String settingsSource() { return VisionForgeX.settingsSource(); }

    public static void flush() { VisionForgeX.flush(); }

    public static String nativeVersion() { return VisionForgeX.VERSION; }

    // ------------------------------------------------------------------ banners

    /** One banner: the SDK's own view in a full-width strip at the top or bottom, padded clear of notches and bars. */
    private static final class Banner {
        final int id;
        final String placement;
        final boolean top;
        final Activity activity;
        FrameLayout strip;
        VfxBannerView view;
        int lastHeight = -1;
        boolean loaded;

        Banner(int id, String placement, boolean top, Activity activity) { this.id = id; this.placement = placement; this.top = top; this.activity = activity; }

        void attach() {
            if (strip != null || activity == null || activity.isFinishing()) return;
            strip = new FrameLayout(activity);
            view = new VfxBannerView(activity, placement);
            strip.addView(view, new FrameLayout.LayoutParams(ViewGroup.LayoutParams.MATCH_PARENT, ViewGroup.LayoutParams.WRAP_CONTENT, Gravity.CENTER_HORIZONTAL));
            strip.setOnApplyWindowInsetsListener((v, insets) -> {
                int t = 0, b = 0, l = 0, r = 0;
                if (Build.VERSION.SDK_INT >= 30) {
                    android.graphics.Insets i = insets.getInsets(WindowInsets.Type.systemBars() | WindowInsets.Type.displayCutout());
                    t = i.top; b = i.bottom; l = i.left; r = i.right;
                } else {
                    t = insets.getSystemWindowInsetTop(); b = insets.getSystemWindowInsetBottom();
                    l = insets.getSystemWindowInsetLeft(); r = insets.getSystemWindowInsetRight();
                }
                v.setPadding(l, top ? t : 0, r, top ? 0 : b);
                return insets;
            });
            strip.addOnLayoutChangeListener((v, l, t, r, b, ol, ot, or, ob) -> report());
            FrameLayout.LayoutParams lp = new FrameLayout.LayoutParams(ViewGroup.LayoutParams.MATCH_PARENT, ViewGroup.LayoutParams.WRAP_CONTENT,
                    (top ? Gravity.TOP : Gravity.BOTTOM) | Gravity.CENTER_HORIZONTAL);
            activity.addContentView(strip, lp);
            strip.requestApplyInsets();
        }

        void setVisible(boolean visible) {
            if (visible) attach();
            if (strip != null) strip.setVisibility(visible ? View.VISIBLE : View.GONE);
            report();
        }

        /** An ad filled it: from now on it covers part of the screen. */
        void markLoaded() {
            loaded = true;
            if (strip != null) strip.post(this::report);
        }

        void report() {
            int h = !loaded || strip == null || strip.getVisibility() != View.VISIBLE || view == null || view.getHeight() <= 0 ? 0 : strip.getHeight();
            if (h == lastHeight) return;
            lastHeight = h;
            JSONObject m = msg("banner");
            put(m, "id", id);
            put(m, "h", h);
            send(m);
        }

        void destroy() {
            if (strip != null && strip.getParent() instanceof ViewGroup) ((ViewGroup) strip.getParent()).removeView(strip);
            strip = null;
            view = null;
        }
    }

    public static void bannerCreate(final Activity activity, final int id, final String placement, final int position) {
        onMain(() -> BANNERS.put(id, new Banner(id, placement, position == 1, activity)));
    }

    public static void bannerSetVisible(final int id, final boolean visible) {
        onMain(() -> { Banner b = BANNERS.get(id); if (b != null) b.setVisible(visible); });
    }

    public static void bannerDestroy(final int id) {
        onMain(() -> { Banner b = BANNERS.remove(id); if (b != null) b.destroy(); });
    }
}
