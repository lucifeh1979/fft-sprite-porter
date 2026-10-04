using System.Drawing.Drawing2D;

static class Program {
  [STAThread]
  static int Main(string[] args) {
    if (args.Length == 7 && args[0] == "--cli") {
      try {
        var sheet = Sheet.Load(args[1]);
        var chars = args[6].Split(';').Select(n => Porter.Characters.First(c => c.name == n)).ToList();
        Porter.BuildMod(sheet, chars, args[3], args[4], args[5], Path.GetFileNameWithoutExtension(args[1]), args[2]);
        File.WriteAllText(args[2] + ".log", string.Join("\n", chars.Select(c => { var f = Porter.Fit(sheet, c); return $"{c.name}: missing {f.missing:0.0} extra {f.extra:0.0}"; })) + $"\npalettes {sheet.PalettesInFile}\n");
        return 0;
      } catch (Exception e) { File.WriteAllText(args[2] + ".log", "ERROR " + e); return 1; }
    }
    ApplicationConfiguration.Initialize();
    Application.Run(new MainForm(args.Length == 1 ? args[0] : null));
    return 0;
  }
}

class MainForm : Form {
  readonly TextBox txtSheet = new() { ReadOnly = true }, txtFilter = new() { PlaceholderText = "Type to filter..." };
  readonly TextBox txtName = new(), txtAuthor = new() { Text = "Lucifeh" }, txtArtist = new() { PlaceholderText = "Who drew the sprite (shown in the mod description)" };
  readonly CheckedListBox list = new() { CheckOnClick = true, IntegralHeight = false };
  readonly PictureBox preview = new() { BackColor = Color.FromArgb(200, 200, 210), BorderStyle = BorderStyle.FixedSingle, SizeMode = PictureBoxSizeMode.Zoom };
  readonly Label status = new() { AutoSize = false };
  readonly Button btnBrowse = new() { Text = "Browse..." }, btnCreate = new() { Text = "Create mod (.zip)..." };
  readonly HashSet<Character> selected = new();
  Sheet sheet; string sheetPath; bool nameEdited, filling;

  public MainForm(string initial) {
    Text = "FFT Sprite Porter - classic sprite sheets for The Ivalice Chronicles";
    Font = new Font("Segoe UI", 9f); AutoScaleMode = AutoScaleMode.Font; AutoScaleDimensions = new SizeF(7f, 15f);
    ClientSize = new Size(820, 640); FormBorderStyle = FormBorderStyle.FixedSingle; MaximizeBox = false; AllowDrop = true;
    StartPosition = FormStartPosition.CenterScreen;

    Add(new Label { Text = "1. Sprite sheet", Font = new Font(Font, FontStyle.Bold) }, 12, 12, 200, 18);
    Add(txtSheet, 12, 34, 700, 23); Add(btnBrowse, 720, 33, 88, 25);
    Add(new Label { Text = "A classic FFT sprite as shared on FFHacktics: a 256x488 indexed .bmp (or .png), or a .spr file. You can also drop the file on this window.", ForeColor = SystemColors.GrayText }, 12, 60, 796, 18);

    Add(new Label { Text = "2. Who gets this sprite?", Font = new Font(Font, FontStyle.Bold) }, 12, 90, 280, 18);
    Add(txtFilter, 12, 112, 280, 23); Add(list, 12, 140, 280, 362);
    Add(new Label { Text = "Tick one or more. [Job] = every unit of that job.", ForeColor = SystemColors.GrayText }, 12, 506, 280, 18);

    Add(new Label { Text = "Preview", Font = new Font(Font, FontStyle.Bold) }, 308, 90, 200, 18);
    Add(preview, 308, 112, 216, 412);

    Add(new Label { Text = "3. Mod details", Font = new Font(Font, FontStyle.Bold) }, 540, 90, 200, 18);
    Add(new Label { Text = "Mod name" }, 540, 114, 268, 18); Add(txtName, 540, 134, 268, 23);
    Add(new Label { Text = "Your name (mod author)" }, 540, 164, 268, 18); Add(txtAuthor, 540, 184, 268, 23);
    Add(new Label { Text = "Sprite artist" }, 540, 214, 268, 18); Add(txtArtist, 540, 234, 268, 23);
    Add(status, 540, 270, 268, 254);

    Add(new Label { Text = "The zip installs like any other mod: drag it into Reloaded-II and enable it. It needs the FFTIVC Mod Loader. Portraits are not changed.", ForeColor = SystemColors.GrayText }, 12, 540, 620, 36);
    Add(btnCreate, 648, 540, 160, 34);
    Add(new Label { Text = "Sprites belong to their artists. Credit them when you share a mod made with this tool.", ForeColor = SystemColors.GrayText }, 12, 606, 796, 18);

    FillList();
    btnBrowse.Click += (_, _) => { using var d = new OpenFileDialog { Filter = "Sprite sheets (*.bmp;*.png;*.spr)|*.bmp;*.png;*.spr|All files|*.*" }; if (d.ShowDialog(this) == DialogResult.OK) LoadSheet(d.FileName); };
    txtFilter.TextChanged += (_, _) => FillList();
    list.ItemCheck += (_, e) => { if (filling) return; var c = (Character)list.Items[e.Index]; if (e.NewValue == CheckState.Checked) selected.Add(c); else selected.Remove(c); BeginInvoke(Refresh2); };
    txtName.TextChanged += (_, _) => { if (!filling) nameEdited = txtName.Text != ""; };
    btnCreate.Click += (_, _) => Create();
    DragEnter += (_, e) => { if (e.Data.GetDataPresent(DataFormats.FileDrop)) e.Effect = DragDropEffects.Copy; };
    DragDrop += (_, e) => { if (e.Data.GetData(DataFormats.FileDrop) is string[] f && f.Length > 0) LoadSheet(f[0]); };
    Refresh2();
    if (initial != null) Shown += (_, _) => LoadSheet(initial);
  }

