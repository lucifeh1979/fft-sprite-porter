using System.Drawing.Imaging;
using System.IO.Compression;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;


partial class Sheet {
  public static Sheet Load(string path) {
    string ext = Path.GetExtension(path).ToLowerInvariant();
    if (ext == ".spr") return LoadSpr(path);
    return (ext == ".bmp" ? LoadBmp(path) : null) ?? LoadImage(path);
  }

  static Sheet LoadImage(string path) {
    using var bmp = new Bitmap(path);
    bool b8 = bmp.PixelFormat == PixelFormat.Format8bppIndexed, b4 = bmp.PixelFormat == PixelFormat.Format4bppIndexed;
    if (!b8 && !b4) throw new Exception("This image is not indexed (16 or 256 colors). FFHacktics sprite sheets are 8-bit .bmp files - a screenshot or a true-color export will not work.");
    if (bmp.Width != W || bmp.Height < H) throw new Exception($"The sheet must be {W}x{H} pixels. This one is {bmp.Width}x{bmp.Height}.");
    var s = new Sheet();
    var d = bmp.LockBits(new Rectangle(0, 0, W, H), ImageLockMode.ReadOnly, bmp.PixelFormat);
    try {
      var row = new byte[d.Stride];
      for (int y = 0; y < H; y++) {
        Marshal.Copy(d.Scan0 + y * d.Stride, row, 0, d.Stride);
        for (int x = 0; x < W; x++) {
          int v = b8 ? row[x] : (x % 2 == 0 ? row[x / 2] >> 4 : row[x / 2] & 15);
          s.Pixels[y * W + x] = (byte)(v < 16 ? v : 0);
        }
      }
    } finally { bmp.UnlockBits(d); }
    var pal = bmp.Palette.Entries;
    for (int k = 0; k < Variants; k++) {
      var p = new byte[48]; bool has = pal.Length >= k * 16 + 16, any = false;
      for (int i = 0; has && i < 16; i++) {
        var c = pal[k * 16 + i]; p[i * 3] = c.R; p[i * 3 + 1] = c.G; p[i * 3 + 2] = c.B;
        any |= c.R + c.G + c.B > 0;
      }
      if (has && any) { s.Palettes[k] = p; s.PalettesInFile = k + 1; }
    }
    if (s.Palettes[0] == null) throw new Exception("The sheet has no colors in its first palette.");
    for (int k = 1; k < Variants; k++) s.Palettes[k] ??= s.Palettes[0];
    return s;
  }

  public Bitmap Preview(int variant = 0) {
    var bmp = new Bitmap(W, H, PixelFormat.Format32bppArgb);
    var px = new int[W * H]; var p = Palettes[variant];
    for (int i = 0; i < px.Length; i++) { int v = Pixels[i]; px[i] = v == 0 ? 0 : unchecked((int)0xFF000000) | p[v * 3] << 16 | p[v * 3 + 1] << 8 | p[v * 3 + 2]; }
    var d = bmp.LockBits(new Rectangle(0, 0, W, H), ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);
    Marshal.Copy(px, 0, d.Scan0, px.Length); bmp.UnlockBits(d);
    return bmp;
  }
}

static class Porter {
  public const int CW = 128, CH = 244;
  static readonly Dictionary<string, byte[]> data = new();
  public static List<Character> Characters;
  static Dictionary<string, int> shapeOffsets;

  static Porter() {
    using var z = new ZipArchive(typeof(Porter).Assembly.GetManifestResourceStream("data.zip"));
    foreach (var e in z.Entries) { using var s = e.Open(); using var m = new MemoryStream(); s.CopyTo(m); data[e.Name] = m.ToArray(); }
    Characters = JsonSerializer.Deserialize<List<Character>>(data["characters.json"]);
    shapeOffsets = JsonSerializer.Deserialize<Dictionary<string, int>>(data["shape.json"]);
  }

  static bool[] Dilate2(bool[] m) {
    var o = new bool[m.Length];
    for (int y = 0; y < CH; y++) for (int x = 0; x < CW; x++) if (m[y * CW + x])
      for (int dy = -2; dy <= 2; dy++) for (int dx = -2; dx <= 2; dx++) {
        int yy = y + dy, xx = x + dx; if (yy >= 0 && yy < CH && xx >= 0 && xx < CW) o[yy * CW + xx] = true;
      }
    return o;
  }

