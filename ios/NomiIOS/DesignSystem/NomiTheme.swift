import SwiftUI

/// Jetons « Nomi Command » : porcelaine chaude, encre précise, accent corail rare,
/// surfaces documentaires opaques et matériaux natifs réservés à la navigation/aux contrôles.
public enum NomiTheme {
    public static let canvas = Color.dynamic(light: "F6F1E9", dark: "11100F")
    public static let surface = Color.dynamic(light: "FFFDFA", dark: "1C1A18")
    public static let surface2 = Color.dynamic(light: "EEE7DC", dark: "252220")
    public static let ink = Color.dynamic(light: "191512", dark: "F2EDE4")
    public static let ink2 = Color.dynamic(light: "716961", dark: "A99E90")
    public static let ink3 = Color.dynamic(light: "9B938A", dark: "777068")
    public static let line = Color.dynamic(light: "D9D3CA", dark: "343331")
    public static let glassStroke = Color.dynamic(light: "FFFFFF", dark: "4A4340")

    public static let accent = Color.dynamic(light: "D95F43", dark: "E47B5D")
    public static let accentSoft = Color.dynamic(light: "F1DDD6", dark: "3A211D")
    public static let focus = Color.dynamic(light: "D95F43", dark: "E47B5D")
    public static let focusSoft = Color.dynamic(light: "F1DDD6", dark: "3A211D")
    public static let code = Color.dynamic(light: "8267EA", dark: "A896FF")
    public static let codeSoft = Color.dynamic(light: "E9E3FB", dark: "302B4B")
    public static let course = Color.dynamic(light: "557C68", dark: "85AE9A")
    public static let courseSoft = Color.dynamic(light: "DDF0ED", dark: "1D3735")
    public static let success = Color.dynamic(light: "4F9F72", dark: "79C99A")
    public static let successSoft = Color.dynamic(light: "E0EFE3", dark: "23382D")
    public static let warning = Color.dynamic(light: "B7791F", dark: "E4B85F")
    public static let warningSoft = Color.dynamic(light: "F6E7C8", dark: "453620")

    public static let radiusSmall: CGFloat = 10
    public static let radius: CGFloat = 15
    public static let padding: CGFloat = 14

    public static let pressAnimation = Animation.spring(response: 0.24, dampingFraction: 0.62)
    public static let morphAnimation = Animation.spring(response: 0.44, dampingFraction: 0.78)

    public static var accentGradient: LinearGradient {
        LinearGradient(colors: [accent, Color.dynamic(light: "D9462F", dark: "D95F43")], startPoint: .topLeading, endPoint: .bottomTrailing)
    }

    public static var focusGradient: AngularGradient {
        AngularGradient(colors: [focus, course, warning, focus], center: .center)
    }
}


public enum NomiLiquidShape {
    case capsule
    case circle
    case rounded(CGFloat)
}

/// Progressive Liquid Glass surface. iOS 26 receives the native optical material;
/// earlier OS versions keep a dense translucent fallback with the same geometry.
public struct NomiLiquidGlassModifier: ViewModifier {
    public var shape: NomiLiquidShape
    public var tint: Color?
    public var tintStrength: Double
    public var interactive: Bool

    public init(shape: NomiLiquidShape, tint: Color? = nil, tintStrength: Double = 0.18, interactive: Bool = false) {
        self.shape = shape
        self.tint = tint
        self.tintStrength = tintStrength
        self.interactive = interactive
    }

    @available(iOS 26.0, *)
    private var glass: Glass {
        var glass = Glass.regular
        if let tint {
            glass = glass.tint(tint.opacity(tintStrength))
        }
        if interactive {
            glass = glass.interactive()
        }
        return glass
    }

