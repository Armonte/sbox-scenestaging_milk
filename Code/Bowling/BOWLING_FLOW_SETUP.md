# Bowling Game Flow Setup Guide

This document explains how to set up the bowling game flow with the new components.

## Overview

The bowling game now has a complete game flow:
1. **Get Ball** - Player spawns with a ball
2. **Charge & Throw** - Hold left mouse to charge, release to throw (power meter shows charge)
3. **Ball Travels** - Ball goes down lane, can hit pins or fall in gutter
4. **Settle & Score** - Wait for pins to settle, count knocked pins
5. **Reset** - Ball despawns, pins reset (or stay for 2nd roll), new ball given
6. **Win/Lose** - After 10 frames, game ends with final score

## Components

### BowlingGameManager
Main orchestrator for the game. Add to a single GameObject in your scene.

**Properties:**
- `TotalFrames` (default: 10) - Number of frames in the game
- `SettleTime` (default: 2.0s) - Time to wait for pins to settle after ball stops
- `ResetDelay` (default: 1.5s) - Time before resetting pins/ball
- `BallDespawnZ` (default: -500) - Z position where ball despawns if it falls off
- `BallMaxDistance` (default: 3000) - Max distance ball can travel before despawn

### BowlingPlayerController  
Handles player input for throwing. Attach to player prefab alongside PlayerController.

**Properties:**
- `BallPrefab` - Reference to bowling ball prefab
- `BallHoldPoint` - Child GameObject where ball is held (create an empty child of player)
- `ThrowChargeTime` (default: 1.5s) - Max time to fully charge throw
- `MinThrowForce` (default: 400) - Minimum throw force
- `MaxThrowForce` (default: 1800) - Maximum throw force at full charge

**Exposed State (read-only for UI):**
- `IsCharging` - Whether player is currently charging
- `ChargePercent` - 0-1 value of current charge

### BowlingPin
Attach to each pin prefab. Pins are now harder to knock over.

**Properties:**
- `KnockOverAngle` (default: 60°) - Angle from vertical to be considered knocked over
- `PinMass` (default: 1.5kg) - Mass of the pin
- `AngularDamping` (default: 2.0) - Resistance to tipping (higher = more stable)

### BowlingBall
Attach to ball prefab. Handles throw physics.

**Properties:**
- `ThrowForce` (default: 1000) - Base throw force
- `MaxThrowForce` (default: 2000) - Maximum throw force
- `Mass` (default: 7kg) - Ball mass
- `RollingResistance` (default: 0.05) - Friction coefficient

### GutterTrigger
Place trigger colliders on both sides of the lane for gutters.

**Properties:**
- `IsLeftGutter` / `IsRightGutter` - Which side of the lane

### LaneEndTrigger
Place a trigger collider at the end of the lane behind the pins.

**Properties:**
- `DespawnDelay` (default: 0.5s) - Delay before ball destruction

### BowlingHUD (Razor Panel)
UI panel showing charge meter, score, and game state. Add to a Screen Panel in your scene.

**Displays:**
- Power charge bar (when charging)
- Current frame / roll
- Total score
- Pins remaining
- Game state messages (settling, resetting)
- Game over screen with restart prompt

## Scene Setup

1. **Create Game Manager Object**
   - Create empty GameObject named "BowlingGameManager"
   - Add `BowlingGameManager` component

2. **Setup Player**
   - Ensure player prefab has `BowlingPlayerController` component
   - Create child empty GameObject named "BallHoldPoint" positioned in front of player
   - Set `BallHoldPoint` reference on BowlingPlayerController
   - Set `BallPrefab` reference to your bowling ball prefab

3. **Setup Pins**
   - Each pin needs `BowlingPin` component
   - Each pin needs `Rigidbody` component
   - Each pin needs a `Collider` (usually CapsuleCollider or mesh)
   - Tag each pin with "bowling_pin"

4. **Setup Ball Prefab**
   - Ball prefab needs `BowlingBall` component
   - Ball needs `Rigidbody` component
   - Ball needs `SphereCollider`
   - Ball will be tagged automatically with "bowling_ball"

5. **Setup Gutters**
   - Create two trigger volumes along lane sides
   - Add `GutterTrigger` component to each
   - Set `IsLeftGutter` or `IsRightGutter` appropriately

6. **Setup Lane End**
   - Create trigger volume at end of lane behind pins
   - Add `LaneEndTrigger` component

7. **Setup HUD**
   - Create ScreenPanel in scene
   - Add `BowlingHUD` component to it

## Controls

- **Left Mouse (attack1)** - Hold to charge throw, release to throw
- **R (reload)** - Restart game (only works when game is over)

## Game Flow States

| State | Description |
|-------|-------------|
| WaitingForThrow | Player has ball, can throw |
| BallInPlay | Ball has been thrown, tracking its progress |
| Settling | Ball stopped, waiting for pins to finish moving |
| ResettingPins | Pins being reset for next roll/frame |
| GameOver | All frames complete |

## Scoring

- Basic scoring: 1 point per pin knocked down
- Strike: All 10 pins on first roll (bonus scoring not yet implemented)
- Spare: All 10 pins on second roll (bonus scoring not yet implemented)
- 10th frame: Get extra rolls for strikes/spares

## Troubleshooting

**Ball doesn't spawn:**
- Check that `BallPrefab` is set on BowlingPlayerController
- Check that `BallHoldPoint` exists and is set

**Ball passes through pins:**
- Ensure both ball and pins have Rigidbody and Collider components
- Ensure colliders are NOT triggers
- Check that ball has "bowling_ball" tag

**Pins fall too easily:**
- Increase `PinMass` and `AngularDamping` on BowlingPin
- Increase `KnockOverAngle` for more tolerance

**Game doesn't reset after throw:**
- Ensure BowlingGameManager exists in scene
- Check console for errors about missing references

**HUD not showing:**
- Ensure BowlingHUD is on a ScreenPanel
- Check that BowlingGameManager and BowlingPlayerController exist in scene
