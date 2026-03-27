# XR Interaction Toolkit 3.x Patterns

## Interaction Groups

XRI 3.x uses **Interaction Groups** to manage multiple interactors (like a grab interactor and a poke interactor) on a single controller/hand. 
- Use groups to prevent conflicts between different interaction types.
- Prioritize interaction types (e.g., Poke > Grab).

## Locomotion

- **Snap Turn / Continuous Turn**: Use Action-based controllers.
- **Teleportation**: Set up Teleportation Areas and Anchors.
- **Provider Pattern**: Link `Locomotion Mediator` to `Teleportation Provider` and `Turn Provider`.

## Action-Based Input

- All inputs should go through **Input System Actions**.
- Use the **XR Origin (XR Rig)** prefab with appropriate action-based components.
- Map Meta Quest 2 controllers to OpenXR patterns.

## Hand Tracking

- Use **XR Hand Tracking** (if available) with XRI 3.x's specialized hand interactors.
- Ensure "Hand Tracking" is enabled in the Android Manifest (via Meta/Oculus XR features).
