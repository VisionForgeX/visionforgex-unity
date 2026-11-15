// The C functions C# calls on iOS (VfxIosBridge.cs, DllImport("__Internal")). They turn C strings into Objective-C
// ones and pass each call to VfxUnityBridgeSwift (VfxUnityBridge.swift), which calls the VisionForgeX iOS SDK.
// Answers come back as JSON through the callback C# registers. No decision is made here.

#import <Foundation/Foundation.h>
#import <UIKit/UIKit.h>
#include <string.h>

// The Swift class, as Objective-C sees it (written out here so no generated header is needed). The selectors are
// the @objc(...) names in VfxUnityBridge.swift: change both together.
@interface VfxUnityBridgeSwift : NSObject
@property (class, nonatomic, copy, nullable) void (^onMessage)(NSString * _Nonnull json);
+ (void)startWithKey:(NSString * _Nonnull)key options:(NSString * _Nonnull)options;
+ (BOOL)isReady;
+ (BOOL)fullScreenReady:(NSString * _Nonnull)placement format:(NSString * _Nonnull)format;
+ (void)load:(NSString * _Nonnull)placement format:(NSString * _Nonnull)format;
+ (void)show:(NSInteger)showId placement:(NSString * _Nonnull)placement format:(NSString * _Nonnull)format;
+ (void)preloadAll;
+ (void)setKeyValue:(NSString * _Nonnull)key value:(NSString * _Nullable)value;
+ (BOOL)isPrivacyOptionsRequired;
+ (void)showPrivacyOptions:(NSInteger)requestId;
+ (void)setMuted:(BOOL)muted;
+ (void)openAdInspector;
+ (NSString * _Nonnull)settingsSource;
+ (void)flush;
+ (NSString * _Nonnull)nativeVersion;
+ (void)bannerCreate:(NSInteger)bannerId placement:(NSString * _Nonnull)placement position:(NSInteger)position;
+ (void)bannerSetVisible:(NSInteger)bannerId visible:(BOOL)visible;
+ (void)bannerDestroy:(NSInteger)bannerId;
@end

typedef void (*VfxUnityCallback)(const char *json);
static VfxUnityCallback VfxCallback = NULL;

static NSString *VfxString(const char *s) { return s ? [NSString stringWithUTF8String:s] ?: @"" : @""; }

// IL2CPP frees a returned string with free(): hand it a copy made with malloc.
static char *VfxCopy(NSString *s) {
    const char *u = [s UTF8String];
    return strdup(u ? u : "");
}

extern "C" {

void VfxUnity_SetCallback(VfxUnityCallback callback) {
    VfxCallback = callback;
    VfxUnityBridgeSwift.onMessage = ^(NSString *json) {
        if (VfxCallback) VfxCallback([json UTF8String]);
    };
}

void VfxUnity_Initialize(const char *appKey, const char *optionsJson) {
    [VfxUnityBridgeSwift startWithKey:VfxString(appKey) options:VfxString(optionsJson)];
}

bool VfxUnity_IsReady(void) { return [VfxUnityBridgeSwift isReady]; }

bool VfxUnity_FullScreenReady(const char *placement, const char *format) {
    return [VfxUnityBridgeSwift fullScreenReady:VfxString(placement) format:VfxString(format)];
}

void VfxUnity_Load(const char *placement, const char *format) {
    [VfxUnityBridgeSwift load:VfxString(placement) format:VfxString(format)];
}

void VfxUnity_Show(int showId, const char *placement, const char *format) {
    [VfxUnityBridgeSwift show:showId placement:VfxString(placement) format:VfxString(format)];
}

void VfxUnity_PreloadAll(void) { [VfxUnityBridgeSwift preloadAll]; }

void VfxUnity_SetKeyValue(const char *key, const char *value, bool remove) {
    [VfxUnityBridgeSwift setKeyValue:VfxString(key) value:(remove ? nil : VfxString(value))];
}

bool VfxUnity_IsPrivacyOptionsRequired(void) { return [VfxUnityBridgeSwift isPrivacyOptionsRequired]; }

void VfxUnity_ShowPrivacyOptions(int requestId) { [VfxUnityBridgeSwift showPrivacyOptions:requestId]; }

void VfxUnity_SetMuted(bool muted) { [VfxUnityBridgeSwift setMuted:muted]; }

void VfxUnity_OpenAdInspector(void) { [VfxUnityBridgeSwift openAdInspector]; }

char *VfxUnity_SettingsSource(void) { return VfxCopy([VfxUnityBridgeSwift settingsSource]); }

void VfxUnity_Flush(void) { [VfxUnityBridgeSwift flush]; }

char *VfxUnity_NativeVersion(void) { return VfxCopy([VfxUnityBridgeSwift nativeVersion]); }

void VfxUnity_BannerCreate(int bannerId, const char *placement, int position) {
    [VfxUnityBridgeSwift bannerCreate:bannerId placement:VfxString(placement) position:position];
}

void VfxUnity_BannerSetVisible(int bannerId, bool visible) { [VfxUnityBridgeSwift bannerSetVisible:bannerId visible:visible]; }

void VfxUnity_BannerDestroy(int bannerId) { [VfxUnityBridgeSwift bannerDestroy:bannerId]; }

}
