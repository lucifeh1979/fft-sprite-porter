using System.Reflection;
using System.Text;
using System.Text.Json;
using FF16Tools.Files.Nex;
using fftivc.utility.modloader.Interfaces;
using Reloaded.Mod.Interfaces;
using Reloaded.Mod.Interfaces.Internal;

namespace SpritePorter;

public class Mod : IMod
{
    public const string ModId = "fftivc.utility.spriteporter";
    const string SpritesDir = "Sprites";
    const string HowTo =
        "FFT Sprite Porter\r\n\r\n" +
        "To give a character a sprite made for the original game:\r\n" +
        "  1. Put the sprite file (.bmp or .spr, as shared on FFHacktics) inside the folder with that character's name.\r\n" +
        "  2. Launch the game.\r\n\r\n" +
        "To go back to the original sprite: delete the file from the folder and launch the game again.\r\n\r\n" +
        "Only one sprite per folder is used (the first in alphabetical order).\r\n" +
        "\"Ramza (all chapters)\" and \"Delita (all chapters)\" apply to every chapter at once; a file in a single-chapter folder wins over them.\r\n" +
        "A sprite in these folders wins over a sprite that comes from another mod.\r\n" +
        "Folders starting with \"Job - \" are the generic jobs: they change every unit of that job and gender.\r\n" +
        "The Dark Knight and Onion Knight folders show up while Dark Knight & Onion Knight (Generic) or WotL Restoration is enabled.\r\n" +
        "Sprites are smoothed when doubled to the game's resolution. To keep the original jagged pixels, put \"nosmooth\" in the file name (for example \"MySprite nosmooth.bmp\").\r\n" +
        "Portraits are not changed.\r\n";

    ILogger _log;
    IModLoader _loader;

    public void StartEx(IModLoaderV1 loaderApi, IModConfigV1 modConfig)
    {
        _loader = (IModLoader)loaderApi;
        _log = (ILogger)_loader.GetLogger();
        try
        {
            _loader.GetController<IFFTOModPackManager>().TryGetTarget(out var packs);
            if (packs == null) { Log("FFT mod loader not found."); return; }
            if (packs.GameMode != FFTOGameMode.Enhanced) { Log("only the Enhanced version is supported."); return; }

            var targets = LoadTargets(EnabledMods());
            var ownDir = Path.Combine(_loader.GetDirectoryForModId(ModId), SpritesDir);
            PrepareOwnFolder(ownDir, targets);

            var sources = SpriteModFolders();
            sources.Add((ModId, ownDir));

            var chosen = new Dictionary<Character, (string file, string owner)>();
            foreach (var (owner, dir) in sources)
            {
                if (!Directory.Exists(dir)) continue;
                var found = new List<(Target target, string file)>();
                foreach (var sub in Directory.GetDirectories(dir))
                {
                    var files = Directory.GetFiles(sub).Where(f => f.EndsWith(".bmp", StringComparison.OrdinalIgnoreCase) || f.EndsWith(".spr", StringComparison.OrdinalIgnoreCase))
                        .OrderBy(f => f, StringComparer.OrdinalIgnoreCase).ToList();
                    if (files.Count == 0) continue;
                    if (!targets.TryGetValue(Normalize(Path.GetFileName(sub)), out var target)) { Log($"{owner}: folder \"{Path.GetFileName(sub)}\" is not a character this mod knows - skipped."); continue; }
                    if (files.Count > 1) Log($"{owner}: \"{Path.GetFileName(sub)}\" has {files.Count} sprites, using {Path.GetFileName(files[0])}.");
                    found.Add((target, files[0]));
                }
                foreach (var (target, file) in found.OrderBy(f => f.target.Characters.Count > 1 ? 0 : 1))
                    foreach (var c in target.Characters) chosen[c] = (file, owner);
            }
            if (chosen.Count == 0) { PrepareCache(); Log("no sprites to port. Put a .bmp or .spr inside a character's folder in this mod's Sprites folder."); return; }

            var cacheDir = PrepareCache();
            var palettes = new List<(int key, Sheet sheet, List<int> ids)>();
            foreach (var group in chosen.GroupBy(c => c.Value.file, StringComparer.OrdinalIgnoreCase))
            {
                Sheet sheet;
                try
                {
                    sheet = group.Key.EndsWith(".spr", StringComparison.OrdinalIgnoreCase) ? Sheet.LoadSpr(group.Key) : Sheet.LoadBmp(group.Key)
                        ?? throw new Exception("This .bmp uses a format this mod can't read (compressed or very old). Save it again as a plain 8-bit .bmp.");
                }
                catch (Exception e) { Log($"{Path.GetFileName(group.Key)}: skipped ({e.Message})"); continue; }
                bool smooth = !Normalize(Path.GetFileNameWithoutExtension(group.Key)).Contains("nosmooth");
                var (top, bottom) = sheet.ToHd(smooth);
                foreach (var c in group.Select(g => g.Key))
                {
                    AddSheet(packs, cacheDir, c.tex, top);
                    AddSheet(packs, cacheDir, c.tex + 1, c.mods == null ? bottom : [.. bottom, .. new byte[top.Length - bottom.Length]]);
                }
                var ids = group.SelectMany(g => g.Key.ids).Distinct().OrderBy(i => i).ToList();
                palettes.Add((ids[0], sheet, ids));
                Log($"{string.Join(", ", group.Select(g => g.Key.name))} <- {Path.GetFileName(group.Key)}" + (smooth ? "" : " (no smooth)") + (group.First().Value.owner == ModId ? "" : $" (from {group.First().Value.owner})"));
            }
            if (palettes.Count == 0) return;
            WritePalettes(packs, palettes);
        }
        catch (Exception e) { Log("error: " + e); }
    }

