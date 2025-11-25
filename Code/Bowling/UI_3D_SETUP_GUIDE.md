# 3D/2D Mixed UI Setup Guide

This guide explains how to set up a mixed 3D/2D UI system similar to `test.ui.scene` for the bowling title and character selection screens.

## Structure Overview

The setup uses:
- **3D World**: Rotating parent with 3D objects (bowling pins, balls, etc.)
- **WorldPanel**: UI panels attached to 3D objects in the world
- **ScreenPanel**: 2D overlay UI for main menu/character select

## Scene Structure

```
Title/Character Select Scene
├── Spinner (GameObject with SpinComponent)
│   ├── Camera (CameraComponent)
│   ├── Bowling Pin 1 (ModelRenderer)
│   │   └── World UI (WorldPanel + TitleScreenPanel or CharacterSelectPanel)
│   ├── Bowling Pin 2 (ModelRenderer)
│   │   └── World UI (WorldPanel + TitleScreenPanel or CharacterSelectPanel)
│   └── Bowling Ball (ModelRenderer)
│       └── World UI (WorldPanel + TitleScreenPanel or CharacterSelectPanel)
├── Screen Panel (GameObject with ScreenPanel)
│   └── Title Screen Panel (TitleScreenPanel) - Optional overlay
└── Game Flow Manager (BowlingGameFlowManager)
```

## Component Setup

### 1. Spinner GameObject
- Add `SpinComponent` component
- Set `SpinAngles` to something like `(0, 5, 0)` for slow Y-axis rotation
- Position at origin or desired location

### 2. Camera (Child of Spinner)
- Add `CameraComponent`
- Set as main camera
- Position to view the rotating world
- Example: Position `(-280, 108, 198)`, Rotation `(0.035, 0.224, -0.152, 0.962)`

### 3. 3D Objects (Children of Spinner)
- Add `ModelRenderer` with bowling pin/ball models
- Position them around the center
- Add child GameObject "World UI" with:
  - `WorldPanel` component
    - `PanelSize`: `(1024, 1024)` or larger
    - `RenderScale`: `3` or appropriate scale
    - `LookAtCamera`: `true` or `false` depending on desired effect
  - `TitleScreenPanel` or `CharacterSelectPanel` component (Razor panel)

### 4. Screen Panel (Separate GameObject)
- Add `ScreenPanel` component
- Add child with `TitleScreenPanel` or `CharacterSelectPanel`
- This provides 2D overlay UI

## Example: Title Screen with Rotating Bowling Pins

1. Create "Spinner" GameObject with `SpinComponent` (SpinAngles: `0, 5, 0`)
2. Add Camera as child, positioned to view the scene
3. Create bowling pin GameObjects as children:
   - Each pin has a ModelRenderer
   - Each pin has a child "World UI" with:
     - `WorldPanel` component
     - `TitleScreenPanel` component (or create a simpler menu panel)
4. Create separate "Screen Panel" GameObject:
   - `ScreenPanel` component
   - Child with `TitleScreenPanel` for main menu

## WorldPanel vs ScreenPanel

- **WorldPanel**: Renders UI in 3D space, attached to objects, rotates with world
- **ScreenPanel**: Renders UI as 2D overlay, always on screen, doesn't rotate

You can use both:
- WorldPanel for immersive 3D menu items attached to objects
- ScreenPanel for main menu overlay that's always visible

## Tips

- Use `LookAtCamera: true` on WorldPanel to keep UI facing camera
- Adjust `RenderScale` on WorldPanel to make UI larger/smaller
- Position 3D objects in a circle or interesting pattern
- Use different SpinAngles for different rotation speeds
- You can have multiple WorldPanels on different objects showing different UI

