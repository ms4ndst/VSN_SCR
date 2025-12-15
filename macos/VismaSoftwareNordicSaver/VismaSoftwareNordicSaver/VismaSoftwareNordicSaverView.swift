import ScreenSaver
import AppKit
import QuartzCore
import CoreText

final class VismaSoftwareNordicSaverView: ScreenSaverView {
    // Settings keys and defaults
    private enum Keys {
        static let slideDuration = "slideDuration"
        static let transitionSeconds = "transitionSeconds"
        static let displayScale = "displayScale"
        static let showClock = "showClock"
        static let clockFormat = "clockFormat"
        static let clockFontName = "clockFontName"
        static let clockFontSize = "clockFontSize"
        static let shuffle = "shuffle"
        static let intensity = "intensity"
        static let animationStyle = "animationStyle"
    }

    private enum AnimationStyle: String, CaseIterable {
        case crossfade = "Crossfade"
        case kenBurns = "Ken Burns"
    }

    private let moduleName: String = Bundle(for: VismaSoftwareNordicSaverView.self).bundleIdentifier ?? "VismaSoftwareNordicSaver"
    private lazy var prefs: ScreenSaverDefaults = {
        let d = ScreenSaverDefaults(forModuleWithName: moduleName)!
        d.register(defaults: [
            Keys.slideDuration: 8.0,
            Keys.transitionSeconds: 2.0,
            Keys.displayScale: 0.9,
            Keys.showClock: true,
            Keys.clockFormat: "EEEE dd MMM yyyy HH:mm",
            Keys.clockFontName: "FiraMono Nerd Font",
            Keys.clockFontSize: 64.0,
            Keys.shuffle: false,
            Keys.intensity: 0.6,
            Keys.animationStyle: AnimationStyle.crossfade.rawValue
        ])
        return d
    }()

    // Live config (loaded from prefs)
    private var slideDuration: TimeInterval = 8.0
    private var transitionSeconds: TimeInterval = 2.0
    private var displayScale: CGFloat = 0.9 // 70–100% style margin
    private var showClock: Bool = true
    private var clockFormat: String = "EEEE dd MMM yyyy HH:mm"
    private var clockFontName: String = "FiraMono Nerd Font"
    private var clockFontSize: CGFloat = 64
    private var shuffle: Bool = false
    private var intensity: CGFloat = 0.6 // affects zoom/pan strength
    private var animationStyle: AnimationStyle = .crossfade

    // State
    private var images: [NSImage] = []
    private var idx: Int = 0
    private let imageLayer = CALayer()
    private let nextLayer = CALayer()
    private let clockLayer = CATextLayer()
    private var displayTimer: Timer?
    private var showClockNext = false
    private var prefsWindow: NSWindow?

    override init?(frame: NSRect, isPreview: Bool) {
        super.init(frame: frame, isPreview: isPreview)
        setup()
    }

    required init?(coder: NSCoder) {
        super.init(coder: coder)
        setup()
    }

    private func setup() {
        wantsLayer = true
        layer = CALayer()
        layer?.backgroundColor = NSColor.black.cgColor

        // Load latest prefs
        loadSettings()

        // Register any bundled fonts (e.g., FiraMono Nerd)
        registerBundledFonts()

        imageLayer.contentsGravity = .resizeAspect
        imageLayer.masksToBounds = true
        layer?.addSublayer(imageLayer)

        nextLayer.contentsGravity = .resizeAspect
        nextLayer.masksToBounds = true
        layer?.addSublayer(nextLayer)

        clockLayer.isWrapped = false
        clockLayer.alignmentMode = .center
        clockLayer.foregroundColor = NSColor.white.cgColor
        clockLayer.opacity = 0
        layer?.addSublayer(clockLayer)

        // Load bundled images from Resources/images
        loadImagesFromBundle()
    }

