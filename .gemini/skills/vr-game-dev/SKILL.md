---
name: vr-game-dev
description: Expert VR game development assistance for Unity, specializing in Meta Quest 2, XR Interaction Toolkit (XRI) 3.x, and standalone performance optimization. Use when building VR games, setting up XR rigs, implementing locomotion/interaction, or optimizing for Meta Quest.
---

# VR Game Development (Meta Quest 2 & XRI 3.x)

This skill provides specialized guidance for building high-performance VR applications using Unity and the XR Interaction Toolkit.

## Key Workflows

### 1. Project Setup & Optimization
- **Meta Quest 2 Deployment**: Ensure your project is configured for standalone performance. 
- **Reference**: See [meta-quest-stand-alone.md](references/meta-quest-stand-alone.md) for URP and XR Plug-in settings.
- **Performance**: Review [vr-optimization.md](references/vr-optimization.md) for draw calls, triangles, and shader tips.

### 2. Interaction & Locomotion (XRI 3.x)
- **XR Origin Setup**: Use the modern `XR Origin (XR Rig)` prefab with `Action-Based` controllers.
- **Interactors**: Configure Interaction Groups for hands/controllers.
- **Locomotion**: Implement Snap Turn, Continuous Turn, and Teleportation.
- **Reference**: See [xri-3.x-patterns.md](references/xri-3.x-patterns.md).

### 3. Dance Game Mechanics (Specialized)
- **Rhythm Sync**: Use `AudioSettings.dspTime` for precise rhythm tracking. Avoid `Time.time` for music games.
- **Dance Validation**: Use collision triggers or position/rotation checks on hand/foot transforms against target volumes.
- **Haptics**: Trigger controller haptics for rhythmic feedback: `xrController.SendHapticImpulse(amplitude, duration)`.

## Expert Principles
- **Comfort First**: Always provide "Comfort" options (Snap Turn, Blinders) for motion-sensitive players.
- **Performance is UX**: Dropped frames in VR cause nausea. Maintain a solid 72 FPS at all times.
- **Physicality**: Design interactions that feel tactile. Use haptics and spatial audio to reinforce actions.
