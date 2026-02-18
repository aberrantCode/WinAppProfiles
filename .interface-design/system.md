# WinAppProfiles Design System — Precision Dark

## Intent

**Who:** A developer or power user managing Windows environment contexts — switching between work, gaming, streaming, and minimal configurations.

**What they do:** Apply a profile to bring running apps and services into a desired state. One decisive action with real system consequences.

**How it should feel:** Like a control room. Purposeful, immediate. Every card represents something real. No decoration for its own sake — every visual element communicates state.

---

## Color Tokens

### Backgrounds (cool blue-black undertones)

| Token | Value | Usage |
|-------|-------|-------|
| `BackgroundPrimary` | `#0D0D0F` | Window background (darkest) |
| `BackgroundSecondary` | `#1A1A20` | Panel backgrounds, table rows |
| `BackgroundTertiary` | `#222228` | Input fields, search boxes |
| `BackgroundQuaternary` | `#16161A` | Card backgrounds |
| `BackgroundLight` | `#1E1E26` | Alternating table rows |

### Text

| Token | Value | Usage |
|-------|-------|-------|
| `TextPrimary` | `#F1F0F5` | Headings, card titles |
| `TextSecondary` | `#9993B4` | Secondary labels, status text (violet-gray) |
| `TextMuted` | `#6B6783` | Placeholders, disabled states |

### Accent

| Token | Value | Usage |
|-------|-------|-------|
| `AccentPrimary` | `#7C6FCD` | Primary buttons, selected states, active tabs |
| `AccentHover` | `#8B7FD8` | Hover state |
| `AccentPressed` | `#6B5EBF` | Pressed state |

### Status (the primary visual language)

| Token | Value | Usage |
|-------|-------|-------|
| `StatusRunning` | `#22C55E` | Running / alive |
| `StatusStopped` | `#EF4444` | Stopped / not running |
| `StatusError` | `#EF4444` | Error state |
| `StatusNotFound` | `#6B7280` | Not found on system |
| `StatusUnknown` | `#F59E0B` | Unknown / polling |

### Borders & Depth

| Token | Value | Usage |
|-------|-------|-------|
| `BorderColor` | `#2A2A35` | Card borders, panel dividers |
| `CardBorderBrush` | `#2A2A35` | Card 1px border (replaces drop shadows) |

### Controls

| Token | Value | Usage |
|-------|-------|-------|
| `ToggleOff` | `#2A2A35` | Toggle switch track, off state |
| `ToggleOffKnob` | `#9993B4` | Toggle switch knob, off state |

---

## Typography

**Typeface:** Segoe UI (system native, Windows-native feel)

| Style | Size | Weight | Color |
|-------|------|--------|-------|
| Header | 22px | SemiBold | TextPrimary |
| Section Header | 16px | SemiBold | TextPrimary |
| Card Title | 13px | SemiBold | TextPrimary |
| Body | 14px | Regular | TextPrimary |
| Status / Label | 12px | Regular | TextSecondary |
| Muted / Path | 11px | Regular | TextMuted |

---

## Depth System

**No heavy drop shadows.** Depth is created through:
1. **Border:** 1px `#2A2A35` border on cards and panels
2. **Background contrast:** Cards (`#16161A`) are visibly distinct from window (`#0D0D0F`)
3. **Border dividers:** 1px lines between sidebar and content, header and body

**Why:** Drop shadows are expensive in WPF and look soft/blurry. Border-based depth is crisp and reads better on high-DPI screens.

---

## Component Patterns

### Cards (CardWindow)

- Size: 180px × 200px
- Background: `BackgroundQuaternary` (`#16161A`)
- Border: 1px `CardBorderBrush` (`#2A2A35`), CornerRadius 6
- No drop shadow
- Content: 64×64 icon → name → status dot+text → toggle
- Dims to 50% opacity when `Exists = false`
- Gear icon appears on hover (0% → 80% opacity)

### Status Indicator

- 10px ellipse + status text
- Color-coded by `CurrentState` string
- Placed below icon, centered

### Toggle Switch

- 40×20px, CornerRadius 10
- Off: track `#2A2A35`, knob `#9993B4`
- On: track `AccentPrimary` (`#7C6FCD`), knob white

### Primary Button

- Background: `AccentPrimary`
- Foreground: white
- FontWeight: SemiBold, 14px
- Padding: 20,10
- CornerRadius: 4

### Secondary Button

- Background: `BackgroundTertiary`
- Foreground: `TextPrimary`
- Padding: 15,10
- CornerRadius: 4

### Search Box

- Background: `BackgroundTertiary`
- CornerRadius: 20 (pill shape)
- No border
- 14px input text

### Scrollbars

- Width/Height: 4px
- Track: transparent
- Thumb: `#606060` at 50% opacity, hover 70%

---

## Spacing

Base unit: 5px
- Card padding: 15px
- Card gap: 15px (right margin)
- Section padding: 20px
- Header padding: 15px
- Button padding: 20,10 (primary) / 15,10 (secondary)

---

## Signature

The **indigo accent** (`#7C6FCD`) distinguishes this tool from generic Windows-blue apps. It's present on:
- Applied/active state (buttons, selected list items, active tabs)
- Toggle switch track when ON
- Status bar background

The **violet-gray secondary text** (`#9993B4`) ties everything together — it's the most repeated mid-tone and keeps the palette cohesive.

---

## What We're Not

- Not a Fluent Design clone (`#0078D4` everywhere)
- Not a terminal emulator (no phosphor green, no monospace everything)
- Not a consumer app (no gradients, no illustrations)

Precision. State. Control.