    private func loadSettings() {
        slideDuration = prefs.double(forKey: Keys.slideDuration)
        transitionSeconds = prefs.double(forKey: Keys.transitionSeconds)
        displayScale = prefs.object(forKey: Keys.displayScale) as? NSNumber != nil ? CGFloat(prefs.double(forKey: Keys.displayScale)) : 0.9
        showClock = prefs.bool(forKey: Keys.showClock)
        clockFormat = prefs.string(forKey: Keys.clockFormat) ?? "EEEE dd MMM yyyy HH:mm"
        clockFontName = prefs.string(forKey: Keys.clockFontName) ?? "FiraMono Nerd Font"
        let size = prefs.double(forKey: Keys.clockFontSize)
        clockFontSize = size > 0 ? CGFloat(size) : 64
        shuffle = prefs.bool(forKey: Keys.shuffle)
        let intensityVal = prefs.object(forKey: Keys.intensity) as? NSNumber
        intensity = intensityVal != nil ? CGFloat(intensityVal!.doubleValue) : 0.6
        if let s = prefs.string(forKey: Keys.animationStyle), let style = AnimationStyle(rawValue: s) {
            animationStyle = style
        } else {
            animationStyle = .crossfade
        }
    }

    private func loadImagesFromBundle() {
        guard let resURL = Bundle(for: Self.self).resourceURL else { return }
        let imagesURL = resURL.appendingPathComponent("images", isDirectory: true)
        if let items = try? FileManager.default.contentsOfDirectory(at: imagesURL, includingPropertiesForKeys: nil, options: [.skipsHiddenFiles]) {
            let supported = Set([".jpg", ".jpeg", ".png", ".bmp", ".gif"]) 
            var loaded = items.filter { supported.contains($0.pathExtension.lowercased().withDot) }
                .compactMap { NSImage(contentsOf: $0) }
            if shuffle {
                loaded.shuffle()
            }
            images = loaded
        }
        if images.isEmpty {
            // Fallback: add a placeholder to avoid crashes
            let ph = NSImage(size: NSSize(width: 800, height: 600))
            ph.lockFocus()
            NSColor.black.setFill()
            NSBezierPath(rect: NSRect(x: 0, y: 0, width: 800, height: 600)).fill()
            let attrs: [NSAttributedString.Key: Any] = [
                .foregroundColor: NSColor.white,
                .font: NSFont.systemFont(ofSize: 24)
            ]
            let str = NSString(string: "No images found in Resources/images")
            str.draw(at: NSPoint(x: 20, y: 280), withAttributes: attrs)
            ph.unlockFocus()
            images = [ph]
        }
    }

    override func startAnimation() {
        super.startAnimation()
        scheduleNext()
    }

    override func stopAnimation() {
        super.stopAnimation()
        displayTimer?.invalidate()
        displayTimer = nil
    }

    override func draw(_ rect: NSRect) {
        super.draw(rect)
    }

    override func animateOneFrame() {
        // CA-based animations; nothing in per-frame loop
    }

    override var isOpaque: Bool { true }

    override func resizeSubviews(withOldSize oldSize: NSSize) {
        super.resizeSubviews(withOldSize: oldSize)
        layoutLayers()
    }

    private func layoutLayers() {
        guard let root = layer else { return }
        let w = bounds.width * displayScale
        let h = bounds.height * displayScale
        let x = (bounds.width - w) / 2
        let y = (bounds.height - h) / 2
        let target = CGRect(x: x, y: y, width: w, height: h)
        CATransaction.begin()
        CATransaction.setDisableActions(true)
        imageLayer.frame = target
        nextLayer.frame = target
        clockLayer.frame = bounds
        CATransaction.commit()
    }

    private func scheduleNext() {
        displayTimer?.invalidate()
        displayTimer = Timer.scheduledTimer(withTimeInterval: slideDuration, repeats: false) { [weak self] _ in
            self?.advance()
        }
        RunLoop.main.add(displayTimer!, forMode: .common)
        advance() // show immediately
    }

    private func advance() {
        guard !images.isEmpty else { return }
        layoutLayers()

        if showClock && showClockNext {
            showClockScreen()
            showClockNext = false
            return
        }

        let current = images[idx % images.count]
        idx += 1
        switch animationStyle {
        case .crossfade:
            crossfade(to: current)
        case .kenBurns:
            kenBurns(to: current)
        }
        showClockNext = showClock
    }

