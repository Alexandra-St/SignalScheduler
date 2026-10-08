import AppKit

// Code-rendered installer artwork: no external image or font dependencies.
let width = 600, height = 360
let bitmap = NSBitmapImageRep(bitmapDataPlanes: nil, pixelsWide: width * 2,
    pixelsHigh: height * 2, bitsPerSample: 8, samplesPerPixel: 4,
    hasAlpha: true, isPlanar: false, colorSpaceName: .deviceRGB,
    bytesPerRow: 0, bitsPerPixel: 0)!
bitmap.size = NSSize(width: width, height: height)
NSGraphicsContext.saveGraphicsState()
NSGraphicsContext.current = NSGraphicsContext(bitmapImageRep: bitmap)
NSGradient(starting: NSColor(srgbRed: 0.055, green: 0.11, blue: 0.15, alpha: 1),
           ending: NSColor(srgbRed: 0.025, green: 0.065, blue: 0.10, alpha: 1))!
    .draw(in: NSRect(x: 0, y: 0, width: width, height: height), angle: -30)
func text(_ value: String, y: CGFloat, size: CGFloat, color: NSColor, weight: NSFont.Weight) {
    let attributes: [NSAttributedString.Key: Any] = [.font: NSFont.systemFont(ofSize: size, weight: weight), .foregroundColor: color]
    let string = value as NSString
    let measured = string.size(withAttributes: attributes)
    string.draw(at: NSPoint(x: (600 - measured.width) / 2, y: y), withAttributes: attributes)
}
text("Signal Scheduler", y: 294, size: 26, color: .white, weight: .semibold)
let arrow = NSBezierPath()
arrow.move(to: NSPoint(x: 265, y: 190)); arrow.line(to: NSPoint(x: 335, y: 190))
arrow.move(to: NSPoint(x: 325, y: 200)); arrow.line(to: NSPoint(x: 335, y: 190)); arrow.line(to: NSPoint(x: 325, y: 180))
arrow.lineWidth = 3
arrow.lineCapStyle = .round; arrow.lineJoinStyle = .round
NSColor(srgbRed: 0.43, green: 0.64, blue: 0.84, alpha: 1).setStroke(); arrow.stroke()
text("Drag to Applications", y: 76, size: 18, color: .white, weight: .medium)
text("Then eject this disk and open the app from Applications.", y: 43, size: 12,
    color: NSColor(srgbRed: 0.7, green: 0.78, blue: 0.83, alpha: 1), weight: .regular)
NSGraphicsContext.restoreGraphicsState()
try bitmap.representation(using: .png, properties: [:])!.write(to: URL(fileURLWithPath: CommandLine.arguments[1]))