    void Log(string msg) => _log.WriteLine($"[FFT Sprite Porter] {msg}");

    string PrepareCache()
    {
        var dir = Path.Combine(_loader.GetDirectoryForModId(ModId), "Cache");
        Directory.CreateDirectory(dir);
        foreach (var old in Directory.GetFiles(dir, "tex_*.bin")) File.Delete(old);
        return dir;
    }

    static void AddSheet(IFFTOModPackManager packs, string cacheDir, int tex, byte[] data)
    {
        var local = Path.Combine(cacheDir, $"tex_{tex}.bin");
        File.WriteAllBytes(local, data);
        packs.AddModdedFile(ModId, FFTOGameMode.Enhanced, local, $"system/ffto/g2d/tex_{tex}.bin");
    }

    List<string> EnabledMods()
    {
        try { return _loader.GetAppConfig().EnabledMods.ToList(); }
        catch (Exception e) { Log("could not read the enabled mods: " + e.Message); return new List<string>(); }
    }

    List<(string id, string dir)> EnabledModFolders()
    {
        var found = new List<(string id, string dir, int order)>();
        try
        {
            var enabled = EnabledMods();
            var modsDir = Path.GetDirectoryName(_loader.GetDirectoryForModId(ModId).TrimEnd('\\', '/'));
            foreach (var dir in Directory.GetDirectories(modsDir))
            {
                var config = Path.Combine(dir, "ModConfig.json");
                if (!File.Exists(config)) continue;
                string id;
                try { id = JsonDocument.Parse(File.ReadAllText(config)).RootElement.GetProperty("ModId").GetString(); }
                catch { continue; }
                int order = enabled.IndexOf(id);
                if (id != ModId && order >= 0) found.Add((id, dir, order));
            }
        }
        catch (Exception e) { Log("could not look through the mods folder: " + e.Message); }
        return found.OrderBy(f => f.order).Select(f => (f.id, f.dir)).ToList();
    }

    List<(string owner, string dir)> SpriteModFolders() =>
        EnabledModFolders().Select(m => (m.id, Path.Combine(m.dir, SpritesDir))).Where(m => Directory.Exists(m.Item2)).ToList();

    class Target { public string Folder; public List<Character> Characters; }

    static string Normalize(string s) => new string(s.ToLowerInvariant().Where(ch => ch < 128 && char.IsLetterOrDigit(ch)).ToArray());

    static string FolderFor(Character c) => c.group == "Generic jobs" ? "Job - " + c.name : c.name;

    static List<Character> ReadCharacters(string resource)
    {
        using var s = Assembly.GetExecutingAssembly().GetManifestResourceStream(resource);
        return JsonSerializer.Deserialize<List<Character>>(s);
    }

    static Dictionary<string, Target> LoadTargets(List<string> enabledMods)
    {
        var chars = ReadCharacters("characters.json");
        chars.AddRange(ReadCharacters("extra_characters.json").Where(c => c.mods.Any(enabledMods.Contains)));
        var targets = new Dictionary<string, Target>();
        void Add(string folder, IEnumerable<Character> who) => targets[Normalize(folder)] = new Target { Folder = folder, Characters = who.ToList() };
        foreach (var c in chars) Add(FolderFor(c), new[] { c });
        foreach (var name in new[] { "Ramza", "Delita" }) Add($"{name} (all chapters)", chars.Where(c => c.name.StartsWith(name + " (")));
        return targets;
    }

