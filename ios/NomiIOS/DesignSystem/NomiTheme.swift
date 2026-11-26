import SwiftUI

/// Jetons « Nomi Aura » : porcelaine chaude, verre lumineux et accents organiques.
/// Les couleurs restent dynamiques clair/sombre et les effets sont dégradables nativement.
public enum NomiTheme {
    public static let canvas = Color.dynamic(light: "F4EEE7", dark: "12100F")
    public static let surface = Color.dynamic(light: "FFFDFB", dark: "211E1C")
    public static let surface2 = Color.dynamic(light: "F0E8E0", dark: "292623")
    public static let ink = Color.dynamic(light: "1B1715", dark: "F4EEE7")
    public static let ink2 = Color.dynamic(light: "655D55", dark: "CCC3BA")
    public static let ink3 = Color.dynamic(light: "8A8177", dark: "948B81")
    public static let line = Color.dynamic(light: "E4DAD0", dark: "3A3531")
    public static let glassStroke = Color.dynamic(light: "FFFFFF", dark: "4A4340")

    public static let accent = Color.dynamic(light: "E9653F", dark: "F28A68")
    public static let accentSoft = Color.dynamic(light: "FBE0D5", dark: "472B24")
    public static let focus = Color.dynamic(light: "8267EA", dark: "A896FF")
    public static let focusSoft = Color.dynamic(light: "E9E3FB", dark: "302B4B")
    public static let code = Color.dynamic(light: "8267EA", dark: "A896FF")
    public static let codeSoft = Color.dynamic(light: "E9E3FB", dark: "302B4B")
    public static let course = Color.dynamic(light: "2C9B9A", dark: "6FCDCB")
    public static let courseSoft = Color.dynamic(light: "DDF0ED", dark: "1D3735")
    public static let success = Color.dynamic(light: "4F9F72", dark: "79C99A")
    public static let successSoft = Color.dynamic(light: "E0EFE3", dark: "23382D")
    public static let warning = Color.dynamic(light: "B7791F", dark: "E4B85F")
    public static let warningSoft = Color.dynamic(light: "F6E7C8", dark: "453620")

    public static let radiusSmall: CGFloat = 14
    public static let radius: CGFloat = 26
    public static let padding: CGFloat = 18

    public static let pressAnimation = Animation.spring(response: 0.24, dampingFraction: 0.62)
    public static let morphAnimation = Animation.spring(response: 0.44, dampingFraction: 0.78)

    public static var accentGradient: LinearGradient {
        LinearGradient(colors: [accent, Color.dynamic(light: "D9462F", dark: "E9653F")], startPoint: .topLeading, endPoint: .bottomTrailing)
    }

