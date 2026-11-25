import SwiftUI

/// Jetons adaptés de `design/tokens/tokens.json` de la version Windows,
/// puis traduits vers les couleurs dynamiques et matériaux natifs iOS.
public enum NomiTheme {
    public static let canvas = Color.dynamic(light: "ECE6DE", dark: "0F0E0D")
    public static let surface = Color.dynamic(light: "FFFFFF", dark: "1F1E1C")
    public static let surface2 = Color.dynamic(light: "F7F5F2", dark: "262523")
    public static let ink = Color.dynamic(light: "171512", dark: "F2EFEA")
    public static let ink2 = Color.dynamic(light: "4D4741", dark: "C9C3BB")
    public static let ink3 = Color.dynamic(light: "857D74", dark: "8D867D")
    public static let line = Color.dynamic(light: "E2DDD6", dark: "2F2D2A")
    public static let accent = Color.dynamic(light: "B4482B", dark: "E8825F")
    public static let accentSoft = Color.dynamic(light: "F6E6DF", dark: "3A2A23")
    public static let code = Color.dynamic(light: "3D6A98", dark: "7FA7D4")
    public static let codeSoft = Color.dynamic(light: "E2EAF3", dark: "1E2935")
    public static let course = Color.dynamic(light: "23787F", dark: "6CC3C9")
    public static let courseSoft = Color.dynamic(light: "DDEDEE", dark: "1C3133")
    public static let success = Color.dynamic(light: "2F7D4F", dark: "6FBF8A")
    public static let successSoft = Color.dynamic(light: "E1EAE0", dark: "22302A")
    public static let warning = Color.dynamic(light: "7F6136", dark: "DDBE8C")

    public static let radiusSmall: CGFloat = 12
    public static let radius: CGFloat = 18
    public static let padding: CGFloat = 16

    public static let pressAnimation = Animation.spring(response: 0.28, dampingFraction: 0.6)
    public static let morphAnimation = Animation.spring(response: 0.42, dampingFraction: 0.78)
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

public struct NomiCardModifier: ViewModifier {
    public var tint: Color

    public func body(content: Content) -> some View {
        content
            .padding(NomiTheme.padding)
            .background(NomiTheme.surface, in: RoundedRectangle(cornerRadius: NomiTheme.radius, style: .continuous))
            .overlay {
                RoundedRectangle(cornerRadius: NomiTheme.radius, style: .continuous)
                    .strokeBorder(NomiTheme.line, lineWidth: 1)
            }
            .overlay(alignment: .topLeading) {
                RoundedRectangle(cornerRadius: 3, style: .continuous)
                    .fill(tint.opacity(0.8))
                    .frame(width: 42, height: 6)
                    .padding([.top, .leading], 10)
            }
            .shadow(color: .black.opacity(0.06), radius: 18, y: 8)
    }
}

extension View {
    public func nomiCard(tint: Color = NomiTheme.accent) -> some View {
        modifier(NomiCardModifier(tint: tint))
    }

    public func nomiScreenBackground() -> some View {
        background(NomiTheme.canvas.ignoresSafeArea())
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
            .font(.caption.weight(.semibold))
            .foregroundStyle(tint)
            .padding(.horizontal, 9)
            .padding(.vertical, 5)
            .background(tint.opacity(0.12), in: Capsule())
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
