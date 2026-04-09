# Vertex Skybox Editor — Test Checklist

## Setup
- [ ] Launch s&box, scene has a SkyboxComponent with a loaded .skye file
- [ ] Activate "Spyro Skybox" from the toolbar dropdown
- [ ] Verify 15 tool icons appear in the left sidebar, grouped logically

## Tool Switching
- [ ] Click each tool icon — verify it switches (active tool highlighted blue)
- [ ] Verify sidebar stays visible with all controls when switching tools

## Paint Tool (brush icon)
- [ ] Left-click and drag to paint — vertices change color
- [ ] Shift+click to paint with right/secondary color
- [ ] Ctrl+click to pipette (pick color from vertex → updates left paint color in sidebar)
- [ ] Adjust brush radius in sidebar — brush circle changes size
- [ ] Adjust opacity — painting becomes more/less transparent
- [ ] Hold mouse still while painting — no over-accumulation (cursor-delta guard)
- [ ] Ctrl+Z — paint stroke reverts in one undo step
- [ ] Ctrl+Y — redo restores it

## Pipette Tool (colorize icon)
- [ ] Left-click a vertex — left paint color updates in sidebar
- [ ] Right-click — right paint color updates
- [ ] Color preview ring appears on hovered vertex

## Gradient Tool (gradient icon)
- [ ] Click and drag — preview line appears with color dots at start/end
- [ ] Release — gradient applied to vertices near the line
- [ ] Ctrl+Z — gradient reverts

## Sketch Pencil (edit icon)
- [ ] Left-click and drag — white reference lines drawn on sphere
- [ ] Right-click and drag — erases sketch points near cursor
- [ ] Lines persist across tool switches
- [ ] Ctrl+Z — sketch stroke reverts

## Grab Tool (open_with icon)
- [ ] Click and drag on vertices — they move along sphere surface
- [ ] Falloff: center vertices move more than edge vertices
- [ ] Release — vertices stay re-projected on sphere
- [ ] Ctrl+Z — grab reverts

## Select Tool (near_me icon)
- [ ] Right-click vertex — selects it (orange highlight)
- [ ] Shift+Right-click — additive selection
- [ ] Ctrl+Right-click — subtractive selection
- [ ] Left-click drag — moves selected vertices on sphere
- [ ] Shift+Left-click drag — rotates selection around sphere center
- [ ] Ctrl+Left-click drag — scales selection around center
- [ ] Ctrl+Z — transform reverts

## Pipette Select (format_color_fill icon)
- [ ] Click a vertex — selects all vertices with similar color
- [ ] Shift+click — adds similar-color verts to selection
- [ ] Ctrl+click — removes them

## Selection Group (workspaces icon)
- [ ] Press digit 0-9 to set active group
- [ ] Left-click/drag — assigns group to vertices in brush
- [ ] Vertices in active group highlighted cyan near cursor

## Create Tool (add_circle icon)
- [ ] Click on sphere — new vertex appears at cursor
- [ ] Drag — places evenly-spaced vertices along path
- [ ] New vertex uses current left paint color
- [ ] Ctrl+Z — reverts vertex creation

## Delete Tool (delete icon)
- [ ] Click/drag — removes vertices + connected edges/triangles in brush
- [ ] Shift+click — removes only edges/triangles, keeps vertices
- [ ] Ctrl+Z — reverts deletion

## Edge Flip (swap_horiz icon)
- [ ] Hover near an edge — highlights cyan
- [ ] Click — flips the shared edge diagonal
- [ ] Only works on edges shared by exactly 2 triangles
- [ ] Ctrl+Z — flip reverts

## Edge Collapse (compress icon)
- [ ] Hover near an edge — highlights red
- [ ] Click — edge collapses to single vertex at cursor
- [ ] New vertex color is blended from the two endpoints
- [ ] Degenerate edges/triangles auto-removed
- [ ] Ctrl+Z — collapse reverts

