# -*- coding: utf-8 -*-
"""从 icon2.png（自带 alpha 的 PNG）生成 Splash.png 与 icon.ico，全程保留原透明度。"""
import os

from PIL import Image

IMG = os.path.join(os.path.dirname(os.path.dirname(os.path.abspath(__file__))), "XingYue", "Images")

src = Image.open(os.path.join(IMG, "icon2.png")).convert("RGBA")
print("源图", src.size, "alpha", src.getchannel("A").getextrema())

# 1) 按 alpha 裁掉四周空白（不做任何白底转透明，原图已经是透明底）
logo = src.crop(src.getchannel("A").getbbox())
print("裁切后", logo.size)

# 2) Splash.png：透明底，等比放大到长边约 347（原 520 缩小三分之一）
scale = 347 / max(logo.size)
splash = logo.resize((round(logo.width * scale), round(logo.height * scale)), Image.LANCZOS)
splash.save(os.path.join(IMG, "Splash.png"))
print("Splash.png", splash.size)

# 3) icon.ico：补成正方形（透明底），大尺寸在前
side = max(logo.size)
square = Image.new("RGBA", (side, side), (0, 0, 0, 0))
square.paste(logo, ((side - logo.width) // 2, (side - logo.height) // 2))
sizes = [256, 128, 64, 48, 32, 24, 16]
square.save(os.path.join(IMG, "icon.ico"), format="ICO", sizes=[(s, s) for s in sizes])
print("icon.ico 尺寸顺序", sizes)

# 4) 预览用：把 256 帧抠出来放灰底上，方便肉眼检查有没有破洞
prev = Image.open(os.path.join(IMG, "icon.ico"))
prev.size = (256, 256)
bg = Image.new("RGBA", (256, 256), (120, 120, 130, 255))
bg.alpha_composite(prev.convert("RGBA"), (0, 0))
bg.save(os.path.join(os.path.dirname(os.path.abspath(__file__)), "_icon256.png"))