    void PrepareOwnFolder(string dir, Dictionary<string, Target> targets)
    {
        try
        {
            foreach (var t in targets.Values) Directory.CreateDirectory(Path.Combine(dir, t.Folder));
            var howTo = Path.Combine(dir, "HOW TO USE.txt");
            if (!File.Exists(howTo) || File.ReadAllText(howTo) != HowTo) File.WriteAllText(howTo, HowTo, new UTF8Encoding(false));
        }
        catch (Exception e) { Log("could not prepare the Sprites folder: " + e.Message); }
    }

    void WritePalettes(IFFTOModPackManager packs, List<(int key, Sheet sheet, List<int> ids)> palettes)
    {
        const string clutPath = "nxd/charclut.nxd", shapePath = "nxd/charshape.nxd";

        var clutLayout = TableMappingReader.ReadTableLayout("CharCLUT", new Version(1, 0, 0), "ffto");
        int dataCol = clutLayout.Columns.Keys.ToList().IndexOf("CLUTData");
        var clut = new NexDataFile(); clut.Read(packs.GetFileData(FFTOGameMode.Enhanced, clutPath));
        var rows = new SortedDictionary<(uint, uint), List<object>>();
        foreach (var r in clut.RowManager.GetAllRowInfos()) rows[(r.Key, r.Key2)] = NexUtils.ReadRow(clutLayout, clut.Buffer, r.RowDataOffset);
        var template = rows.First().Value;
        foreach (var (key, sheet, _) in palettes)
            for (uint v = 0; v < Sheet.Variants; v++)
            {
                var cells = rows.TryGetValue(((uint)key, v), out var existing) ? existing : new List<object>(template);
                cells[dataCol] = (byte[])sheet.Palettes[v].Clone();
                rows[((uint)key, v)] = cells;
            }
        var builder = new NexDataFileBuilder(clutLayout);
        foreach (var row in rows) builder.AddRow(row.Key.Item1, row.Key.Item2, 0, row.Value, false);
        var ms = new MemoryStream(); builder.Write(ms);
        packs.AddModdedFile(ModId, FFTOGameMode.Enhanced, clutPath, ms.ToArray());

        var shapeLayout = TableMappingReader.ReadTableLayout("CharShape", new Version(1, 0, 0), "ffto");
        int clutCol = shapeLayout.Columns.Keys.ToList().IndexOf("charclut+Id");
        var shape = new NexDataFile(); shape.Read(packs.GetFileData(FFTOGameMode.Enhanced, shapePath));
        var keyOf = palettes.SelectMany(p => p.ids.Select(id => (id, p.key))).ToDictionary(x => (uint)x.id, x => (uint)x.key);
        builder = new NexDataFileBuilder(shapeLayout);
        var present = new HashSet<uint>();
        foreach (var r in shape.RowManager.GetAllRowInfos())
        {
            var cells = NexUtils.ReadRow(shapeLayout, shape.Buffer, r.RowDataOffset);
            if (keyOf.TryGetValue(r.Key, out var key)) cells[clutCol] = key;
            builder.AddRow(r.Key, r.Key2, r.Key3, cells, false);
            present.Add(r.Key);
        }
        foreach (var (id, key) in keyOf.Where(k => !present.Contains(k.Key)).OrderBy(k => k.Key))
        {
            var cells = ShapeRowFromMods(nxd => nxd.RowManager.GetAllRowInfos().Where(r => r.Key == id).Select(r => NexUtils.ReadRow(shapeLayout, nxd.Buffer, r.RowDataOffset)).FirstOrDefault());
            if (cells == null) { Log($"sprite {id}: no mod defines its shape, colors not applied."); continue; }
            cells[clutCol] = key;
            builder.AddRow(id, 0, 0, cells, false);
        }
        ms = new MemoryStream(); builder.Write(ms);
        packs.AddModdedFile(ModId, FFTOGameMode.Enhanced, shapePath, ms.ToArray());
    }

    List<object> ShapeRowFromMods(Func<NexDataFile, List<object>> read)
    {
        List<object> row = null;
        foreach (var (_, dir) in EnabledModFolders())
        {
            var file = Path.Combine(dir, "FFTIVC", "data", "enhanced", "nxd", "charshape.nxd");
            if (!File.Exists(file)) continue;
            try
            {
                var nxd = new NexDataFile(); nxd.Read(File.ReadAllBytes(file));
                row = read(nxd) ?? row;
            }
            catch (Exception e) { Log($"could not read {file}: {e.Message}"); }
        }
        return row;
    }

    public void Suspend() { }
    public void Resume() { }
    public void Unload() { }
    public bool CanUnload() => false;
    public bool CanSuspend() => false;
    public Action Disposing { get; }
}
