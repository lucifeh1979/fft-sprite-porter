FFT Sprite Porter 1.1.1
Classic FFT sprite sheets -> Reloaded-II mods for FINAL FANTASY TACTICS - The Ivalice Chronicles (Enhanced version)

WHAT IT DOES
Takes a sprite made for the original game (the 256x488 .bmp sheets or the .spr files shared on FFHacktics) and builds a
ready-to-install mod that gives that sprite to the character or job you pick. The sheet keeps its own colors:
the mod carries the sheet's palette, so you are not limited to sprites that reuse the original character's colors.

HOW TO USE
1. Run FFTSpritePorter.exe.
2. Pick the sprite: a .bmp sheet or a .spr file (Browse, or drop the file on the window).
3. Tick who gets the sprite. You can tick more than one (for example the three Ramza chapters).
4. Fill in the mod name, your name and the sprite artist, then click "Create mod (.zip)".
5. Drag the zip into the Reloaded-II window, enable the mod, launch the game.

REQUIREMENTS
- To run the tool: .NET 9 Desktop Runtime (x64). If you use Reloaded-II you already have it.
- To use the mods it creates: Reloaded-II and the FFTIVC Mod Loader (fftivc.utility.modloader).

GOOD TO KNOW
- A .bmp/.png sheet must be an indexed (16 or 256 color) image, 256x488. A screenshot or a true-color export will
  not work. A .spr file is read as it is, with all its palettes.
- Use a sheet of the same kind as the character you are replacing. Human sheets come in two layouts on
  FFHacktics (TYPE1 and TYPE2); if arms or heads look wrong in some animations, the sheet is the other type.
  The tool cannot tell them apart. It only warns when a sheet is clearly not a human sheet.
- Sheets drawn for the original game can lack a few poses that story scenes use. Those frames show up empty or odd.
- If the sheet has more than one palette, the extra ones become the alternate team colors.
- Portraits are not changed.
- A generic job sprite changes every unit of that job and gender, yours and the enemy's.
- Two mods made with this tool work together as long as they do not replace the same character.
- Monsters, Lucavi and story NPCs are not supported.

CREDIT THE ARTISTS
Sprites belong to the people who drew them. FFHacktics allows its sprites in mods as long as every artist is
credited, so put their names on your mod page.

THANKS
- Nenkai, for the FFTIVC Mod Loader and FF16Tools.
- The FFHacktics community and its sprite artists.
