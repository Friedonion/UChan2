# Meta Quest 2 Standalone Optimization

## Project Settings (XR Plug-in Management)

- **Plug-in Providers**: Enable **Oculus** (or Meta XR Plugin) for Android.
- **Stereo Rendering Mode**: Always use **Single Pass Instanced**. Multi-Pass is too expensive for Quest 2.
- **Color Space**: **Linear** is recommended, but Gamma is cheaper for performance if needed. URP works best with Linear.

## URP Settings

- **Anti-aliasing (MSAA)**: 4x is usually the sweet spot for VR. Higher can kill performance; lower looks "shimmery".
- **Opaque Texture/Depth Texture**: Disable if not needed. These add extra draw calls.
- **HDR**: Disable if your game doesn't strictly need high-dynamic range (improves performance).

## Performance Targets

- **Refresh Rate**: 72Hz is standard for Quest 2. Can be pushed to 90Hz or 120Hz but requires extreme optimization.
- **Foveated Rendering**: Use `OVRManager` (if using Meta SDK) or OpenXR extensions to enable dynamic foveated rendering.