    @ViewBuilder
    public func body(content: Content) -> some View {
        if #available(iOS 26.0, *) {
            switch shape {
            case .capsule:
                content.glassEffect(glass, in: Capsule())
            case .circle:
                content.glassEffect(glass, in: Circle())
            case .rounded(let radius):
                content.glassEffect(glass, in: .rect(cornerRadius: radius))
            }
        } else {
            switch shape {
            case .capsule:
                content
                    .background {
                        ZStack {
                            Capsule().fill(.thinMaterial)
                            if let tint {
                                Capsule().fill(tint.opacity(tintStrength))
                            }
                        }
                    }
                    .overlay(Capsule().strokeBorder(NomiTheme.glassStroke.opacity(0.62), lineWidth: 1))
            case .circle:
                content
                    .background {
                        ZStack {
                            Circle().fill(.thinMaterial)
                            if let tint {
                                Circle().fill(tint.opacity(tintStrength))
                            }
                        }
                    }
                    .overlay(Circle().strokeBorder(NomiTheme.glassStroke.opacity(0.62), lineWidth: 1))
            case .rounded(let radius):
                content
                    .background {
                        ZStack {
                            RoundedRectangle(cornerRadius: radius, style: .continuous).fill(.thinMaterial)
                            RoundedRectangle(cornerRadius: radius, style: .continuous).fill(NomiTheme.surface.opacity(0.46))
                            if let tint {
                                RoundedRectangle(cornerRadius: radius, style: .continuous).fill(tint.opacity(tintStrength))
                            }
                        }
                    }
                    .overlay(RoundedRectangle(cornerRadius: radius, style: .continuous).strokeBorder(NomiTheme.glassStroke.opacity(0.62), lineWidth: 1))
            }
        }
    }
}

public extension View {
    func nomiLiquidGlass(shape: NomiLiquidShape, tint: Color? = nil, tintStrength: Double = 0.18, interactive: Bool = false) -> some View {
        modifier(NomiLiquidGlassModifier(shape: shape, tint: tint, tintStrength: tintStrength, interactive: interactive))
    }
}

extension Color {
    static func dynamic(light: String, dark: String) -> Color {
        Color(UIColor { traits in
            traits.userInterfaceStyle == .dark ? UIColor(hex: dark) : UIColor(hex: light)
        })
    }
}

extension UIColor {
    convenience init(hex: String) {
        var cleaned = hex.trimmingCharacters(in: .whitespacesAndNewlines)
        cleaned = cleaned.replacingOccurrences(of: "#", with: "")
        var value: UInt64 = 0
        Scanner(string: cleaned).scanHexInt64(&value)
        let red = CGFloat((value >> 16) & 0xff) / 255
        let green = CGFloat((value >> 8) & 0xff) / 255
        let blue = CGFloat(value & 0xff) / 255
        self.init(red: red, green: green, blue: blue, alpha: 1)
    }
}


public extension View {
    /// Hides the system navigation bar while preserving the correct layout on iOS 17.
    /// `toolbarVisibility` is the supported API from iOS 18; the older modal API is
    /// kept only as a compatibility fallback because `.toolbar(.hidden:)` can leave
    /// the bar's vertical space behind in NavigationStack roots.
    @ViewBuilder
    func nomiNavigationBarHidden() -> some View {
        if #available(iOS 18.0, *) {
            self.toolbarVisibility(.hidden, for: .navigationBar)
        } else {
            self.navigationBarHidden(true)
        }
    }
}

public struct NomiAmbientBackground: View {
    public init() {}

    public var body: some View {
        NomiTheme.canvas
            .overlay(alignment: .top) {
                LinearGradient(colors: [NomiTheme.accent.opacity(0.035), .clear], startPoint: .top, endPoint: .center)
                    .frame(height: 280)
                    .allowsHitTesting(false)
            }
            .ignoresSafeArea()
    }
}

public struct NomiCardModifier: ViewModifier {
    public var tint: Color

    public func body(content: Content) -> some View {
        content
            .padding(NomiTheme.padding)
            .background(NomiTheme.surface.opacity(0.94), in: RoundedRectangle(cornerRadius: NomiTheme.radius, style: .continuous))
            .overlay(RoundedRectangle(cornerRadius: NomiTheme.radius, style: .continuous).strokeBorder(NomiTheme.line.opacity(0.88), lineWidth: 0.8))
            .shadow(color: Color.black.opacity(0.035), radius: 10, y: 4)
    }
}

extension View {
    public func nomiCard(tint: Color = NomiTheme.accent) -> some View {
        modifier(NomiCardModifier(tint: tint))
    }