  void Add(Control c, int x, int y, int w, int h) { c.SetBounds(x, y, w, h); Controls.Add(c); }

  void FillList() {
    filling = true; list.BeginUpdate(); list.Items.Clear();
    foreach (var c in Porter.Characters.Where(c => c.ToString().Contains(txtFilter.Text.Trim(), StringComparison.OrdinalIgnoreCase)))
      list.Items.Add(c, selected.Contains(c));
    list.EndUpdate(); filling = false;
  }

  void LoadSheet(string path) {
    try {
      var s = Sheet.Load(path);
      sheet = s; sheetPath = path; txtSheet.Text = path;
      preview.Image?.Dispose(); preview.Image = s.Preview();
    } catch (Exception e) { MessageBox.Show(this, e.Message, "Can't use this file", MessageBoxButtons.OK, MessageBoxIcon.Warning); }
    Refresh2();
  }

  List<Character> Chosen() => Porter.Characters.Where(selected.Contains).ToList();

  void Refresh2() {
    var chars = Chosen();
    if (!nameEdited && sheet != null && chars.Count > 0) {
      filling = true; txtName.Text = $"{chars[0].name.Split(" (")[0]} as {Path.GetFileNameWithoutExtension(sheetPath)}"; filling = false;
    }
    var lines = new List<string>();
    if (sheet == null) lines.Add("Pick a sprite sheet to begin.");
    else {
      lines.Add(sheet.PalettesInFile > 1 ? $"Sheet OK. {sheet.PalettesInFile} palettes found: the extra ones become the alternate team colors." : "Sheet OK. Only one palette found, so every team uses the same colors.");
      foreach (var c in chars) {
        var (missing, extra) = Porter.Fit(sheet, c);
        if (missing > 8 || extra > 8) lines.Add($"⚠ {c.name}: this sheet's layout is far from the original ({missing:0}% of the original poses have no art). It is probably the wrong kind of sheet and will look broken.");
      }
      if (chars.Count == 0) lines.Add("Now tick who gets this sprite.");
    }
    status.Text = string.Join("\r\n\r\n", lines);
    btnCreate.Enabled = sheet != null && chars.Count > 0;
  }

  void Create() {
    string name = txtName.Text.Trim();
    if (name == "") { MessageBox.Show(this, "Give the mod a name.", Text); return; }
    using var d = new SaveFileDialog { Filter = "Zip|*.zip", FileName = string.Concat(name.Select(ch => Path.GetInvalidFileNameChars().Contains(ch) ? '-' : ch)).Replace(' ', '-') + "-1.0.0.zip", InitialDirectory = Path.GetDirectoryName(sheetPath) };
    if (d.ShowDialog(this) != DialogResult.OK) return;
    try {
      Porter.BuildMod(sheet, Chosen(), name, txtAuthor.Text.Trim(), txtArtist.Text.Trim(), Path.GetFileNameWithoutExtension(sheetPath), d.FileName);
      MessageBox.Show(this, "Mod created:\r\n" + d.FileName + "\r\n\r\nDrag the zip into the Reloaded-II window, enable the mod and launch the game.", Text, MessageBoxButtons.OK, MessageBoxIcon.Information);
    } catch (Exception e) { MessageBox.Show(this, e.Message, "Could not create the mod", MessageBoxButtons.OK, MessageBoxIcon.Error); }
  }
}
