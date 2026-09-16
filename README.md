# 🌌 ZeroVision — High-Performance RAW & Image Processing Workstation

[![Type: Desktop Application](https://img.shields.io/badge/Type-Desktop%20Application-007ACC?style=flat-square&logo=windows)](https://github.com/kzxl/ZeroVision)
[![Version](https://img.shields.io/badge/version-v1.1.0-blue.svg?style=flat-square)](https://github.com/kzxl/ZeroVision)
[![Ecosystem](https://img.shields.io/badge/Ecosystem-ZeroUniverse-8A2BE2?style=flat-square)](https://github.com/kzxl/ZeroUniverse)
[![Platform](https://img.shields.io/badge/Platform-Windows%20x64-0078D6?style=flat-square&logo=windows)](https://dotnet.microsoft.com/)
[![Distribution: Standalone Single-File](https://img.shields.io/badge/Distribution-Standalone%20Single--File-2ea44f?style=flat-square)](https://github.com/kzxl/ZeroVision)
[![Unit Tests](https://img.shields.io/badge/tests-878%20passed%20(100%25)-brightgreen.svg?style=flat-square)](#-quick-start-for-developers)
[![Build and Test](https://github.com/kzxl/ZeroVision/actions/workflows/build-test.yml/badge.svg)](https://github.com/kzxl/ZeroVision/actions/workflows/build-test.yml)
[![License: Apache 2.0](https://img.shields.io/badge/License-Apache_2.0-blue.svg?style=flat-square)](LICENSE)

> **ZeroVision** is a sovereign professional desktop workstation application (WPF, .NET 8) for high-fidelity image cataloging, RAW development, color grading, multi-frame computational photography, and computer vision acceleration. Part of the **ZeroUniverse** industrial computing ecosystem, it combines a **non-destructive 32-bit float Linear Light** develop pipeline with native **DirectML ONNX AI engines** and ergonomic industry-standard professional keyboard and workflow paradigms.

---

![ZeroVision Preview](screenshots/preview.png)

---

## 🌟 Key Capabilities

### 1. Professional Workstation Workflows & ZeroUI Controls
- **Left Dock Navigator Panel**: Fixed top widget with ZeroUI `SegmentedControl` (`FIT` | `FILL` | `1:1` | `2:1`), live viewport rectangle tracking, and bi-directional real-time canvas pan/zoom.
- **Library Metadata Drill-Down**: 4-column filter bar (**Date (Year)** | **Camera** | **Lens** | **ISO**) with aggregate image counts and high-speed in-memory EXIF caching.
- **Interactive Masking Gizmos**: Direct canvas drag manipulation for **Linear Gradient** (start/center/end bars + rotation axis) and **Radial Mask** (center handle + 4 perimeter dimension handles + feather ring).
- **Virtual Copies (`Ctrl+'`)**: Instant zero-byte branching with isolated edit histories and shared decoded proxies.
- **Selective Copy Settings (`Ctrl+Shift+C`)**: Granular checklist dialog across 16 processing modules with Check All/None and persistent state.
- **Auto-Advance Culling (`Caps Lock`)**: Hands-free keyboard workflow that automatically advances to the next photo when assigning ratings (0–5), flags (P/X/U), or color labels (6–9).
- **Panel Toggles & Lights Out**:
  - `Tab`: Toggle side panels.
  - `Shift+Tab`: Full clean canvas mode (hides titlebar, status bar, and filmstrip).
  - `L`: 3-stage Lights Out background dimming (100% -> Dim 80% -> Full Black 100%).
- **Interactive Histogram Tone Zones**: Real-time hover zone highlighting and direct drag tuning for *Blacks*, *Shadows*, *Exposure*, *Highlights*, and *Whites*.

### 2. Non-Destructive Develop Pipeline (32-bit Linear Light)
- **Exposure & Tone**: Exposure, Contrast, Highlights, Shadows, Whites, Blacks, Tone Curve (RGB & individual channels), Parametric Curves, Filmic RGB, Sigmoid, Dehaze, Auto Levels.
- **Computational Photography & Multi-Frame Fusion**:
  - **Mertens Exposure Fusion (`ExposureFusionService`)**: Blends bracketed exposure sequences into high dynamic range imagery without tone-mapping halos using Contrast, Saturation, and Well-Exposedness weighting.
  - **Multi-Band Focus Stacking**: Laplacian pyramid frequency-band fusion with local energy computation for deep depth-of-field synthesis.
  - **À-Trous Wavelet Denoising (`WaveletDenoiseOp`)**: Multi-scale $B_3$-spline wavelet decomposition with soft/hard thresholding for edge-preserving noise suppression.
  - **Fast Marching Inpainting (`AiInpaintOp`)**: Telea fast marching algorithm for seamless defect, blemish, and watermark removal.
  - **Edge-Preserving Smoothing & Noise Control**: $O(1)$ Fast Guided Filter (`GuidedFilterOp`) and Directed Median Filter (`DirectedMedianOp`) for salt-and-pepper noise suppression.
  - **Minkowski Gray-Edge AWB (`AutoWhiteBalance`)**: High-order derivative illuminant estimation for accurate color temperature recovery.
- **Color & Color Grading**: White Balance (Kelvin & Eyedropper), 8-Channel HSL Mixer + Targeted Adjustment Tool (TAT direct-canvas drag), 4-Way Color Balance, Split Toning, 3D LUT (.cube), Film Scan Negative Inverter.
- **Detail & Corrections**: Radius-based Sharpening with edge Masking, Multi-stage Denoise (Luminance, Color, Chroma), Lensfun auto-correction (distortion & vignetting), Perspective Upright, Liquify Warp.
- **Local Adjustments**: Linear Gradient, Radial, Brush, Polygon, Path, Luminance Range, Color Range, Parametric, and AI Subject/Sky masks.

### 3. Autonomous AI & Vision Pipeline
ZeroVision runs heavy AI workloads out-of-process via DirectML and ONNX Runtime:

| AI Module | Architecture | Capability |
| :--- | :--- | :--- |
| **Upscaler** | Real-ESRGAN / AuraSR | 2x / 4x super-resolution with GPU acceleration |
| **Face Restorer** | GFPGAN / CodeFormer | Portrait reconstruction and facial detail restoration |
| **Vision Tagger** | MobileNet / ResNet ONNX | Automated multi-label semantic tagging and classification |

---

## 🏗 Solution Structure

```
ZeroVision/
├── ZeroVision.Core/          # Domain contracts, pipeline abstractions, metadata & catalog models
├── ZeroVision.Imaging/       # 32-bit float linear light image processing kernels & masks
├── ZeroVision.Shared/        # Services (Catalog SQLite/LiteSql, EXIF parser, Stacking, Fusion, Export)
├── ZeroVision.Host/          # Main WPF desktop application (CenterPreview, Navigator, DevelopPanel)
├── ZeroVision.Plugins.*/     # Autonomous DirectML ONNX plugins (Upscaler, FaceRestorer, VisionTagger)
├── ZeroVision.Tests/         # 878 automated unit & integration tests (100% pass)
└── publish.ps1               # Dual-mode release packager (Full Self-Contained & Lite)
```

---

## ⚡ Quick Start for Developers

### Prerequisites
- **Windows 10** (build 1809+) or **Windows 11** (64-bit)
- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- DirectX 12 compatible GPU (for DirectML AI features)

### Build and Test
```powershell
# Clone repository
git clone https://github.com/kzxl/ZeroVision.git
cd ZeroVision

# Build entire solution
dotnet build ZeroVision.slnx -c Release

# Run automated tests (878 tests)
dotnet test ZeroVision.Tests/ZeroVision.Tests.csproj -c Release
```

### Packaging & Release
```powershell
# Publish Dual-Mode (Full Self-Contained + Lite Framework-Dependent)
powershell -ExecutionPolicy Bypass -File .\publish.ps1 -Mode All
```

Binaries are generated in:
- `Publish/Full/ZeroVision.exe` (Single-file self-contained executable)
- `Publish/Lite/ZeroVision.exe` (Compact framework-dependent executable)

---

## 📜 Release History

| Version | Release Date | Key Milestones & Highlights |
| :--- | :---: | :--- |
| **`v1.1.0`** | 2026-09-16 | **Computational Photography & Multi-Frame Fusion Suite**:<br/>• Integrated Mertens Multi-Exposure HDR Fusion (`ExposureFusionService`) without tone-mapping halos.<br/>• Integrated Multi-Band Focus Stacking for synthetic deep depth-of-field.<br/>• Added À-Trous Wavelet Denoising (`WaveletDenoiseOp`) with soft/hard thresholding.<br/>• Added Fast Marching Inpainting (`AiInpaintOp`) for blemish and watermark removal.<br/>• Added Fast Guided Filter (`GuidedFilterOp`) & Directed Median (`DirectedMedianOp`).<br/>• Added Minkowski Gray-Edge Auto White Balance (`AutoWhiteBalance`).<br/>• Expanded test suite to **878 automated tests (100% pass rate)**. |
| **`v1.0.0`** | 2026-09-14 | **Initial Workstation Release**:<br/>• High-performance WPF .NET 8 desktop workstation architecture.<br/>• Non-destructive 32-bit float Linear Light image processing pipeline.<br/>• Out-of-process DirectML ONNX AI models (Upscaler, FaceRestorer, VisionTagger).<br/>• Integrated ZeroUI custom controls (SegmentedControl, CurveEditor, ColorWheel, MaskGizmo).<br/>• Virtual copies, selective copy settings, and auto-advance culling workflow.<br/>• Dual-mode deployment packager (Full Self-Contained & Lite Framework-Dependent). |

---

## 📄 License

Licensed under the **Apache License 2.0**. Part of the sovereign **ZeroUniverse** industrial computing ecosystem.