    public func nomiScreenBackground() -> some View {
        background(NomiAmbientBackground())
    }

    public func nomiInput() -> some View {
        padding(.horizontal, 12)
            .padding(.vertical, 11)
            .background(NomiTheme.surface.opacity(0.94), in: RoundedRectangle(cornerRadius: 13, style: .continuous))
            .overlay {
                RoundedRectangle(cornerRadius: 13, style: .continuous)
                    .strokeBorder(NomiTheme.line.opacity(0.82), lineWidth: 0.8)
            }
    }
}

public struct NomiPrimaryButtonStyle: ButtonStyle {
    public init() {}
    public func makeBody(configuration: Configuration) -> some View {
        configuration.label
            .font(.subheadline.weight(.semibold))
            .foregroundStyle(NomiTheme.canvas)
            .padding(.horizontal, 14)
            .padding(.vertical, 9)
            .frame(minHeight: 44)
            .background(NomiTheme.ink, in: RoundedRectangle(cornerRadius: NomiTheme.radiusSmall, style: .continuous))
            .opacity(configuration.isPressed ? 0.76 : 1)
    }
}

public struct NomiSecondaryButtonStyle: ButtonStyle {
    public init() {}
    public func makeBody(configuration: Configuration) -> some View {
        configuration.label
            .font(.subheadline.weight(.semibold))
            .foregroundStyle(NomiTheme.ink)
            .padding(.horizontal, 13)
            .padding(.vertical, 9)
            .frame(minHeight: 44)
            .background(NomiTheme.surface2.opacity(0.52), in: RoundedRectangle(cornerRadius: NomiTheme.radiusSmall, style: .continuous))
            .overlay(RoundedRectangle(cornerRadius: NomiTheme.radiusSmall, style: .continuous).strokeBorder(NomiTheme.line.opacity(0.82), lineWidth: 0.8))
            .opacity(configuration.isPressed ? 0.68 : 1)
    }
}

extension ButtonStyle where Self == NomiPrimaryButtonStyle {
    public static var nomiPrimary: NomiPrimaryButtonStyle { NomiPrimaryButtonStyle() }
}

extension ButtonStyle where Self == NomiSecondaryButtonStyle {
    public static var nomiSecondary: NomiSecondaryButtonStyle { NomiSecondaryButtonStyle() }
}

public struct NomiProgressRing: View {
    public var progress: Double
    public var tint: Color
    public var size: CGFloat

    public init(progress: Double, tint: Color = NomiTheme.accent, size: CGFloat = 76) {
        self.progress = progress
        self.tint = tint
        self.size = size
    }

    public var body: some View {
        ZStack {
            Circle()
                .stroke(NomiTheme.ink.opacity(0.09), lineWidth: size * 0.10)
            Circle()
                .trim(from: 0, to: max(0.02, min(1, progress)))
                .stroke(
                    AngularGradient(colors: [tint.opacity(0.45), tint], center: .center),
                    style: StrokeStyle(lineWidth: size * 0.10, lineCap: .round)
                )
                .rotationEffect(.degrees(-90))
            Text("\(Int((max(0, min(1, progress)) * 100).rounded()))%")
                .font(.system(size: size * 0.20, weight: .bold, design: .rounded))
                .foregroundStyle(NomiTheme.ink)
        }
        .frame(width: size, height: size)
        .accessibilityElement(children: .ignore)
        .accessibilityLabel("Progression")
        .accessibilityValue("\(Int((max(0, min(1, progress)) * 100).rounded())) pour cent")
    }
}

public struct NomiBadge: View {
    public var text: String
    public var tint: Color

    public init(_ text: String, tint: Color = NomiTheme.accent) {
        self.text = text
        self.tint = tint
    }

    public var body: some View {
        Text(text)
            .font(.caption.weight(.bold))
            .foregroundStyle(tint)
            .padding(.horizontal, 9)
            .padding(.vertical, 5)
            .background(tint.opacity(0.11), in: Capsule())
            .overlay(Capsule().stroke(tint.opacity(0.18), lineWidth: 0.7))
            .accessibilityLabel(Text(text))
    }
}