    public static var focusGradient: AngularGradient {
        AngularGradient(colors: [focus, course, warning, focus], center: .center)
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

public struct NomiAmbientBackground: View {
    public init() {}

    public var body: some View {
        ZStack {
            NomiTheme.canvas
            Circle()
                .fill(NomiTheme.accent.opacity(0.18))
                .frame(width: 300, height: 300)
                .blur(radius: 70)
                .offset(x: 150, y: -230)
            Circle()
                .fill(NomiTheme.focus.opacity(0.16))
                .frame(width: 250, height: 250)
                .blur(radius: 78)
                .offset(x: -180, y: 20)
            Circle()
                .fill(NomiTheme.course.opacity(0.14))
                .frame(width: 270, height: 270)
                .blur(radius: 82)
                .offset(x: 170, y: 330)
        }
        .ignoresSafeArea()
    }
}

public struct NomiCardModifier: ViewModifier {
    public var tint: Color

    public func body(content: Content) -> some View {
        content
            .padding(NomiTheme.padding)
            .background {
                ZStack {
                    RoundedRectangle(cornerRadius: NomiTheme.radius, style: .continuous)
                        .fill(.thinMaterial)
                    RoundedRectangle(cornerRadius: NomiTheme.radius, style: .continuous)
                        .fill(
                            LinearGradient(
                                colors: [NomiTheme.surface.opacity(0.76), NomiTheme.surface.opacity(0.42)],
                                startPoint: .topLeading,
                                endPoint: .bottomTrailing
                            )
                        )
                }
            }
            .overlay {
                RoundedRectangle(cornerRadius: NomiTheme.radius, style: .continuous)
                    .strokeBorder(NomiTheme.glassStroke.opacity(0.72), lineWidth: 1)
            }
            .overlay(alignment: .topLeading) {
                Capsule()
                    .fill(LinearGradient(colors: [tint.opacity(0.85), tint.opacity(0.25)], startPoint: .leading, endPoint: .trailing))
                    .frame(width: 54, height: 5)
                    .padding([.top, .leading], 11)
                    .opacity(0.75)
            }
            .shadow(color: Color.black.opacity(0.10), radius: 28, y: 14)
            .shadow(color: tint.opacity(0.07), radius: 20, y: 4)
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
        padding(.horizontal, 14)
            .padding(.vertical, 12)
            .background(NomiTheme.surface.opacity(0.56), in: RoundedRectangle(cornerRadius: 17, style: .continuous))
            .overlay {
                RoundedRectangle(cornerRadius: 17, style: .continuous)
                    .strokeBorder(NomiTheme.glassStroke.opacity(0.62), lineWidth: 1)
            }
    }
}

public struct NomiPrimaryButtonStyle: ButtonStyle {
    public init() {}

    public func makeBody(configuration: Configuration) -> some View {
        configuration.label
            .font(.headline.weight(.bold))
            .foregroundStyle(.white)
            .padding(.horizontal, 18)
            .padding(.vertical, 11)
            .frame(minHeight: 44)
            .background(NomiTheme.accentGradient, in: Capsule())
            .shadow(color: NomiTheme.accent.opacity(0.30), radius: 16, y: 8)
            .scaleEffect(configuration.isPressed ? 0.965 : 1)
            .animation(NomiTheme.pressAnimation, value: configuration.isPressed)
    }
}

public struct NomiSecondaryButtonStyle: ButtonStyle {
    public init() {}

    public func makeBody(configuration: Configuration) -> some View {
        configuration.label
            .font(.subheadline.weight(.bold))
            .foregroundStyle(NomiTheme.ink)
            .padding(.horizontal, 15)
            .padding(.vertical, 10)
            .frame(minHeight: 44)
            .background(NomiTheme.surface.opacity(0.54), in: Capsule())
            .overlay(Capsule().strokeBorder(NomiTheme.glassStroke.opacity(0.66), lineWidth: 1))
            .scaleEffect(configuration.isPressed ? 0.97 : 1)
            .animation(NomiTheme.pressAnimation, value: configuration.isPressed)
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
            .padding(.horizontal, 10)
            .padding(.vertical, 6)
            .background(tint.opacity(0.13), in: Capsule())
            .overlay(Capsule().strokeBorder(tint.opacity(0.16), lineWidth: 1))
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
                .font(.title2.weight(.bold))
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
                .font(.system(.largeTitle, design: .rounded).weight(.bold))
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

private struct NomiChildNavigationModifier: ViewModifier {
    @Environment(\.dismiss) private var dismiss
    @Environment(\.horizontalSizeClass) private var horizontalSizeClass

    func body(content: Content) -> some View {
        content
            .toolbar(.hidden, for: .navigationBar)
            .safeAreaInset(edge: .top, spacing: 0) {
                if horizontalSizeClass == .compact {
                    HStack {
                        Button {
                            dismiss()
                        } label: {
                            Label("Retour", systemImage: "chevron.left")
                                .font(.subheadline.weight(.bold))
                                .foregroundStyle(NomiTheme.ink)
                                .padding(.horizontal, 13)
                                .padding(.vertical, 9)
                                .background(NomiTheme.surface.opacity(0.62), in: Capsule())
                                .overlay(Capsule().strokeBorder(NomiTheme.glassStroke.opacity(0.58), lineWidth: 1))
                        }
                        .buttonStyle(.plain)
                        .accessibilityLabel("Retour")
                        Spacer()
                    }
                    .padding(.horizontal, 18)
                    .padding(.vertical, 6)
                    .background(.thinMaterial)
                }
            }
    }
}

public extension View {
    func nomiChildNavigation() -> some View {
        modifier(NomiChildNavigationModifier())
    }
}
