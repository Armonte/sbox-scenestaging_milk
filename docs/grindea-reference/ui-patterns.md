# UI and Menu Patterns -- Secrets of Grindea Reference

## Overview
The UI system handles the level-up selection (QuickLevel), shop interfaces, inventory management, in-game menus, and HUD elements. Key files include the `States/Menus/` directory for menu state machines and `Rendering/Components/GUI/` for HUD rendering.

## State Machine Architecture
The game uses a state master pattern:
```csharp
public class StateMaster
{
    // Manages transitions between game states:
    // - Main menu, gameplay, inventory, shop, etc.
    // - States can be nested (e.g., shop within gameplay)
}
```

### Game States
Located in `/States/`:
- `RogueLike.cs` - Arcade mode state
- `InventoryState.cs` - Inventory/equipment screen
- `Options.cs` - Settings menu
- `Replays.cs` - Replay viewer
- `GlobalData.cs` - Persistent world data
- `Gameover/` - Death/game over screens
- `Menus/` - Menu subsystems
- `Minigames/` - Mini-game states (archery, fishing, etc.)
- `Tutorial.cs`, `TutorialBoss.cs`, `EquipmentTutorial.cs` - Tutorial states

## Level-Up System (Chaos Mode)

### QuickLevel Interface
When a level-up plate is collected in Chaos Mode, the player is presented with upgrade options:

```csharp
// Chaos_UpgradePlate.GetRandom() generates options:
// - 6 stat types (HP, Damage, Speed, MaxEP, EPReg, TalentPoints)
// - Or a spell if random chance triggers (dSpellChance)

// Spell selection ensures variety:
// - At least 2 melee/magic spells available
// - At least 2 utility spells available
// - Already-maxed spells are filtered out
```

### Upgrade Values Per Selection
| Upgrade | Per-Pick Value |
|---------|---------------|
| HP Up | +50 Max HP, +50 current HP |
| Damage Up | +10 ATK, +10 MATK |
| Speed Up | +8 ASPD, +8 CSPD |
| Max EP Up | +15 Max EP |
| EP Regen Up | +22% EP Regen |
| Talent Points | +2 Talent Points |
| Spell (Lv0->1) | Learn spell, auto-equip |
| Spell (Lv1->5) | 4 level ups (offense) or 2 (utility) |
| Spell (Lv5->10) | 5 level ups to max |

### Display
- Each option shows an icon from the talent/spell icon set
- Spell options show spell name + target level
- Stat options show type name + current level count
- Selection confirmed with visual effects (particle explosion, level-up sound)

## Shop System

### Shop Items
- Shop rooms have a fixed number of items for sale
- "Extra Items In Shop" perk adds 1-2 more items
- Item pool is determined by current region and floor
- Shadier Merchant sells at 50% discount
- Treat "Cheaper Stores" gives 30% discount

### Shop UI Pattern
Located in `States/Menus/ShopsAndCraft.cs`:
- Grid/list display of available items
- Each item shows: name, icon, stats, price
- Equipment items preview stat changes
- Compare-to-equipped functionality
- Buy confirmation

## Inventory Management
Located in `/Items/Inventory.cs`:
- Grid-based inventory with fixed slots
- Equipment separated from consumables
- Quick-equip from inventory
- Sort functionality

## Equipment Screen
Located in `/Entities/Player/Equipment.cs`:
- Slot-based equipment: weapon, shield, hat, facegear, accessory, shoes
- Equip slots for quick-use spells
- Visual stat change preview on hover
- Equipment special effects displayed

## HUD Elements

### Health/EP Display
- Health bar with numerical display
- EP (Energy Points) bar
- Shield HP bar (when shield equipped)
- Potion cooldown indicators

### Status Effect Display
```csharp
// Active buffs tracked in denxClientBuffTracker
// Displayed as icons with duration countdown
// GetListOfActiveBuffs() returns sorted list by remaining duration
```

### Mini-Boss HP Bar
```csharp
// MiniBossHPRenderComponent
// Shows boss name and HP bar
// bForceClose flag for cleanup
```

### Room Grade Display
- S/A/B/C grade shown on room clear
- Time-based grade + damage-based grade for arena
- Visual feedback with particle effects

### Challenge Timer
```csharp
// ShowChallengeTimer() returns true when:
// - In challenge room, OR
// - SRankOrDie mode with battle room timer running
// - Hidden during zoning transitions
// - Some challenge types hide timer (DontShowChallengeTimer flag)
```

## Notification System
```csharp
// NoticeImage: Floating icon above player
// NoticeTextWatcher: Floating text above player
// Used for level-ups, item pickups, buff activations
// Each has fade time (iFadeFrame) and finish time (iFinishFrame)
```

## Player View Stats
Located in `/Entities/Player/PlayerViewStats.cs`:
```csharp
// Tracks per-player:
// - Skill levels (GetSkillLevel)
// - Talent points (iTalentPoints)
// - Level and experience
// - Equipped spells and quick slots
```

## Journal / Codex
Located in `/Entities/Player/Journal.cs`:
- Enemy codex entries
- Card album
- Item discovery log
- Quest tracking

## Key Code Locations
- StateMaster: `/States/StateMaster.cs`
- Menus: `/States/Menus/` directory
- Inventory: `/Items/Inventory.cs`
- Equipment: `/Entities/Player/Equipment.cs`
- PlayerViewStats: `/Entities/Player/PlayerViewStats.cs`
- HUD rendering: `/Rendering/Components/GUI/`
- Notice system: `/Watchers/NoticeTextWatcher.cs`, `ShowFloatingTextWatcher.cs`
- Spell description UI: `/SoG/SpellDescription.cs`
- GUIStuff: `/Entities/Player/GUIStuff.cs`

## Design Patterns Worth Stealing
- Chaos mode upgrade plate design: Present 3-6 options, player picks one, creates "draft" feel
- Spell leveling in jumps (0->1->5->10) makes each selection dramatically impactful
- Auto-equip on first spell learn removes a friction point in roguelike runs
- Stat preview on equipment hover is essential for informed decisions
- S/A/B/C grading system creates instant performance feedback and replayability
- Notice system (floating icons + text) is simple but effective for communicating state changes
- Challenge timer visibility rules (hidden during transitions, optional per challenge type) prevent UI noise
- Buff display sorted by remaining duration helps players track expiring effects
- The "par time" approach to grading is transparent and learnable by players
