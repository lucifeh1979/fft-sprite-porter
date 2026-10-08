# FFT Sprite Porter

Use battle sprites made for the original *Final Fantasy Tactics* (the `.bmp` sheets and `.spr` files shared on [FFHacktics](https://ffhacktics.com)) in **FINAL FANTASY TACTICS - The Ivalice Chronicles** (Enhanced version), with their own colors.

There are two pieces, built from the same code:

| | What it is | Who it is for |
|---|---|---|
| **FFT Sprite Porter** (mod) | A Reloaded-II mod. Drop a sprite into a character's folder and launch the game. Delete the file to go back. | Players |
| **Mod Maker** (tool) | A small `.exe`. Drag a sprite onto it, pick the character, and it writes a finished, self-contained mod as a `.zip`. | Spriters who want to publish their sprite as a mod |

Downloads are on the [Releases](../../releases) page.

## What it does

- Reads the 256x488 indexed `.bmp` sheets and the game's `.spr` files.
- Keeps the sprite's own palette. A sprite does not have to reuse the colors of the character it replaces; extra unit palettes become the alternate team colors.
- 43 named characters and every generic job. In the mod, also the Dark Knight and Onion Knight from [Dark Knight & Onion Knight (Generic)](https://github.com/cipherxof/FFTGenericJobs) or WotL Restoration, and Balthier and Luso from WotL Restoration, while those mods are enabled.
- Does not touch the art: the sheet is enlarged 2x, pixel by pixel, to fit the Enhanced format.
- Smooth upscale (Scale2x) that rounds the jagged edges using only the sprite's own colors. It is a quick filter and does not compare to a sprite upscaled by hand: it softens edges, it cannot add detail. In the mod it is on by default; put `nosmooth` in the file name to keep the original pixels. In the Mod Maker it is a checkbox.

## Wrong colors?

If a replaced sprite shows up painted with the old character's colors, the game did not load the mod pack. It happens when Reloaded-II gets into the game too late (Auto Inject, or the game started before Reloaded-II). The mod writes a warning in the Reloaded-II log when it detects this. To fix it, in Reloaded-II use **Edit Application > Advanced Tools & Options > Deploy ASI Loader**, turn **Auto Inject** off and start the game again.

## What it does not do

- Portraits are not changed.
- Monsters and Lucavi are not supported.
- It cannot tell TYPE1 from TYPE2 sheets. The sheet has to match the character; if arms or heads look out of place in some animations, it is the other type.
- Sprites drawn for the original game can lack poses that some story scenes use.

## Using the mod

1. Install [Reloaded-II](https://github.com/Reloaded-Project/Reloaded-II) and the [FFTIVC Mod Loader](https://github.com/Nenkai/fftivc.utility.modloader).
2. Drag `FFT-Sprite-Porter-x.y.z.zip` into Reloaded-II and enable it.
3. Select the mod, click **Open Folder**, open `Sprites`. There is one folder per character (launch the game once if they are not there yet).
4. Put a `.bmp` or `.spr` in the character's folder and launch the game.

To remove a sprite, delete the file. The full guide is in [`spriteporter-mod/TUTORIAL.txt`](spriteporter-mod/TUTORIAL.txt).

## Using the Mod Maker

1. Run `FFTSpritePorter.exe` (needs the .NET 9 Desktop Runtime, which Reloaded-II users already have).
2. Drag the sprite onto the window.
3. Tick the character or job.
4. Fill in the mod name, your name and the sprite artist.
5. Click **Create mod (.zip)** and upload that zip as it is.

See [`spriteporter/QUICK START.txt`](spriteporter/QUICK%20START.txt).

## How it works

- A character's sheet lives in two `g2d` slots (`tex_N.bin` 512x512 and `tex_N+1.bin` 512x464, 4bpp, low nibble = left pixel). The classic 256x488 sheet is doubled to 512x976 and split across them.
- Only Ramza has palette rows in the `CharCLUT` table. For everyone else `CharShape` points at palette 0 and the colors come from elsewhere, so the sprite's palettes are added as new `CharCLUT` rows and the character's `CharShape` row is pointed at them. The mod loader merges both tables cell by cell.
- Sprites added by other mods (Dark Knight, Onion Knight, Balthier, Luso: ids 159-164, slots 1110-1121) have no `CharShape` row in the base game. The mod copies the row from the enabled mod that adds it and points it at the new palette.
- The table that maps each sprite id to its `g2d` slot was read from the game executable and is stored in [`spriteporter/sprite_table.json`](spriteporter/sprite_table.json).
- The `.spr` reader (palettes, the 288 raw rows, the nibble run-length compression of the last 200 rows) is in [`spriteporter/SheetCore.cs`](spriteporter/SheetCore.cs).

## Building

Requires the .NET 9 SDK.

- **Tool:** `dotnet publish -c Release -o publish` inside `spriteporter/`.
- **Mod:** `dotnet build -c Release` inside `spriteporter-mod/`. The project references the FFTIVC Mod Loader's DLLs and expects them at `../Release/Mods/fftivc.utility.modloader` (a Reloaded-II install next to this repository); change `ModLoaderDir` in the `.csproj` if yours is somewhere else.
- `spriteporter/data.zip` is embedded in the tool. It is generated by `build_data.py`, which needs files extracted from the game and is only required if you want to regenerate it.
- `spriteporter/empacotar.py` builds the release zips.

## Credits

- The FFHacktics sprite artists. Sprites belong to the people who drew them; this project ships none. Credit every artist when you share a mod made with it.
- [Nenkai](https://github.com/Nenkai), for the FFTIVC Mod Loader and FF16Tools.
- Twinees, whose Animist Female sheet was the first one ported this way.

Made by Lucifeh. Not affiliated with Square Enix.
