import os
from PIL import Image, ImageDraw, ImageFont

def create_medtrx_icon(output_path):
    os.makedirs(os.path.dirname(output_path), exist_ok=True)
    size = 512
    # Create high-res base image with transparency
    img = Image.new("RGBA", (size, size), (0, 0, 0, 0))
    draw = ImageDraw.Draw(img)

    # 1. Background Rounded Rectangle / Medical Shield
    # Hospital cyan/teal to deep medical blue gradient
    margin = 24
    corner_radius = 90
    
    # Draw a smooth rounded rectangle
    draw.rounded_rectangle(
        [(margin, margin), (size - margin, size - margin)],
        radius=corner_radius,
        fill=(14, 116, 144, 255) # Deep cyan-blue (#0e7490)
    )
    
    # Inner border for a polished glassmorphic feel
    inner_margin = margin + 12
    draw.rounded_rectangle(
        [(inner_margin, inner_margin), (size - inner_margin, size - inner_margin)],
        radius=corner_radius - 8,
        outline=(56, 189, 248, 200), # Light sky blue
        width=8
    )

    # 2. Medical Cross in Center (Clean White with Rounded Ends)
    center = size // 2
    cross_length = 260
    cross_thickness = 76
    c_radius = 24

    # Vertical bar
    v_top = center - cross_length // 2
    v_bottom = center + cross_length // 2
    v_left = center - cross_thickness // 2
    v_right = center + cross_thickness // 2
    draw.rounded_rectangle(
        [(v_left, v_top), (v_right, v_bottom)],
        radius=c_radius,
        fill=(255, 255, 255, 255)
    )

    # Horizontal bar
    h_top = center - cross_thickness // 2
    h_bottom = center + cross_thickness // 2
    h_left = center - cross_length // 2
    h_right = center + cross_length // 2
    draw.rounded_rectangle(
        [(h_left, h_top), (h_right, h_bottom)],
        radius=c_radius,
        fill=(255, 255, 255, 255)
    )

    # 3. Center Medical Symbol Heartbeat Pulse Line (Emerald/Teal accent)
    pulse_color = (13, 148, 136, 255) # Deep teal #0d9488
    # Small inner heart/pulse line or M emblem
    draw.ellipse(
        [(center - 22, center - 22), (center + 22, center + 22)],
        fill=pulse_color
    )

    # Multi-resolution icon sizes for crisp Windows display
    icon_sizes = [(256, 256), (128, 128), (64, 64), (48, 48), (32, 32), (16, 16)]
    img.save(output_path, format="ICO", sizes=icon_sizes)
    print(f"Generated multi-res icon at: {output_path}")

if __name__ == "__main__":
    script_dir = os.path.dirname(os.path.abspath(__file__))
    project_dir = os.path.dirname(script_dir)
    ico_path = os.path.join(project_dir, "assets", "logo.ico")
    create_medtrx_icon(ico_path)
