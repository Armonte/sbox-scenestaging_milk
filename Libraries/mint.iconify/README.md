# Sbox Iconify

Use **200,000+ icons** from any icon pack in your S&Box project with a single tag.

Icons are fetched at runtime from the [Iconify API](https://iconify.design/) and cached locally for instant loading.

## Installation

Install from the S&Box Library Manager — search for **"Sbox Iconify"** by **Mint Studios**.

## Usage

```html
<iconify icon="ph:house" Size=@(24) Color="white" />
<iconify icon="ph:gear" Size=@(20) Color="#c0392b" />
<iconify icon="mdi:account" Size=@(32) Color="rgb(255,255,255)" />
```

Drop it into any Razor component. Icons load asynchronously and cache automatically.

## Parameters

| Parameter | Type | Default | Description |
|-----------|------|---------|-------------|
| `icon` | string | `""` | Icon identifier in `prefix:name` format |
| `Color` | string | `"white"` | Icon color (hex, named, rgb) |
| `Size` | int | `24` | Icon size in pixels |

## Browse Icons

Find icons at **[icones.js.org](https://icones.js.org/)**

Popular icon packs:
- **ph** — [Phosphor Icons](https://icones.js.org/collection/ph) (1,500+ icons)
- **mdi** — [Material Design Icons](https://icones.js.org/collection/mdi) (7,000+ icons)
- **tabler** — [Tabler Icons](https://icones.js.org/collection/tabler) (4,000+ icons)
- **lucide** — [Lucide](https://icones.js.org/collection/lucide) (1,500+ icons)
- **ic** — [Google Material Symbols](https://icones.js.org/collection/ic) (10,000+ icons)
- **game-icons** — [Game Icons](https://icones.js.org/collection/game-icons) (4,000+ icons)

## How It Works

1. First render — fetches SVG from `api.iconify.design`
2. Converts SVG to texture via S&Box's rendering pipeline
3. Caches to disk (`FileSystem.Data/iconify_cache/`) and memory
4. Subsequent loads are instant — no network needed

## Credits & Acknowledgments

This library is built on top of the work of others:

- **[Iconify](https://iconify.design/)** by Vjacheslav Trushkin — the unified icon framework and API that makes this possible
- **[Facepunch](https://github.com/Facepunch/sbox-iconify)** — original S&Box Iconify implementation that inspired this library
- **[UmbleStudio](https://sbox.game/)** — their Iconify library for S&Box provided additional reference
- **[Phosphor Icons](https://phosphoricons.com/)**, **[Material Design Icons](https://materialdesignicons.com/)**, **[Tabler Icons](https://tabler-icons.io/)**, and all other icon pack creators

This is a community contribution — if you find bugs or want to improve it, PRs are welcome.

## License

MIT
