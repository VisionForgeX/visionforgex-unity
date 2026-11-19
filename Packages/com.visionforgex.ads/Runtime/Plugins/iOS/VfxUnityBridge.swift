import Foundation
import UIKit
import VisionForgeX

/// Unity's side of the VisionForgeX iOS SDK 1.3.0: called by VfxUnityBridge.mm (the C functions C# calls), answers
/// sent back as JSON through `onMessage` (the keys are listed in IVfxBridge.cs). No decision is made here: it only
/// passes calls to the SDK and its answers back. Every call arrives on the main thread (Unity's on iOS).
@objc(VfxUnityBridgeSwift)
public final class VfxUnityBridgeSwift: NSObject {
    /// Set by the .mm: hands one JSON message to C#.
    @objc public static var onMessage: ((String) -> Void)?

    private static var banners: [Int: VfxUnityBanner] = [:]
    private static var answered = false

    static func send(_ m: [String: Any]) {
        guard let data = try? JSONSerialization.data(withJSONObject: m, options: []), let s = String(data: data, encoding: .utf8) else { return }
        if Thread.isMainThread { onMessage?(s) } else { DispatchQueue.main.async { onMessage?(s) } }
    }

    /// Values JSON can carry.
    private static func plain(_ v: Any) -> Any {
        switch v {
        case is String, is Bool, is Int, is Int64, is Double: return v
        case let n as NSNumber: return n.doubleValue
        default: return "\(v)"
        }
    }

    /// The view controller full-screen ads and forms are shown from: Unity's own, on top of what it presents.
    static func top() -> UIViewController? {
        let scenes = UIApplication.shared.connectedScenes.compactMap { $0 as? UIWindowScene }
        var window = scenes.flatMap { $0.windows }.first { $0.isKeyWindow }
        if window == nil { window = UIApplication.shared.delegate?.window ?? nil }
        var vc = window?.rootViewController
        while let p = vc?.presentedViewController { vc = p }
        return vc
    }

    static func rootView() -> UIView? {
        let scenes = UIApplication.shared.connectedScenes.compactMap { $0 as? UIWindowScene }
        var window = scenes.flatMap { $0.windows }.first { $0.isKeyWindow }
        if window == nil { window = UIApplication.shared.delegate?.window ?? nil }
        return window?.rootViewController?.view
    }

    // MARK: start

    @objc(startWithKey:options:)
    public static func start(key: String, options json: String) {
        var opts = VfxOptions()
        let o = ((try? JSONSerialization.jsonObject(with: Data(json.utf8))) as? [String: Any]) ?? [:]
        if o["googleDemoAds"] as? Bool == true { opts = VfxOptions.googleDemoAds() }
        if let ids = o["testDeviceIds"] as? [String] { opts.testDeviceIds = ids }
        switch o["logLevel"] as? String {
        case "none": opts.logLevel = VfxLogLevel.none
        case "error": opts.logLevel = .error
        case "warn": opts.logLevel = .warn
        case "info": opts.logLevel = .info
        case "debug": opts.logLevel = .debug
        default: break
        }
        if o["consentDebugEea"] as? Bool == true {
            opts.consentDebugEea = true
            opts.consentTestDeviceHashedIds = (o["consentTestDeviceHashedIds"] as? [String]) ?? []
        }
        if let u = o["baseUrl"] as? String, !u.isEmpty { opts.baseUrl = u }
        opts.autoPreload = (o["autoPreload"] as? Bool) != false
        if let t = o["settingsForTesting"] as? String, !t.isEmpty { opts.settingsForTesting = t }

        answered = false
        VisionForgeX.initialize(appKey: key, options: opts) { st in
            if answered { return }
            answered = true
            send(["k": "init", "ready": st.ready, "reason": st.reason, "fromCache": st.fromCache])
        }
        VisionForgeX.eventListener = { e in
            var d: [String: Any] = [:]
            for (k, v) in e.details { d[k] = plain(v) }
            if e.format == .banner && e.type == "fill" { for b in banners.values where b.placement == e.placement { b.markLoaded() } }
            send(["k": "event", "type": e.type, "placement": e.placement, "format": e.format.wire, "time": e.time, "details": d])
        }
        VisionForgeX.revenueListener = { r in
            send(["k": "revenue", "placement": r.placement, "format": r.format.wire, "valueMicros": r.valueMicros,
                  "currency": r.currency, "precision": r.precision, "source": r.source ?? NSNull()])
        }
    }

    // MARK: placements

    private static func fullScreen(_ placement: String, _ format: String) -> VfxFullScreen {
        switch VfxFormat.fromWire(format) {
        case .rewarded: return VisionForgeX.rewarded(placement)
        case .rewardedInterstitial: return VisionForgeX.rewardedInterstitial(placement)
        case .appOpen: return VisionForgeX.appOpen(placement)
        default: return VisionForgeX.interstitial(placement)
        }
    }

    @objc(isReady)
    public static func isReady() -> Bool { VisionForgeX.isReady }

    @objc(fullScreenReady:format:)
    public static func fullScreenReady(_ placement: String, format: String) -> Bool { fullScreen(placement, format).isReady() }

    @objc(load:format:)
    public static func load(_ placement: String, format: String) { fullScreen(placement, format).load() }

