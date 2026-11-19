package com.visionforgex.ads; import android.content.Context; import android.app.Activity;
public final class VisionForgeX { public static final String VERSION = "1.3.0";
 public static void initialize(Context c, String k, VfxOptions o, VfxInitListener l){}
 public static boolean isReady(){return false;}
 public static VfxFullScreen interstitial(String p){return null;} public static VfxFullScreen rewarded(String p){return null;} public static VfxFullScreen rewardedInterstitial(String p){return null;} public static VfxFullScreen appOpen(String p){return null;}
 public static void preloadAll(){} public static void setKeyValue(String k, String v){}
 public static boolean isPrivacyOptionsRequired(){return false;} public static void showPrivacyOptions(Activity a, VfxConsentListener l){}
 public static void setMuted(boolean m){} public static void openDebugPanel(Context c){} public static void openAdInspector(Context c){}
 public static void setEventListener(VfxEventListener l){} public static void setRevenueListener(VfxRevenueListener l){}
 public static String settingsSource(){return "";} public static void flush(){} }