    private func crossfade(to image: NSImage) {
        // Prepare next layer
        nextLayer.removeAllAnimations()
        imageLayer.removeAllAnimations()
        nextLayer.contents = image
        nextLayer.opacity = 0

        let fadeIn = CABasicAnimation(keyPath: "opacity")
        fadeIn.fromValue = 0
        fadeIn.toValue = 1
        fadeIn.duration = transitionSeconds
        fadeIn.timingFunction = CAMediaTimingFunction(name: .easeInEaseOut)
        nextLayer.add(fadeIn, forKey: "fadeIn")
        nextLayer.opacity = 1

        // Subtle Ken Burns zoom
        let zoom = CABasicAnimation(keyPath: "transform.scale")
        let baseFrom: CGFloat = 1.03 + 0.04 * intensity
        let baseTo: CGFloat = 1.10 + 0.12 * intensity
        zoom.fromValue = baseFrom
        zoom.toValue = baseTo
        zoom.duration = slideDuration
        zoom.timingFunction = CAMediaTimingFunction(name: .easeInEaseOut)
        nextLayer.add(zoom, forKey: "zoom")
        nextLayer.setAffineTransform(.identity)

        // Fade out current
        let out = CABasicAnimation(keyPath: "opacity")
        out.fromValue = imageLayer.opacity
        out.toValue = 0
        out.duration = transitionSeconds
        out.timingFunction = CAMediaTimingFunction(name: .easeInEaseOut)
        imageLayer.add(out, forKey: "fadeOut")
        imageLayer.opacity = 0

        // Swap layers after fade completes
        DispatchQueue.main.asyncAfter(deadline: .now() + transitionSeconds) { [weak self] in
            guard let self = self else { return }
            self.imageLayer.contents = self.nextLayer.contents
            self.imageLayer.opacity = 1
            self.nextLayer.opacity = 0
            self.nextLayer.removeAllAnimations()
        }
    }

    private func kenBurns(to image: NSImage) {
        nextLayer.removeAllAnimations()
        imageLayer.removeAllAnimations()
        nextLayer.contents = image
        nextLayer.opacity = 1

        // Pan + zoom
        let zoom = CABasicAnimation(keyPath: "transform.scale")
        let zFrom: CGFloat = 1.00 + 0.02 * intensity
        let zTo: CGFloat = 1.15 + 0.10 * intensity
        zoom.fromValue = zFrom
        zoom.toValue = zTo
        zoom.duration = slideDuration
        zoom.timingFunction = CAMediaTimingFunction(name: .easeInEaseOut)

        let pan = CABasicAnimation(keyPath: "position")
        let rect = nextLayer.frame
        let dx = rect.width * 0.05 * intensity
        let dy = rect.height * 0.05 * intensity
        pan.fromValue = NSValue(point: CGPoint(x: rect.midX - dx, y: rect.midY - dy))
        pan.toValue = NSValue(point: CGPoint(x: rect.midX + dx, y: rect.midY + dy))
        pan.duration = slideDuration
        pan.timingFunction = CAMediaTimingFunction(name: .easeInEaseOut)

        let group = CAAnimationGroup()
        group.animations = [zoom, pan]
        group.duration = slideDuration
        nextLayer.add(group, forKey: "kenburns")

        // Fade out previous layer
        let out = CABasicAnimation(keyPath: "opacity")
        out.fromValue = imageLayer.opacity
        out.toValue = 0
        out.duration = transitionSeconds
        out.timingFunction = CAMediaTimingFunction(name: .easeInEaseOut)
        imageLayer.add(out, forKey: "fadeOut")
        imageLayer.opacity = 0

        DispatchQueue.main.asyncAfter(deadline: .now() + transitionSeconds) { [weak self] in
            guard let self = self else { return }
            self.imageLayer.contents = self.nextLayer.contents
            self.imageLayer.opacity = 1
            self.nextLayer.opacity = 1
        }
    }