public struct NomiSectionTitle: View {
    public var title: String
    public var subtitle: String?

    public init(_ title: String, subtitle: String? = nil) {
        self.title = title
        self.subtitle = subtitle
    }

    public var body: some View {
        VStack(alignment: .leading, spacing: 4) {
            Text(title)
                .font(.headline.weight(.semibold))
                .foregroundStyle(NomiTheme.ink)
            if let subtitle {
                Text(subtitle)
                    .font(.subheadline)
                    .foregroundStyle(NomiTheme.ink2)
            }
        }
        .frame(maxWidth: .infinity, alignment: .leading)
    }
}

public struct NomiScreenTitle: View {
    public var kicker: String?
    public var title: String
    public var subtitle: String?

    public init(_ title: String, kicker: String? = nil, subtitle: String? = nil) {
        self.title = title
        self.kicker = kicker
        self.subtitle = subtitle
    }

    public var body: some View {
        VStack(alignment: .leading, spacing: 6) {
            if let kicker {
                Text(kicker)
                    .font(.subheadline.weight(.semibold))
                    .foregroundStyle(NomiTheme.ink2)
            }
            Text(title)
                .font(.system(.largeTitle, design: .serif).weight(.semibold))
                .foregroundStyle(NomiTheme.ink)
                .minimumScaleFactor(0.75)
            if let subtitle {
                Text(subtitle)
                    .font(.subheadline)
                    .foregroundStyle(NomiTheme.ink2)
            }
        }
        .frame(maxWidth: .infinity, alignment: .leading)
    }
}


private struct NomiTopEdgeProtection: View {
    @Environment(\.colorScheme) private var colorScheme
    @Environment(\.horizontalSizeClass) private var horizontalSizeClass
    @Environment(\.verticalSizeClass) private var verticalSizeClass

    private var height: CGFloat {
        if verticalSizeClass == .compact { return 22 }
        return horizontalSizeClass == .compact ? 62 : 28
    }

    var body: some View {
        ZStack {
            Rectangle()
                .fill(.regularMaterial)
            LinearGradient(
                colors: [
                    NomiTheme.canvas.opacity(colorScheme == .dark ? 0.50 : 0.62),
                    NomiTheme.canvas.opacity(colorScheme == .dark ? 0.16 : 0.12)
                ],
                startPoint: .top,
                endPoint: .bottom
            )
        }
        .frame(maxWidth: .infinity)
        .frame(height: height)
        .overlay(alignment: .bottom) {
            Rectangle()
                .fill(NomiTheme.ink.opacity(0.035))
                .frame(height: 0.7)
        }
        .ignoresSafeArea()
        .allowsHitTesting(false)
    }
}

public extension View {
    /// Keeps scrolled content optically behind a translucent top edge while the
    /// system navigation bar itself remains hidden for Nomi's custom headers.
    func nomiTopEdgeProtection() -> some View {
        overlay(alignment: .top) {
            NomiTopEdgeProtection()
        }
    }
}

private struct NomiChildNavigationModifier: ViewModifier {
    @Environment(\.dismiss) private var dismiss
    @Environment(\.horizontalSizeClass) private var horizontalSizeClass

    func body(content: Content) -> some View {
        if horizontalSizeClass == .compact {
            content
                .nomiNavigationBarHidden()
                .safeAreaInset(edge: .top, spacing: 0) {
                    HStack {
                        Button {
                            dismiss()
                        } label: {
                            Label("Retour", systemImage: "chevron.left")
                                .font(.subheadline.weight(.bold))
                                .foregroundStyle(NomiTheme.ink)
                                .padding(.horizontal, 13)
                                .padding(.vertical, 9)
                                .nomiLiquidGlass(shape: .capsule, interactive: true)
                        }
                        .buttonStyle(.plain)
                        .accessibilityLabel("Retour")
                        Spacer()
                    }
                    .padding(.horizontal, 18)
                    .padding(.vertical, 6)
                }
        } else {
            content
        }
    }
}

public extension View {
    func nomiChildNavigation() -> some View {
        modifier(NomiChildNavigationModifier())
    }
}
