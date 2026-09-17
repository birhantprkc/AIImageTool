# ZeroVision

[![Build and Test](https://github.com/kzxl/ZeroVision/actions/workflows/build-test.yml/badge.svg)](https://github.com/kzxl/ZeroVision/actions/workflows/build-test.yml)
[![License](https://img.shields.io/github/license/kzxl/ZeroVision)](LICENSE)
![Platform](https://img.shields.io/badge/Platform-Windows%20x64-0078D6?style=flat-square&logo=windows)

> **ZeroVision** is a modern high-performance desktop workstation application (WPF, .NET 8) for RAW photo cataloging, non-destructive editing, color grading, and computer vision processing. It combines professional-grade **32-bit float Linear Light** develop pipelines with autonomous **DirectML ONNX AI engines** and ergonomic industry-standard professional keyboard workflows.

---

![ZeroVision Preview](screenshots/preview.png)

---

## Key Features

### 1. Ergonomic Workstation Workflows
- **Navigator Panel**: Fixed in the left panel dock. Features ZeroUI `SegmentedControl` (`FIT`, `FILL`, `1:1`, `2:1`), live viewport rectangle tracking, and bi-directional real-time canvas pan/zoom.
- **Library Metadata Drill-Down Bar**: 4-column drill-down filter bar (**Date (Year)**, **Camera**, **Lens**, **ISO**) with item counts and in-memory EXIF caching.
- **Interactive Masking Gizmos**: Direct on-canvas dragging for **Linear Gradient** (start/center/end bars + rotation axis) and **Radial Mask** (center handle + 4 perimeter dimension handles + feather ring).
- **Virtual Copies (`Ctrl+'`)**: Instant zero-byte branching with isolated edit histories and shared decoded proxies.
- **Selective Copy Settings (`Ctrl+Shift+C`)**: Comprehensive checklist dialog across 16 processing modules with Check All/None and persistent user selections.
- **Auto-Advance Culling (`Caps Lock`)**: Hands-free keyboard workflow that automatically advances to the next photo when assigning ratings (0–5), flags (P/X/U), or color labels (6–9).
- **Panel Visibility Toggles**:
  - `Tab`: Toggle side panels for an expanded canvas.
  - `Shift+Tab`: Pure Full Canvas mode (hides titlebar, status bar, and filmstrip).
  - `L`: 3-stage Lights Out background dimming (100% -> Dim 80% -> Full Black 100%).
- **Interactive Histogram Tone Zones**: Real-time hover zone highlighting and direct drag tuning for *Blacks*, *Shadows*, *Exposure*, *Highlights*, and *Whites*.

### 2. Non-Destructive Develop Pipeline (Linear Light)
All image adjustments are applied in **linear light float RGBA** color space through an intelligent DAG pipeline, preserving maximum highlight/shadow detail without color banding.

#### Tone & Lighting
- **Exposure, Contrast, Highlights, Shadows, Whites, Blacks**
- **Tone Curve & Parametric Curve** with drag-point editing and built-in presets (*Linear, Medium, Strong, Faded*)
- **Filmic, Filmic RGB, Sigmoid, Tone Equalizer, Dehaze**
- **Levels** (per-channel editing, **Auto Levels**, **Auto Color** for color cast removal)
- Interactive **Histogram** with direct tone adjustment by dragging

#### Color & Grading
- **White Balance** (Kelvin slider, **Auto WB**, **eyedropper** tool, standard illuminant presets)
- **HSL 8-channel mixer** + **Targeted Adjustment Tool (TAT)** (click-drag directly on image for HSL adjustment)
- **Color Balance RGB 4-way** and **Color Grading** wheels
- **Split Toning, Channel Mixer, Selective Color, Color Unify, Velvia, Color Contrast (Lab)**
- **3D LUT (.cube)** support and input color profiles (**sRGB, AdobeRGB, Rec2020, Display P3**)
- **Black & White**: Deep channel mixing with classic color filters and toning
- **Film Negative**: Professional film scan processing and inversion

#### Detail & Sharpness
- **Sharpen** (radius + intelligent edge **Masking**)
- **Noise Reduction** (Luminance, Color, Chroma)
- **Diffuse-or-sharpen (PDE)** filter, **Hot Pixel** removal, **CA Correct**, **Defringe**
- **Texture, Clarity, Grain** (monochrome and chromatic film grain)

#### Geometry & Layout
- **Crop** with free or standard aspect ratios (1:1, 16:9, 4:3...) and composition guide overlays
- **Straighten, Rotate, Flip** with EXIF-based auto-rotation
- **Perspective / Upright** correction, **Liquify/Warp** with intuitive handle-based editing
- **Lens Correction**: Automatic distortion and vignetting correction via **lensfun** database (EXIF-based) or manual adjustment

#### Local Adjustments & Masking
- Mask types: **Linear Gradient, Radial, Brush, Polygon, Path, Luminance Range, Color Range, Parametric, AI Subject & AI Sky**
- Combine multiple masks with opacity and blend modes
- Copy/duplicate masks easily; each mask has its own full set of adjustment sliders
- `O` key cycles **Mask Overlay Color** (Red/Green/Blue/White/Black) for better brush visibility

#### Presets & Style Management
- Save edits as **Styles** for batch application
- **Hover Preset Preview**: Hover over styles in the left panel for instant preview with live indicator badge (`👁 PREVIEW: [Name]`) without dirtying edit history
- **Import standard XMP presets (.xmp)**, auto-write XMP sidecar files
- **Named Snapshots**: Save multiple edit versions within the same image for quick comparison

---

### 3. Autonomous AI Inference (DirectML / ONNX)
- **Super-Resolution Upscaling**: 2x and 4x image enlargement powered by Real-ESRGAN with DirectML GPU acceleration.
- **Face Restoration**: Deep portrait reconstruction with GFPGAN / CodeFormer.
- **Vision Auto-Tagging**: Multi-label semantic tagging using MobileNet / ResNet models.

---

## Keyboard Shortcuts

| Category | Shortcut | Action |
|:---|:---|:---|
| **Navigation & Zoom** | `Left` / `Right` | Previous / Next image |
| | `E` / `G` / `C` / `F` | Single / Grid / Cull / Full view |
| | `Z` | Toggle zoom fit / 100% |
| | `+` / `-` | Step zoom in / out |
| | `Space` + drag | Pan canvas |
| **Workspace Layout** | `Tab` | Toggle side panels |
| | `Shift+Tab` | Pure Full Canvas mode |
| | `L` | Lights Out (3 stages: Normal -> Dim 80% -> Full Black) |
| **Metadata Filter** | `\` | Toggle 4-Column Metadata Filter Bar (in Grid view) |
| **Comparison** | `Y` | Side-by-side Before / After comparison |
| | `\` (hold) | Instant toggle view original image (in Single view) |
| **Culling & Rating** | `Caps Lock` | Toggle Auto-Advance culling mode |
| | `0` – `5` | Set star rating |
| | `P` / `X` / `U` | Pick / Reject / Unflag |
| | `6` – `9` | Color labels (Red / Yellow / Green / Blue / Purple) |
| **Virtual Copies** | `Ctrl+'` | Create Virtual Copy |
| | `Delete` | Remove selected Virtual Copy |
| **Crop Tool** | `R` | Toggle crop mode |
| | `X` | Swap crop orientation (Landscape ↔ Portrait) |
| | `O` | Cycle crop composition guides (Rule of Thirds, Golden Ratio, Diagonals...) |
| | `Enter` / `Esc` | Commit / Dismiss crop |
| | `[` / `]` | Rotate 90° counter-clockwise / clockwise |
| **Develop & Styles** | `D` | Switch to Develop tab |
| | `M` | Switch to Develop and focus Local Masking |
| | `Ctrl+Shift+C` | Open Selective Copy Settings dialog |
| | `Ctrl+V` | Paste copied develop settings |
| | `Ctrl+Z` / `Ctrl+Y` | Undo / Redo |
| | `Ctrl+Shift+E` | Quick Export |
| **Clipping & Focus** | `J` | Toggle shadow & highlight clipping warnings |
| | `K` | Toggle focus peaking overlay |
| | `Alt` + drag slider | Preview clipping threshold interactively |

---

## Installation & Running

Releases provide two deployment options:
- **Full Package (`ZeroVision_Full_Win_x64.zip`)**: Fully self-contained. Extract and launch `ZeroVision.exe`.
- **Lite Package (`ZeroVision_Lite_Win_x64.zip`)**: Lightweight framework-dependent build for systems with .NET 8 Runtime installed.

---

## Developer Guide

### Prerequisites
1. Windows 10 (build 1809+) or Windows 11
2. [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
3. Visual Studio 2022 / Rider / VS Code

### Build and Test
```powershell
# Build entire solution
dotnet build ZeroVision.slnx -c Release

# Run automated tests (820 tests)
dotnet test ZeroVision.Tests/ZeroVision.Tests.csproj -c Release
```

### Packaging
```powershell
# Build and package both Full and Lite single-file executables
powershell -ExecutionPolicy Bypass -File .\publish.ps1 -Mode All
```

---

## License

Licensed under the **MIT License**. Part of the sovereign **ZeroUniverse** ecosystem.