    private func showClockScreen() {
        clockLayer.string = formattedNow()
        clockLayer.font = resolvedClockFont()
        clockLayer.fontSize = clockFontSize
        clockLayer.opacity = 0

        // Keyframe opacity: fade in, hold, fade out across slideDuration
        let dIn = max(0.2, transitionSeconds * 0.5)
        let dOut = max(0.2, transitionSeconds * 0.5)
        let plateau = max(0, slideDuration - dIn - dOut)

        let group = CAAnimationGroup()
        group.duration = slideDuration
        group.timingFunction = CAMediaTimingFunction(name: .easeInEaseOut)

        let fadeIn = CABasicAnimation(keyPath: "opacity")
        fadeIn.fromValue = 0
        fadeIn.toValue = 1
        fadeIn.beginTime = 0
        fadeIn.duration = dIn

        let hold = CABasicAnimation(keyPath: "opacity")
        hold.fromValue = 1
        hold.toValue = 1
        hold.beginTime = dIn
        hold.duration = plateau

        let fadeOut = CABasicAnimation(keyPath: "opacity")
        fadeOut.fromValue = 1
        fadeOut.toValue = 0
        fadeOut.beginTime = dIn + plateau
        fadeOut.duration = dOut

        group.animations = [fadeIn, hold, fadeOut]
        clockLayer.add(group, forKey: "clockOpacity")
        clockLayer.opacity = 0
    }

    private func formattedNow() -> String {
        let f = DateFormatter()
        f.locale = Locale.current
        f.dateFormat = clockFormat
        return f.string(from: Date())
    }

    private func resolvedClockFont() -> CFTypeRef {
        if let f = NSFont(name: clockFontName, size: clockFontSize) {
            return f
        }
        return NSFont.monospacedSystemFont(ofSize: clockFontSize, weight: .regular)
    }

    // Try to register any fonts found in Resources/fonts
    private func registerBundledFonts() {
        guard let resURL = Bundle(for: Self.self).resourceURL else { return }
        let fontsURL = resURL.appendingPathComponent("fonts", isDirectory: true)
        guard let items = try? FileManager.default.contentsOfDirectory(at: fontsURL, includingPropertiesForKeys: nil, options: [.skipsHiddenFiles]) else { return }
        let exts = Set([".ttf", ".otf"]) 
        for url in items where exts.contains(url.pathExtension.lowercased().withDot) {
            CTFontManagerRegisterFontsForURL(url as CFURL, .process, nil)
        }
    }

    // MARK: - Preferences sheet
    override var hasConfigureSheet: Bool { true }
    override var configureSheet: NSWindow? {
        if prefsWindow == nil { prefsWindow = buildPrefsWindow() }
        return prefsWindow
    }

