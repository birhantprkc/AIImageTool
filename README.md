# 🌌 ZeroVision — High-Performance Image Processing & AI Studio

[![Type: Desktop Application](https://img.shields.io/badge/Type-Desktop%20Application-007ACC?style=flat-square&logo=windows)](https://github.com/kzxl/ZeroVision)
[![Ecosystem](https://img.shields.io/badge/Ecosystem-ZeroUniverse-8A2BE2?style=flat-square)](https://github.com/kzxl/ZeroUniverse)
[![Platform](https://img.shields.io/badge/Platform-Windows%20x64-0078D6?style=flat-square&logo=windows)](https://dotnet.microsoft.com/)
[![Distribution: Standalone Single-File](https://img.shields.io/badge/Distribution-Standalone%20Single--File-2ea44f?style=flat-square)](https://github.com/kzxl/ZeroVision)
[![Build and Test](https://github.com/kzxl/ZeroVision/actions/workflows/build-test.yml/badge.svg)](https://github.com/kzxl/ZeroVision/actions/workflows/build-test.yml)
[![License: Apache 2.0](https://img.shields.io/badge/License-Apache_2.0-blue.svg)](LICENSE)


> **ZeroVision** (incorporating **Aurora Studio**) is a desktop workstation application (WPF, .NET 8) for high-fidelity image management, color grading, and computer vision enhancement. Part of the sovereign **ZeroUniverse** ecosystem, it combines a **non-destructive 32-bit float Linear Light** develop pipeline (similar to Adobe Lightroom / Darktable) with autonomous **DirectML ONNX AI engines** for super-resolution upscaling, face restoration, and intelligent auto-tagging.

---

![ZeroVision Preview](screenshots/preview.png)

---

## 🌟 Key Capabilities

### 1. Non-Destructive Develop Pipeline (Linear Light)

All adjustments operate in **linear light float RGBA** color space through an intelligent DAG pipeline, preventing color banding and preserving maximum highlight/shadow dynamic range.

#### Tone & Lighting
- **Exposure, Contrast, Highlights, Shadows, Whites, Blacks**
- **Tone Curve & Parametric Curve** with interactive drag-point editing and presets (*Linear, Medium, Strong, Faded*)
- **Filmic, Filmic RGB, Sigmoid, Tone Equalizer, Dehaze**
- **Levels** (per-channel histogram stretching, **Auto Levels**, **Auto Color** for color cast removal)
- Interactive **Histogram** with direct tone adjustment by dragging

#### Color & Grading
- **White Balance** (Kelvin temperature slider, **Auto WB**, **eyedropper** tool, standard illuminant presets)
- **HSL 8-Channel Mixer** + **Targeted Adjustment Tool (TAT)** (click-drag directly on image canvas for rapid HSL tuning)
- **Color Balance RGB 4-Way** and **Color Grading** wheels
- **Split Toning, Channel Mixer, Selective Color, Color Unify, Velvia, Color Contrast (Lab)**
- **3D LUT (.cube)** support and ICC input color profiles (**sRGB, AdobeRGB, Rec2020, Display P3**)
- **Black & White**: Deep channel mixing with classic color filters and toning simulation
- **Film Negative (negadoctor)**: High-precision negative film scan inversion and color reconstruction

#### Detail & Sharpness
- **Sharpen** (radius control + intelligent edge **Masking**)
- **Noise Reduction** (Luminance, Color, Chroma)
- **Diffuse-or-sharpen (PDE)** filter, **Hot Pixel** removal, **CA Correct**, **Defringe**
- **Texture, Clarity, Grain** (monochrome and chromatic film grain synthesis)

#### Geometry & Layout
- **Crop** with free or standard aspect ratios (1:1, 16:9, 4:3, golden ratio) and composition guide overlays
- **Straighten, Rotate, Flip** with EXIF-based auto-rotation
- **Perspective / Upright** correction, **Liquify/Warp** with intuitive handle-based editing
- **Lens Correction**: Automatic distortion and vignetting correction via **lensfun** database (EXIF-based) or manual adjustment

#### Local Adjustments & Masking
- **Linear Gradient Mask, Radial Mask, Brush Mask**
- **Luminance Mask** and **Color Range Mask** for targeted tonal edits
- Mask inversion, feathering, opacity, and multi-mask stacking

---

### 2. Autonomous AI & Vision Pipeline

ZeroVision decouples heavy AI inference into isolated background host processes (`ImageTool.Host`) via DirectML / ONNX Runtime:

| AI Plugin | Model Architecture | Role |
| :--- | :--- | :--- |
| **Upscaler** | Real-ESRGAN / AuraSR | 2x / 4x super-resolution upscaling with DirectML acceleration |
| **Face Restorer** | GFPGAN / CodeFormer | High-fidelity portrait reconstruction and eye/facial feature enhancement |
| **Vision Tagger** | MobileNet / ResNet ONNX | Semantic multi-label scene and object classification |

---

## 🏗 Architecture & Project Layout

```
ZeroVision/
├── ImageTool.Core/          # Domain models, preset system, pipeline abstractions
├── ImageTool.Imaging/       # 32-bit float linear light image processing kernels
├── ImageTool.Host/          # Out-of-process DirectML ONNX AI runner
├── ImageTool.Shared/        # IPC contracts, cross-process shared memory buffers
├── ImageTool.Plugins.*/     # Autonomous plugin modules (Upscaler, FaceRestorer, VisionTagger)
├── TestDML/                 # DirectML hardware acceleration diagnostic harness
└── ImageTool.Tests/         # ~800 automated unit and integration tests
```

---

## ⚡ Quick Start for Developers

### Prerequisites
- **Windows 10** (build 1809+) or **Windows 11** (64-bit)
- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- DirectX 12 capable GPU (for DirectML AI acceleration)

### Build and Run

```bash
# Clone repository
git clone https://github.com/kzxl/ZeroVision.git
cd ZeroVision

# Build solution
dotnet build ImageTool.slnx -c Release

# Run automated test suite
dotnet test ImageTool.Tests/ImageTool.Tests.csproj
```

### Packaging

To generate optimized Lite and Full deployment packages:
```powershell
pwsh ./publish.ps1
```

---

## 📄 License

Licensed under the **Apache License 2.0**. Part of the sovereign **ZeroUniverse** industrial computing ecosystem.