  public static (double missing, double extra) Fit(Sheet s, Character c) {
    var pk = data[$"mask_{c.tex}.bin"]; var o = new bool[CW * CH]; var n = new bool[CW * CH];
    for (int i = 0; i < o.Length; i++) o[i] = (pk[i / 8] >> (7 - i % 8) & 1) != 0;
    for (int y = 0; y < Sheet.H; y++) for (int x = 0; x < Sheet.W; x++) if (s.Pixels[y * Sheet.W + x] != 0) n[y / 2 * CW + x / 2] = true;
    for (int y = 228; y < CH; y++) for (int x = 40; x < 64; x++) { o[y * CW + x] = false; n[y * CW + x] = false; }
    var od = Dilate2(o); var nd = Dilate2(n); int ot = 0, nt = 0, miss = 0, extra = 0;
    for (int i = 0; i < o.Length; i++) { if (o[i]) { ot++; if (!nd[i]) miss++; } if (n[i]) { nt++; if (!od[i]) extra++; } }
    return (ot == 0 ? 0 : 100.0 * miss / ot, nt == 0 ? 100 : 100.0 * extra / nt);
  }

  public static string Slug(string s) {
    var b = new StringBuilder(); foreach (char ch in s.ToLowerInvariant()) if (ch < 128 && char.IsLetterOrDigit(ch)) b.Append(ch);
    return b.Length == 0 ? "customsprite" : b.ToString();
  }

  static int Find(byte[] hay, byte[] needle) {
    for (int i = 0; i <= hay.Length - needle.Length; i++) { int j = 0; while (j < needle.Length && hay[i + j] == needle[j]) j++; if (j == needle.Length) return i; }
    return -1;
  }

  public static void BuildMod(Sheet sheet, IList<Character> chars, string modName, string author, string artist, string sheetName, string outZip) {
    if (chars.Count == 0) throw new Exception("Pick at least one character.");
    var ids = chars.SelectMany(c => c.ids).Distinct().OrderBy(i => i).ToList();
    int k = ids[0];
    var clut = (byte[])data[$"clut_{k}.nxd"].Clone();
    for (int v = 0; v < Sheet.Variants; v++) {
      var marker = Encoding.ASCII.GetBytes(string.Concat(Enumerable.Repeat($"FFTSPRITEPORTER-PALETTE-SLOT-{v}-", 2)))[..48];
      int at = Find(clut, marker); if (at < 0) throw new Exception("Internal error: palette slot not found.");
      Buffer.BlockCopy(sheet.Palettes[v], 0, clut, at, 48);
    }
    var shape = (byte[])data["charshape.nxd"].Clone();
    foreach (int id in ids) BitConverter.GetBytes((uint)k).CopyTo(shape, shapeOffsets[id.ToString()]);
    var (top, bottom) = sheet.ToHd();
    string modId = "fftivc.sprite." + Slug(modName), who = string.Join(", ", chars.Select(c => c.name));
    var cfg = new JsonObject {
      ["ModId"] = modId, ["ModName"] = modName, ["ModAuthor"] = author, ["ModVersion"] = "1.0.0",
      ["ModDescription"] = $"Replaces the battle sprite of {who} with \"{sheetName}\"" + (artist == "" ? "" : $" by {artist}") + ", a sprite made for the original game, ported to the Enhanced version with FFT Sprite Porter. Sprite and palette only: portraits, stats and abilities are untouched.",
      ["ModDll"] = "", ["ModIcon"] = "", ["ModR2RManagedDll32"] = "", ["ModR2RManagedDll64"] = "", ["ModNativeDll32"] = "", ["ModNativeDll64"] = "",
      ["Tags"] = new JsonArray(), ["CanUnload"] = false, ["HasExports"] = false, ["IsLibrary"] = false, ["ReleaseMetadataFileName"] = "",
      ["IgnoreRegexes"] = new JsonArray(), ["IncludeRegexes"] = new JsonArray(), ["PluginData"] = new JsonObject(), ["IsUniversalMod"] = false,
      ["ModDependencies"] = new JsonArray("fftivc.utility.modloader"), ["OptionalDependencies"] = new JsonArray(),
      ["SupportedAppId"] = new JsonArray("fft_enhanced.exe"), ["ProjectUrl"] = ""
    };
    if (File.Exists(outZip)) File.Delete(outZip);
    using var z = ZipFile.Open(outZip, ZipArchiveMode.Create);
    void Add(string name, byte[] bytes) { using var s = z.CreateEntry($"{modId}/{name}", CompressionLevel.Optimal).Open(); s.Write(bytes); }
    Add("ModConfig.json", Encoding.UTF8.GetBytes(cfg.ToJsonString(new JsonSerializerOptions { WriteIndented = true })));
    foreach (int tex in chars.Select(c => c.tex).Distinct()) {
      Add($"FFTIVC/data/enhanced/system/ffto/g2d/tex_{tex}.bin", top);
      Add($"FFTIVC/data/enhanced/system/ffto/g2d/tex_{tex + 1}.bin", bottom);
    }
    Add("FFTIVC/data/enhanced/nxd/charclut.nxd", clut);
    Add("FFTIVC/data/enhanced/nxd/charshape.nxd", shape);
  }
}
