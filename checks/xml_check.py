"""Structural checks only; a Windows build is still required."""

import re
import xml.etree.ElementTree as ET
from pathlib import Path

root = Path(__file__).resolve().parents[1] / "Nomi.WinUI"
paths = [
    root / "App.xaml",
    root / "MainWindow.xaml",
    root / "app.manifest",
    root / "Nomi.WinUI.csproj",
]
for path in paths:
    ET.parse(path)
    print(f"XML valid: {path.name}")

source = (root / "MainWindow.xaml.cs").read_text()
markup = (root / "MainWindow.xaml").read_text()
for event, handler in re.findall(r'\b(Click|SelectionChanged|Invoked)="(\w+)"', markup):
    assert re.search(rf"\bvoid {handler}\(", source), f"Missing {event}: {handler}"
print("XAML event handlers resolve in source.")