## Triangle Fill (change_history icon)
- [ ] Click 3 separate vertices — triangle created between them (green preview)
- [ ] Subsequent click chains (reuses last 2 verts for strip building)
- [ ] Right-click — resets the chain
- [ ] Preview lines shown while picking
- [ ] Ctrl+Z — triangle creation reverts

## Autofill (auto_fix_high icon)
- [ ] Click/drag over vertices — triangles auto-created between nearby verts
- [ ] Respects max edge length (brush radius * 1.5)
- [ ] Doesn't create duplicate triangles
- [ ] Ctrl+Z — reverts

## Beautify (auto_awesome icon)
- [ ] Click/drag over messy triangulation — edges flip to improve angles
- [ ] Wireframe becomes more uniform
- [ ] Ctrl+Z — reverts

## Sidebar: File Operations
- [ ] **Load .skye** — opens file dialog, loads skybox
- [ ] **Save .skye** — saves current state to file
- [ ] **Import Spyro Sky (.json)** — imports PS1 sky data
- [ ] **Export OBJ** — exports to .obj file
- [ ] **Export PLY** — exports to .ply file
- [ ] **New Sky** — replaces with blank sphere (undoable)
- [ ] **Fix Errors** — removes degenerate geometry, fixes winding (undoable)
- [ ] Stats label shows correct vertex/tri/edge counts

## Sidebar: Color Adjustments (all undoable)
- [ ] Drag Saturation slider — colors change live, Ctrl+Z reverts
- [ ] Drag Brightness slider — same
- [ ] Drag Gamma slider — same
- [ ] Drag R/G/B Shift sliders — channel shifts, undoable
- [ ] Change Scale — skybox resizes
- [ ] Change Background Color — viewport bg changes

## Sidebar: Palette
- [ ] 40 color slots visible in 10x4 grid
- [ ] Left-click slot — sets left paint color
- [ ] Right-click slot — sets right paint color
- [ ] Double-click slot — color picker popup opens, edit color
- [ ] "Palette from Sky" — samples colors from vertex data into palette
- [ ] Palette persists with .skye save/load

## Sidebar: Layers
- [ ] Current layer label shows "Current Layer: 0"
- [ ] Select vertices, click "Layer Up" — verts move to layer 1, label updates
- [ ] Click "Layer Down" — verts move back to layer 0
- [ ] Layers range 0-5, buttons clamp at boundaries
- [ ] Layer changes are undoable (Ctrl+Z)
- [ ] Vertices on higher layers render further from center (parallax/RenderScale)

## Keyboard Shortcuts: Selection
- [ ] **Ctrl+A** — Select All / Deselect All (toggle)
- [ ] **Ctrl+I** — Invert selection
- [ ] **Ctrl+L** — Select linked (flood fill from selection)
- [ ] **Numpad +** — Grow selection by one ring
- [ ] **Numpad -** — Shrink selection by one ring
- [ ] **Ctrl+C** — Copy selected geometry
- [ ] **Ctrl+V** — Paste geometry (undoable, pasted verts auto-selected)

## Keyboard Shortcuts: Tool Switching
- [ ] **B** — Paint Brush
- [ ] **I** — Pipette
- [ ] **G** — Gradient
- [ ] **P** — Sketch Pencil
- [ ] **O** — Grab
- [ ] **S** — Select
- [ ] **C** — Create
- [ ] **X** — Delete
- [ ] **F** — Edge Flip
- [ ] **E** — Edge Collapse
- [ ] **T** — Triangle Fill
- [ ] **A** — Autofill
- [ ] **D** — Beautify
- [ ] Hotkeys only fire without Ctrl/Shift held (no conflict with Ctrl+A etc)

## General
- [ ] Multiple undo/redo steps work across different tool operations
- [ ] Loading a new .skye file works while any tool is active
- [ ] Switching between tools preserves selection state
- [ ] No crashes on empty sky (0 vertices)