    private func buildPrefsWindow() -> NSWindow {
        let window = NSWindow(contentRect: NSRect(x: 0, y: 0, width: 520, height: 420),
                              styleMask: [.titled, .closable],
                              backing: .buffered,
                              defer: false)
        window.title = "VismaSoftwareNordic Preferences"

        let content = NSView()
        content.translatesAutoresizingMaskIntoConstraints = false
        window.contentView = content

        let stack = NSStackView()
        stack.orientation = .vertical
        stack.alignment = .leading
        stack.spacing = 12
        stack.edgeInsets = NSEdgeInsets(top: 16, left: 16, bottom: 16, right: 16)
        stack.translatesAutoresizingMaskIntoConstraints = false
        content.addSubview(stack)
        NSLayoutConstraint.activate([
            stack.leadingAnchor.constraint(equalTo: content.leadingAnchor),
            stack.trailingAnchor.constraint(equalTo: content.trailingAnchor),
            stack.topAnchor.constraint(equalTo: content.topAnchor),
            stack.bottomAnchor.constraint(equalTo: content.bottomAnchor)
        ])

        func labeledSlider(_ title: String, min: Double, max: Double, value: Double, step: Double = 0.1) -> (NSTextField, NSSlider, NSTextField) {
            let label = NSTextField(labelWithString: title)
            let slider = NSSlider(value: value, minValue: min, maxValue: max, target: nil, action: nil)
            slider.numberOfTickMarks = 0
            let valueLabel = NSTextField(labelWithString: String(format: "%.2f", value))
            valueLabel.alignment = .right
            let row = NSStackView(views: [label, slider, valueLabel])
            row.spacing = 8
            row.distribution = .fillProportionally
            label.widthAnchor.constraint(equalToConstant: 180).isActive = true
            valueLabel.widthAnchor.constraint(equalToConstant: 60).isActive = true
            stack.addArrangedSubview(row)
            slider.target = self
            slider.action = #selector(sliderChanged(_:))
            return (label, slider, valueLabel)
        }

        func checkbox(_ title: String, state: Bool) -> NSButton {
            let btn = NSButton(checkboxWithTitle: title, target: nil, action: nil)
            btn.state = state ? .on : .off
            btn.target = self
            btn.action = #selector(checkboxChanged(_:))
            stack.addArrangedSubview(btn)
            return btn
        }

        func labeledText(_ title: String, value: String) -> (NSTextField, NSTextField) {
            let label = NSTextField(labelWithString: title)
            let field = NSTextField(string: value)
            let row = NSStackView(views: [label, field])
            row.spacing = 8
            label.widthAnchor.constraint(equalToConstant: 180).isActive = true
            stack.addArrangedSubview(row)
            return (label, field)
        }

        func popup(_ title: String, items: [String], selected: String) -> (NSTextField, NSPopUpButton) {
            let label = NSTextField(labelWithString: title)
            let pop = NSPopUpButton()
            pop.addItems(withTitles: items)
            pop.selectItem(withTitle: selected)
            let row = NSStackView(views: [label, pop])
            row.spacing = 8
            label.widthAnchor.constraint(equalToConstant: 180).isActive = true
            stack.addArrangedSubview(row)
            return (label, pop)
        }

        // Controls
        let (_, slideSlider, slideVal) = labeledSlider("Slide Duration (s)", min: 2, max: 30, value: slideDuration)
        slideSlider.identifier = NSUserInterfaceItemIdentifier(Keys.slideDuration)

        let (_, transSlider, transVal) = labeledSlider("Transition (s)", min: 0, max: 5, value: transitionSeconds)
        transSlider.identifier = NSUserInterfaceItemIdentifier(Keys.transitionSeconds)

        let (_, scaleSlider, scaleVal) = labeledSlider("Display Scale (0.70–1.00)", min: 0.70, max: 1.00, value: displayScale)
        scaleSlider.identifier = NSUserInterfaceItemIdentifier(Keys.displayScale)

        let (_, intensitySlider, intensityVal) = labeledSlider("Intensity (0–1)", min: 0.0, max: 1.0, value: intensity)
        intensitySlider.identifier = NSUserInterfaceItemIdentifier(Keys.intensity)

        let shuffleBox = checkbox("Shuffle images", state: shuffle)
        shuffleBox.identifier = NSUserInterfaceItemIdentifier(Keys.shuffle)

        let clockBox = checkbox("Show clock between slides", state: showClock)
        clockBox.identifier = NSUserInterfaceItemIdentifier(Keys.showClock)

        let (_, fontField) = labeledText("Clock Font Family", value: clockFontName)
        fontField.identifier = NSUserInterfaceItemIdentifier(Keys.clockFontName)

        let (_, fmtField) = labeledText("Clock Format", value: clockFormat)
        fmtField.identifier = NSUserInterfaceItemIdentifier(Keys.clockFormat)

        let (_, clockSizeSlider, clockSizeVal) = labeledSlider("Clock Font Size (pt)", min: 12, max: 160, value: Double(clockFontSize), step: 1)
        clockSizeSlider.identifier = NSUserInterfaceItemIdentifier(Keys.clockFontSize)

        let (_, stylePopup) = popup("Animation Style", items: AnimationStyle.allCases.map { $0.rawValue }, selected: animationStyle.rawValue)
        stylePopup.identifier = NSUserInterfaceItemIdentifier(Keys.animationStyle)

        let buttons = NSStackView()
        buttons.orientation = .horizontal
        buttons.alignment = .trailing
        buttons.spacing = 8
        let cancel = NSButton(title: "Cancel", target: self, action: #selector(cancelPrefs))
        let save = NSButton(title: "Save", target: self, action: #selector(savePrefs))
        buttons.addArrangedSubview(cancel)
        buttons.addArrangedSubview(save)
        stack.addArrangedSubview(NSView()) // spacer
        stack.addArrangedSubview(buttons)

        // Store value labels for live update
        slideVal.identifier = NSUserInterfaceItemIdentifier("val_" + Keys.slideDuration)
        transVal.identifier = NSUserInterfaceItemIdentifier("val_" + Keys.transitionSeconds)
        scaleVal.identifier = NSUserInterfaceItemIdentifier("val_" + Keys.displayScale)
        intensityVal.identifier = NSUserInterfaceItemIdentifier("val_" + Keys.intensity)
        clockSizeVal.identifier = NSUserInterfaceItemIdentifier("val_" + Keys.clockFontSize)

        return window
    }

    @objc private func sliderChanged(_ sender: NSSlider) {
        guard let id = sender.identifier?.rawValue else { return }
        let value = sender.doubleValue
        if let label = prefsWindow?.contentView?.viewWithIdentifier(NSUserInterfaceItemIdentifier("val_" + id)) as? NSTextField {
            if id == Keys.displayScale { label.stringValue = String(format: "%.2f", value) }
            else if id == Keys.clockFontSize { label.stringValue = String(format: "%.0f", value) }
            else { label.stringValue = String(format: "%.2f", value) }
        }
    }

    @objc private func checkboxChanged(_ sender: NSButton) { /* no-op live */ }

    @objc private func cancelPrefs() {
        if let w = prefsWindow { NSApp.endSheet(w) }
    }

    @objc private func savePrefs() {
        guard let content = prefsWindow?.contentView else { return }
        func find<T: NSView>(_ id: String, as type: T.Type) -> T? {
            content.viewWithIdentifier(NSUserInterfaceItemIdentifier(id)) as? T
        }
        if let s: NSSlider = find(Keys.slideDuration, as: NSSlider.self) { prefs.set(s.doubleValue, forKey: Keys.slideDuration) }
        if let s: NSSlider = find(Keys.transitionSeconds, as: NSSlider.self) { prefs.set(s.doubleValue, forKey: Keys.transitionSeconds) }
        if let s: NSSlider = find(Keys.displayScale, as: NSSlider.self) { prefs.set(s.doubleValue, forKey: Keys.displayScale) }
        if let s: NSSlider = find(Keys.intensity, as: NSSlider.self) { prefs.set(s.doubleValue, forKey: Keys.intensity) }
        if let b: NSButton = find(Keys.shuffle, as: NSButton.self) { prefs.set(b.state == .on, forKey: Keys.shuffle) }
        if let b: NSButton = find(Keys.showClock, as: NSButton.self) { prefs.set(b.state == .on, forKey: Keys.showClock) }
        if let t: NSTextField = find(Keys.clockFontName, as: NSTextField.self) { prefs.set(t.stringValue, forKey: Keys.clockFontName) }
        if let t: NSTextField = find(Keys.clockFormat, as: NSTextField.self) { prefs.set(t.stringValue, forKey: Keys.clockFormat) }
        if let s: NSSlider = find(Keys.clockFontSize, as: NSSlider.self) { prefs.set(s.doubleValue, forKey: Keys.clockFontSize) }
        if let p: NSPopUpButton = find(Keys.animationStyle, as: NSPopUpButton.self), let title = p.selectedItem?.title { prefs.set(title, forKey: Keys.animationStyle) }
        prefs.synchronize()

        // Apply and restart loop
        loadSettings()
        layoutLayers()
        scheduleNext()
        if let w = prefsWindow { NSApp.endSheet(w) }
    }
}

private extension String {
    var withDot: String { starts(with: ".") ? self : "." + self }
}

private extension NSView {
    func viewWithIdentifier(_ identifier: NSUserInterfaceItemIdentifier) -> NSView? {
        if self.identifier == identifier { return self }
        for sub in subviews {
            if let v = sub.viewWithIdentifier(identifier) { return v }
        }
        return nil
    }
}
