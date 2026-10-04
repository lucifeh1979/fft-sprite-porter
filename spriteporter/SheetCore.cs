class Character {
  public string name { get; set; }
  public string group { get; set; }
  public int tex { get; set; }
  public int[] ids { get; set; }
  public override string ToString() => group == "Generic jobs" ? "[Job] " + name : name;
}

partial class Sheet {
  public const int W = 256, H = 488, Variants = 8;
  public byte[] Pixels = new byte[W * H];
  public byte[][] Palettes = new byte[Variants][];
  public int PalettesInFile;

  void FinishPalettes() {
    if (Palettes[0] == null) throw new Exception("The sprite has no colors in its first palette.");
    for (int k = 1; k < Variants; k++) Palettes[k] ??= Palettes[0];
  }

  public static Sheet LoadSpr(string path) {
    var d = File.ReadAllBytes(path);
    const int Raw = 512 + 288 * W / 2;
    if (d.Length < Raw) throw new Exception("This .spr file is too small to be a character sprite.");
    var s = new Sheet(); var raw = new byte[288 * W];
    for (int i = 0; i < raw.Length / 2; i++) { raw[i * 2] = (byte)(d[512 + i] & 15); raw[i * 2 + 1] = (byte)(d[512 + i] >> 4); }
    var comp = new byte[200 * W]; int n = 0, j = 0, total = (d.Length - Raw) * 2;
    int Nib(int k) => k % 2 == 0 ? d[Raw + k / 2] >> 4 : d[Raw + k / 2] & 15;
    while (j < total && n < comp.Length) {
      int v = Nib(j);
      if (v != 0) { comp[n++] = (byte)v; j++; continue; }
      if (j + 1 >= total) break;
      int code = Nib(j + 1), len; j += 2;
      if (code == 7) { if (j + 1 >= total) break; len = Nib(j) + (Nib(j + 1) << 4); j += 2; }
      else if (code == 8) { if (j + 2 >= total) break; len = Nib(j) + (Nib(j + 1) << 4) + (Nib(j + 2) << 8); j += 3; }
      else if (code == 0) { if (j >= total) break; len = Nib(j); j += 1; }
      else len = code;
      n = Math.Min(comp.Length, n + len);
    }
    for (int i = 0; i + 1 < comp.Length; i += 2) (comp[i], comp[i + 1]) = (comp[i + 1], comp[i]);
    Buffer.BlockCopy(raw, 0, s.Pixels, 0, 256 * W);
    Buffer.BlockCopy(comp, 0, s.Pixels, 256 * W, 200 * W);
    Buffer.BlockCopy(raw, 256 * W, s.Pixels, 456 * W, 32 * W);
    for (int k = 0; k < Variants; k++) {
      var p = new byte[48]; bool any = false;
      for (int i = 0; i < 16; i++) {
        int c = d[(k * 16 + i) * 2] | d[(k * 16 + i) * 2 + 1] << 8;
        p[i * 3] = (byte)((c & 31) << 3); p[i * 3 + 1] = (byte)((c >> 5 & 31) << 3); p[i * 3 + 2] = (byte)((c >> 10 & 31) << 3);
        any |= (c & 0x7FFF) != 0;
      }
      if (any) { s.Palettes[k] = p; s.PalettesInFile = k + 1; }
    }
    s.FinishPalettes();
    return s;
  }

  public static Sheet LoadBmp(string path) {
    var d = File.ReadAllBytes(path);
    if (d.Length < 54 || d[0] != 'B' || d[1] != 'M') return null;
    int dataAt = BitConverter.ToInt32(d, 10), header = BitConverter.ToInt32(d, 14), w = BitConverter.ToInt32(d, 18), h = BitConverter.ToInt32(d, 22);
    int bpp = BitConverter.ToUInt16(d, 28), compression = header >= 40 ? BitConverter.ToInt32(d, 30) : 0, used = header >= 40 ? BitConverter.ToInt32(d, 46) : 0;
    if (header < 40 || compression != 0) return null;
    if (bpp != 8 && bpp != 4) throw new Exception("This image is not indexed (16 or 256 colors). FFHacktics sprite sheets are 8-bit .bmp files - a screenshot or a true-color export will not work.");
    bool topDown = h < 0; h = Math.Abs(h);
    if (w != W || h < H) throw new Exception($"The sheet must be {W}x{H} pixels. This one is {w}x{h}.");
    int colors = used > 0 ? used : 1 << bpp, palAt = 14 + header, stride = (w * bpp + 31) / 32 * 4;
    if (palAt + colors * 4 > d.Length || dataAt + stride * h > d.Length) return null;
    var s = new Sheet();
    for (int y = 0; y < H; y++) {
      int row = dataAt + (topDown ? y : h - 1 - y) * stride;
      for (int x = 0; x < W; x++) {
        int v = bpp == 8 ? d[row + x] : (x % 2 == 0 ? d[row + x / 2] >> 4 : d[row + x / 2] & 15);
        s.Pixels[y * W + x] = (byte)(v < 16 ? v : 0);
      }
    }
    for (int k = 0; k < Variants; k++) {
      var p = new byte[48]; bool has = colors >= k * 16 + 16, any = false;
      for (int i = 0; has && i < 16; i++) {
        int c = palAt + (k * 16 + i) * 4; p[i * 3] = d[c + 2]; p[i * 3 + 1] = d[c + 1]; p[i * 3 + 2] = d[c];
        any |= d[c] + d[c + 1] + d[c + 2] > 0;
      }
      if (has && any) { s.Palettes[k] = p; s.PalettesInFile = k + 1; }
    }
    s.FinishPalettes();
    return s;
  }

  public (byte[] top, byte[] bottom) ToHd() {
    var all = new byte[W * H * 2];
    for (int y = 0; y < H; y++)
      for (int x = 0; x < W; x++) {
        byte v = Pixels[y * W + x], b = (byte)(v | v << 4);
        all[(y * 2) * W + x] = b; all[(y * 2 + 1) * W + x] = b;
      }
    return (all[..131072], all[131072..]);
  }
}
