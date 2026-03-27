# VR Optimization Guide (Standalone Quest 2)

## Draw Calls & Batching

- **Static Batching**: Enable "Static" for all non-moving objects.
- **Dynamic Batching**: Useful for small meshes with same materials.
- **GPU Instancing**: Essential for repetitive objects (like trees or crowd members).
- **SRP Batcher**: Keep shaders compatible with SRP Batcher for maximum efficiency.

## Mesh & Topology

- **Poly Count**: Aim for 100k - 200k triangles *per frame*. Quest 2 can handle more, but keep it low for dance games with many moving parts.
- **LOD (Level of Detail)**: Use LOD groups for distant objects.
- **Skinned Meshes**: For dancers, limit the number of bones and avoid too many `SkinnedMeshRenderers` onscreen at once.

## Lighting & Shaders

- **Baked Lighting**: *Always* bake your lights where possible. Real-time lights/shadows are extremely heavy.
- **Light Probes**: Use for moving objects (like players or dancers) to sample baked environment light.
- **Simple Shaders**: Use "URP/Simple Lit" or "URP/Unlit" where possible. Avoid "Complex Lit" or "Standard" shaders.

## Scripts & Physics

- **Fixed Timestep**: Keep `Time.fixedDeltaTime` at `1/72` (0.0138) to match the headset refresh rate.
- **Physics Layer Collision Matrix**: Disable collisions between layers that don't need to interact.
- **Avoid expensive calls**: Don't use `Find`, `GetComponent`, or `SendMessage` in `Update()`. Cache references in `Start()`.