    @objc(show:placement:format:)
    public static func show(_ id: Int, placement: String, format: String) {
        var done = false
        func finish(_ event: String, _ reason: String?) {
            if done { return }
            done = true
            var m: [String: Any] = ["k": "show", "id": id, "e": event]
            if let r = reason { m["r"] = r }
            send(m)
        }
        fullScreen(placement, format).show(from: top(), callbacks: VfxShowCallbacks(
            onShown: { send(["k": "show", "id": id, "e": "shown"]) },
            onClicked: { send(["k": "show", "id": id, "e": "clicked"]) },
            onDismissed: { finish("dismissed", nil) },
            onNotShown: { reason in finish("notShown", reason) },
            onReward: { type, amount in send(["k": "show", "id": id, "e": "reward", "t": type, "a": amount]) }))
    }

    @objc(preloadAll)
    public static func preloadAll() { VisionForgeX.preloadAll() }

    @objc(setKeyValue:value:)
    public static func setKeyValue(_ key: String, value: String?) { VisionForgeX.setKeyValue(key, value) }

    // MARK: consent and tools

    @objc(isPrivacyOptionsRequired)
    public static func isPrivacyOptionsRequired() -> Bool { VisionForgeX.isPrivacyOptionsRequired }

    @objc(showPrivacyOptions:)
    public static func showPrivacyOptions(_ id: Int) {
        guard let vc = top() else { send(["k": "consent", "id": id, "error": "no screen"]); return }
        VisionForgeX.showPrivacyOptions(from: vc) { err in send(["k": "consent", "id": id, "error": err ?? NSNull()]) }
    }

    @objc(setMuted:)
    public static func setMuted(_ muted: Bool) { VisionForgeX.setMuted(muted) }

    @objc(openAdInspector)
    public static func openAdInspector() { if let vc = top() { VisionForgeX.openAdInspector(from: vc) } }

    @objc(settingsSource)
    public static func settingsSource() -> String { VisionForgeX.settingsSource() }

    @objc(flush)
    public static func flush() { VisionForgeX.flush() }

    @objc(nativeVersion)
    public static func nativeVersion() -> String { VisionForgeX.version }

    // MARK: banners

    @objc(bannerCreate:placement:position:)
    public static func bannerCreate(_ id: Int, placement: String, position: Int) {
        banners[id] = VfxUnityBanner(id: id, placement: placement, top: position == 1)
    }

    @objc(bannerSetVisible:visible:)
    public static func bannerSetVisible(_ id: Int, visible: Bool) { banners[id]?.setVisible(visible) }

    @objc(bannerDestroy:)
    public static func bannerDestroy(_ id: Int) { banners.removeValue(forKey: id)?.destroy() }
}

/// One banner: the SDK's own VfxBannerView in a full-width strip on Unity's view, at the top or the bottom of the safe
/// area. It tells C# how much of the screen's edge it covers, in pixels.
final class VfxUnityBanner {
    let id: Int
    let placement: String
    let top: Bool
    private var strip: VfxUnityStrip?
    private var lastHeight = -1
    private var loaded = false

    init(id: Int, placement: String, top: Bool) { self.id = id; self.placement = placement; self.top = top }

    private func attach() {
        if strip != nil { return }
        guard let root = VfxUnityBridgeSwift.rootView() else { return }
        let s = VfxUnityStrip()
        s.onLayout = { [weak self] in self?.report() }
        s.translatesAutoresizingMaskIntoConstraints = false
        let banner = VfxBannerView(placement: placement)
        banner.translatesAutoresizingMaskIntoConstraints = false
        s.addSubview(banner)
        root.addSubview(s)
        let g = root.safeAreaLayoutGuide
        NSLayoutConstraint.activate([
            s.leadingAnchor.constraint(equalTo: g.leadingAnchor),
            s.trailingAnchor.constraint(equalTo: g.trailingAnchor),
            top ? s.topAnchor.constraint(equalTo: g.topAnchor) : s.bottomAnchor.constraint(equalTo: g.bottomAnchor),
            banner.leadingAnchor.constraint(equalTo: s.leadingAnchor),
            banner.trailingAnchor.constraint(equalTo: s.trailingAnchor),
            banner.topAnchor.constraint(equalTo: s.topAnchor),
            banner.bottomAnchor.constraint(equalTo: s.bottomAnchor),
        ])
        s.banner = banner
        strip = s
    }

    func setVisible(_ visible: Bool) {
        if visible { attach() }
        strip?.isHidden = !visible
        report()
    }

    /// An ad filled it: from now on it covers part of the screen.
    func markLoaded() {
        loaded = true
        strip?.setNeedsLayout()
        report()
    }

    func report() {
        var h = 0
        if loaded, let s = strip, !s.isHidden, let root = s.superview, let b = s.banner, b.bounds.height > 0 {
            let edge = top ? s.frame.maxY : root.bounds.height - s.frame.minY
            h = Int((max(0, edge) * UIScreen.main.scale).rounded())
        }
        if h == lastHeight { return }
        lastHeight = h
        VfxUnityBridgeSwift.send(["k": "banner", "id": id, "h": h])
    }

    func destroy() {
        strip?.removeFromSuperview()
        strip = nil
    }
}

/// The strip a banner sits in: says when its layout changed.
final class VfxUnityStrip: UIView {
    var onLayout: (() -> Void)?
    weak var banner: VfxBannerView?
    override func layoutSubviews() {
        super.layoutSubviews()
        onLayout?()
    }
}
