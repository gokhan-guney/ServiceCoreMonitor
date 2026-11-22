using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Media;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using System.Windows.Forms;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;

namespace ServiceCoreMonitor {
  public class Settings {
    public string HomeUrl { get; set; }
    public string[] Urls { get; set; }
    public string[] Rows { get; set; }
    public string[] Keys { get; set; }
    public int Minutes { get; set; }
    public int Seconds { get; set; }
    public string SoundMode { get; set; }
    public string SoundFile { get; set; }
    public bool RememberLinks { get; set; }
    public bool Enabled { get; set; }
    public Settings() {
      HomeUrl = "";
      Urls = new string[3];
      Rows = new[] { "table tbody tr", "table tbody tr", "table tbody tr" };
      Keys = new[] { "a[href]", "a[href]", "a[href]" }; Minutes = 1; Seconds = 10;
      SoundMode = "default"; SoundFile = ""; RememberLinks = false;
    }
  }

  public class MainForm : Form {
    static readonly string[] Names = { "Çağrı", "Olay", "İstek" };
    static readonly string[] Prefixes = { "INT", "INC", "SR" };
    static readonly string[] ListPaths = { "/interaction/IndexV2", "/Ticket/IndexV2", "/TicketServiceRequest/IndexV2" };
    readonly string statePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "ServiceCoreMonitor", "state.json");
    readonly JavaScriptSerializer json = new JavaScriptSerializer();
    readonly TextBox home = new TextBox();
    readonly CheckBox rememberLinks = new CheckBox();
    readonly TextBox[] urls = new TextBox[3];
    readonly Label[] statuses = new Label[3];
    readonly WebView2[] views = new WebView2[4];
    readonly NumericUpDown seconds = new NumericUpDown();
    readonly ComboBox soundChoice = new ComboBox();
    readonly Label soundFileLabel = new Label();
    SoundPlayer customPlayer;
    readonly Button toggle = new Button(), check = new Button(), testNotification = new Button();
    readonly TextBox log = new TextBox();
    readonly TabControl tabs = new TabControl();
    readonly NotifyIcon tray = new NotifyIcon();
    readonly Timer timer = new Timer();
    readonly Dictionary<string, long> highWatermarks = new Dictionary<string, long>();
    Settings settings = new Settings();
    CoreWebView2Environment browserEnvironment;
    bool busy, initialized, exiting;
    string notificationUrl;

    public MainForm() {
      Text = "ServiceCore Kayıt Bildirimi · @gokhanguney"; Size = new Size(1120, 770); MinimumSize = new Size(1040, 620);
      Font = new Font("Segoe UI", 9F); StartPosition = FormStartPosition.CenterScreen;
      LoadState(); ConfigureBundledSound(); BuildUi(); ApplySettings();
      views[0].NavigationCompleted += async (s, e) => { if (e.IsSuccess) await DiscoverPages(); };
      timer.Tick += async (s, e) => await CheckAll();
      try { Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath); } catch { Icon = SystemIcons.Information; }
      tray.Icon = Icon; tray.Text = "ServiceCore Kayıt Bildirimi"; tray.Visible = true;
      var menu = new ContextMenuStrip(); menu.Items.Add("Göster", null, (s, e) => ShowWindow());
      menu.Items.Add("Şimdi kontrol et", null, async (s, e) => await CheckAll());
      menu.Items.Add("Çıkış", null, (s, e) => { exiting = true; Close(); });
      tray.ContextMenuStrip = menu;
      tray.DoubleClick += (s, e) => ShowWindow();
      tray.BalloonTipClicked += (s, e) => { ShowWindow(); if (!String.IsNullOrEmpty(notificationUrl)) views[0].CoreWebView2.Navigate(notificationUrl); };
      Shown += async (s, e) => await InitializeBrowser();
      FormClosing += (s, e) => { if (!exiting) { e.Cancel = true; Hide(); tray.ShowBalloonTip(1800, "ServiceCore", "İzleme sistem tepsisinde sürüyor.", ToolTipIcon.Info); } };
      FormClosed += (s, e) => { timer.Stop(); if (customPlayer != null) customPlayer.Dispose(); tray.Visible = false; tray.Dispose(); };
    }

    void BuildUi() {
      BackColor = Color.FromArgb(240, 244, 250);
      var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 3, Margin = new Padding(0) };
      layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 68));
      layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 158));
      layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
      Controls.Add(layout);
      var header = new Panel { Dock = DockStyle.Fill, BackColor = Color.FromArgb(20, 38, 70) };
      header.Controls.Add(new Label { Text = "ServiceCore Kayıt Bildirimi", ForeColor = Color.White,
        Font = new Font("Segoe UI Semibold", 17F), Left = 18, Top = 8, Width = 470, Height = 35 });
      header.Controls.Add(new Label { Text = "Çağrı · Olay · İstek takipçisi", ForeColor = Color.FromArgb(179, 202, 232),
        Left = 20, Top = 42, Width = 350, Height = 19 });
      var author = new Label { Text = "Gökhan Güney  ·  @gokhanguney", ForeColor = Color.FromArgb(179, 202, 232),
        AutoSize = true, Top = 27, Left = 805, Anchor = AnchorStyles.Top | AnchorStyles.Right };
      header.Controls.Add(author); layout.Controls.Add(header, 0, 0);

      var top = new Panel { Dock = DockStyle.Fill, BackColor = Color.White, Padding = new Padding(12) };
      layout.Controls.Add(top, 0, 1);
      top.Controls.Add(new Label { Text = "Uygulama adresi", Left = 16, Top = 13, Width = 130, ForeColor = Color.FromArgb(46, 64, 89) });
      home.SetBounds(148, 8, 650, 28); home.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
      top.Controls.Add(home);
      var go = new Button { Text = "Sayfayı aç", Left = 810, Top = 7, Width = 120, Height = 30, Anchor = AnchorStyles.Top | AnchorStyles.Right };
      StyleButton(go, Color.FromArgb(37, 90, 175));
      go.Click += (s, e) => NavigateHome(); top.Controls.Add(go);
      rememberLinks.Text = "Bağlantıları hatırla"; rememberLinks.SetBounds(16, 48, 165, 28);
      rememberLinks.CheckedChanged += (s, e) => { settings.RememberLinks = rememberLinks.Checked; SaveState(); };
      top.Controls.Add(rememberLinks);
      top.Controls.Add(new Label { Text = "Kontrol aralığı", Left = 190, Top = 55, Width = 98 });
      seconds.SetBounds(290, 48, 60, 28); seconds.Minimum = 10; seconds.Maximum = 300; top.Controls.Add(seconds);
      top.Controls.Add(new Label { Text = "saniye", Left = 355, Top = 55, Width = 52 });
      toggle.SetBounds(420, 46, 160, 32); StyleButton(toggle, Color.FromArgb(17, 128, 103));
      toggle.Click += async (s, e) => { if (!SaveInputs()) return; settings.Enabled = !settings.Enabled; SaveState(); UpdateTimer(); if (settings.Enabled) await CheckAll(); }; top.Controls.Add(toggle);
      check.Text = "Şimdi kontrol et"; check.SetBounds(590, 46, 140, 32); StyleButton(check, Color.FromArgb(37, 90, 175));
      check.Click += async (s, e) => { if (SaveInputs()) await CheckAll(); }; top.Controls.Add(check);
      testNotification.Text = "Test bildirimi"; testNotification.SetBounds(740, 46, 135, 32);
      StyleButton(testNotification, Color.FromArgb(88, 104, 130));
      testNotification.Click += (s, e) => Notify("Test bildirimi", "Bildirim ekranı çalışıyor. Kayıt algılamasını Durum sekmesinden kontrol edin.", settings.HomeUrl);
      top.Controls.Add(testNotification);
      top.Controls.Add(new Label { Text = "Bildirim sesi", Left = 16, Top = 98, Width = 125 });
      soundChoice.SetBounds(148, 91, 170, 28); soundChoice.DropDownStyle = ComboBoxStyle.DropDownList;
      soundChoice.Items.AddRange(new object[] { "Windows varsayılan", "Yıldız", "Uyarı", "Kritik", "Bip", "Sessiz", "Özel WAV" });
      soundChoice.SelectedIndexChanged += (s, e) => { if (soundChoice.SelectedIndex < 0) return; settings.SoundMode = SoundCode(); SaveState(); };
      top.Controls.Add(soundChoice);
      var browseSound = new Button { Text = "WAV seç", Left = 328, Top = 90, Width = 95, Height = 31 };
      browseSound.Click += (s, e) => BrowseSound(); top.Controls.Add(browseSound);
      var playSound = new Button { Text = "Sesi dene", Left = 433, Top = 90, Width = 100, Height = 31 };
      playSound.Click += (s, e) => PlayNotificationSound(true); top.Controls.Add(playSound);
      soundFileLabel.SetBounds(545, 98, 380, 25); soundFileLabel.AutoEllipsis = true;
      top.Controls.Add(soundFileLabel);
      top.Controls.Add(new Label { Text = "Uygulama adresini açıp oturum açın; üç liste adresi otomatik doldurulur.",
        Left = 16, Top = 130, AutoSize = true, ForeColor = Color.FromArgb(84, 100, 121) });
      tabs.Dock = DockStyle.Fill; layout.Controls.Add(tabs, 0, 2);
      for (int i = 0; i < 4; i++) {
        var page = new TabPage(i == 0 ? "Giriş / Kayıt Aç" : Names[i - 1]) { BackColor = Color.White }; tabs.TabPages.Add(page);
        views[i] = new WebView2 { Dock = DockStyle.Fill }; page.Controls.Add(views[i]);
        if (i == 0) continue;
        int index = i - 1;
        var config = new TableLayoutPanel { Dock = DockStyle.Top, Height = 65, ColumnCount = 2, RowCount = 2, Padding = new Padding(6) };
        config.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 175)); config.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        page.Controls.Add(config); config.BringToFront();
        urls[index] = AddField(config, 0, "Liste adresi:");
        statuses[index] = new Label { Text = "Henüz kontrol edilmedi", AutoSize = true, ForeColor = Color.DarkSlateBlue };
        config.Controls.Add(statuses[index], 1, 1);
      }
      var report = new TabPage("Durum") { BackColor = Color.White }; tabs.TabPages.Add(report);
      log.Multiline = true; log.ReadOnly = true; log.ScrollBars = ScrollBars.Vertical; log.Dock = DockStyle.Fill;
      log.Font = new Font("Consolas", 10F); report.Controls.Add(log);
      log.AppendText("Kontrol sonuçları burada görünecek.\r\n");
    }

    void StyleButton(Button button, Color color) {
      button.FlatStyle = FlatStyle.Flat; button.FlatAppearance.BorderSize = 0;
      button.BackColor = color; button.ForeColor = Color.White; button.Cursor = Cursors.Hand;
    }

    TextBox AddField(TableLayoutPanel panel, int row, string label) {
      panel.Controls.Add(new Label { Text = label, AutoSize = true, Anchor = AnchorStyles.Left }, 0, row);
      var box = new TextBox { Dock = DockStyle.Fill }; panel.Controls.Add(box, 1, row); return box;
    }

    void ApplySettings() {
      home.Text = settings.HomeUrl ?? ""; seconds.Value = Math.Max(10, Math.Min(300, settings.Seconds));
      rememberLinks.Checked = settings.RememberLinks;
      string[] modes = { "default", "asterisk", "exclamation", "hand", "beep", "silent", "custom" };
      int mode = Array.IndexOf(modes, settings.SoundMode ?? "default"); soundChoice.SelectedIndex = mode < 0 ? 0 : mode;
      UpdateSoundFileLabel();
      for (int i = 0; i < 3; i++) {
        urls[i].Text = settings.Urls != null && settings.Urls.Length > i ? settings.Urls[i] : "";
      }
      UpdateTimer();
    }

    bool ValidUrl(string text) { Uri uri; return Uri.TryCreate(text, UriKind.Absolute, out uri) && (uri.Scheme == "http" || uri.Scheme == "https"); }
    void NavigateHome() {
      if (!initialized) return;
      if (!ValidUrl(home.Text.Trim())) { MessageBox.Show("Geçerli bir http veya https adresi girin."); return; }
      var site = new Uri(home.Text.Trim());
      for (int i = 0; i < 3; i++) {
        Uri old;
        if (Uri.TryCreate(urls[i].Text, UriKind.Absolute, out old) &&
            !String.Equals(old.Host, site.Host, StringComparison.OrdinalIgnoreCase)) {
          urls[i].Clear(); settings.Urls[i] = ""; highWatermarks.Remove(i.ToString());
        }
      }
      settings.HomeUrl = home.Text.Trim(); SaveState(); tabs.SelectedIndex = 0; views[0].CoreWebView2.Navigate(settings.HomeUrl);
    }

    async Task DiscoverPages() {
      try {
        Uri site;
        if (!Uri.TryCreate(home.Text.Trim(), UriKind.Absolute, out site)) return;
        Uri current = new Uri(views[0].CoreWebView2.Source);
        if (!String.Equals(site.Host, current.Host, StringComparison.OrdinalIgnoreCase)) return;
        await Task.Delay(700);
        string script = "Array.from(document.querySelectorAll('a[href]')).map(a => a.href).filter(Boolean)";
        string result = await views[0].CoreWebView2.ExecuteScriptAsync(script);
        var links = json.DeserializeObject(result) as object[] ?? new object[0];
        int added = 0;
        for (int i = 0; i < 3; i++) {
          if (!String.IsNullOrWhiteSpace(urls[i].Text)) continue;
          string found = null;
          foreach (var item in links) {
            Uri candidate;
            if (!Uri.TryCreate(Convert.ToString(item), UriKind.Absolute, out candidate)) continue;
            if (!String.Equals(candidate.Host, site.Host, StringComparison.OrdinalIgnoreCase)) continue;
            if (String.Equals(candidate.AbsolutePath.TrimEnd('/'), ListPaths[i], StringComparison.OrdinalIgnoreCase)) {
              found = candidate.GetLeftPart(UriPartial.Path) + candidate.Query; break;
            }
          }
          urls[i].Text = found ?? site.GetLeftPart(UriPartial.Authority).TrimEnd('/') + ListPaths[i];
          settings.Urls[i] = urls[i].Text;
          added++;
        }
        if (added > 0) {
          SaveState();
          log.AppendText(DateTime.Now.ToString("HH:mm:ss") + "  Çağrı, Olay ve İstek bağlantıları dolduruldu.\r\n");
        }
      } catch (Exception ex) {
        log.AppendText("Liste bağlantıları bulunamadı: " + ex.Message + "\r\n");
      }
    }

    bool SaveInputs() {
      var next = new Settings { HomeUrl = home.Text.Trim(), Seconds = (int)seconds.Value, Enabled = settings.Enabled,
        SoundMode = SoundCode(), SoundFile = settings.SoundFile, RememberLinks = rememberLinks.Checked };
      for (int i = 0; i < 3; i++) {
        next.Urls[i] = urls[i].Text.Trim();
        if (!ValidUrl(next.Urls[i])) { MessageBox.Show(Names[i] + " için geçerli bir liste adresi girin."); tabs.SelectedIndex = i + 1; return false; }
        if (settings.Urls == null || settings.Urls.Length <= i || settings.Urls[i] != next.Urls[i]) highWatermarks.Remove(i.ToString());
      }
      settings = next; SaveState(); UpdateTimer(); return true;
    }

    void UpdateTimer() {
      timer.Stop(); timer.Interval = Math.Max(10, Math.Min(300, settings.Seconds)) * 1000;
      if (settings.Enabled && initialized) timer.Start();
      if (toggle != null) toggle.Text = settings.Enabled ? "İzlemeyi durdur" : "İzlemeyi başlat";
    }

    async Task InitializeBrowser() {
      try {
        string folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ServiceCoreMonitor", "Browser");
        Directory.CreateDirectory(folder);
        browserEnvironment = await CoreWebView2Environment.CreateAsync(null, folder);
        foreach (var view in views) await view.EnsureCoreWebView2Async(browserEnvironment);
        initialized = true;
        if (ValidUrl(settings.HomeUrl)) views[0].CoreWebView2.Navigate(settings.HomeUrl);
        UpdateTimer();
        if (settings.Enabled) await CheckAll();
      } catch (Exception ex) { MessageBox.Show("Gömülü tarayıcı başlatılamadı. Microsoft Edge WebView2 Runtime kurulu olmalı.\n\n" + ex.Message); }
    }

    async Task CheckAll() {
      if (!initialized || busy) return;
      busy = true; check.Enabled = false;
      try {
        var tasks = new List<Task>();
        for (int i = 0; i < 3; i++) {
          if (!ValidUrl(settings.Urls[i])) { Status(i, "Liste adresi eksik."); continue; }
          tasks.Add(CheckCategory(i));
        }
        await Task.WhenAll(tasks);
      } finally { busy = false; check.Enabled = true; }
    }

    async Task CheckCategory(int index) {
      var view = views[index + 1];
      try {
        Status(index, "Kontrol ediliyor...");
        var loaded = new TaskCompletionSource<bool>();
        EventHandler<CoreWebView2NavigationCompletedEventArgs> handler = null;
        handler = (s, e) => { view.NavigationCompleted -= handler; loaded.TrySetResult(e.IsSuccess); };
        view.NavigationCompleted += handler;
        view.CoreWebView2.Navigate(settings.Urls[index]);
        if (await Task.WhenAny(loaded.Task, Task.Delay(20000)) != loaded.Task) { view.NavigationCompleted -= handler; Status(index, "Sayfa zaman aşımı; VPN/oturumu kontrol edin."); return; }
        if (!await loaded.Task) { Status(index, "Sayfa açılamadı; VPN/oturumu kontrol edin."); return; }
        var expected = new Uri(settings.Urls[index]);
        var actual = new Uri(view.CoreWebView2.Source);
        if (!String.Equals(expected.Host, actual.Host, StringComparison.OrdinalIgnoreCase) ||
            !String.Equals(expected.AbsolutePath.TrimEnd('/'), actual.AbsolutePath.TrimEnd('/'), StringComparison.OrdinalIgnoreCase)) {
          Status(index, "Liste yerine " + actual.AbsolutePath + " açıldı; oturumu kontrol edin."); return;
        }

        object[] numbers = null;
        bool empty = false;
        string script = @"(function(prefix){
          var text = document.body ? document.body.innerText : '';
          var re = new RegExp('\\b' + prefix + '\\s*(\\d+)\\b', 'gi');
          var matches = [], match;
          while ((match = re.exec(text)) !== null) matches.push(Number(match[1]));
          return {numbers: Array.from(new Set(matches)), empty: /Veri\s+Yok/i.test(text)};
        })" + "(" + json.Serialize(Prefixes[index]) + ")";
        for (int attempt = 0; attempt < 5; attempt++) {
          await Task.Delay(1000);
          string result = await view.CoreWebView2.ExecuteScriptAsync(script);
          var data = json.DeserializeObject(result) as Dictionary<string, object>;
          if (data == null) continue;
          numbers = data["numbers"] as object[];
          empty = Convert.ToBoolean(data["empty"]);
          if ((numbers != null && numbers.Length > 0) || empty) break;
        }
        if (numbers == null) numbers = new object[0];
        if (numbers.Length == 0 && !empty) { Status(index, "Kayıt numarası ve 'Veri Yok' yazısı bulunamadı; sayfayı kontrol edin."); return; }

        string id = index.ToString();
        bool first = !highWatermarks.ContainsKey(id);
        long previous = first ? 0 : highWatermarks[id];
        var visible = numbers.Select(n => Convert.ToInt64(n)).Distinct().OrderBy(n => n).ToList();
        if (visible.Count == 0) {
          if (first) { highWatermarks[id] = 0; SaveState(); }
          Status(index, "Veri Yok; son " + Prefixes[index] + " numarası " + previous + " korundu.");
          return;
        }
        long current = visible[visible.Count - 1];
        if (first) {
          highWatermarks[id] = current; SaveState();
          Status(index, visible.Count + " kayıt görüldü; ilk başlangıç " + Prefixes[index] + current + ".");
          return;
        }
        var fresh = visible.Where(n => n > previous).ToList();
        if (current > previous) { highWatermarks[id] = current; SaveState(); }
        Status(index, visible.Count + " kayıt görüldü; son " + Prefixes[index] + highWatermarks[id] + "; " + fresh.Count + " yeni kayıt.");
        if (fresh.Count > 0) {
          string message = fresh.Count == 1 ? "#" + Prefixes[index] + fresh[0] :
            fresh.Count + " yeni kayıt; en yüksek numara #" + Prefixes[index] + current;
          Notify("Yeni " + Names[index] + " kaydı", message, settings.Urls[index]);
        }
      } catch (Exception ex) { Status(index, "Tarama hatası: " + ex.Message); }
    }

    void Status(int index, string text) {
      string line = DateTime.Now.ToString("HH:mm:ss") + "  " + Names[index] + ": " + text;
      statuses[index].Text = line;
      log.AppendText(line + "\r\n");
      if (log.TextLength > 30000) log.Text = log.Text.Substring(log.TextLength - 20000);
    }
    string SoundCode() {
      string[] modes = { "default", "asterisk", "exclamation", "hand", "beep", "silent", "custom" };
      return soundChoice.SelectedIndex >= 0 ? modes[soundChoice.SelectedIndex] : "default";
    }
    void UpdateSoundFileLabel() {
      soundFileLabel.Text = String.IsNullOrEmpty(settings.SoundFile) ? "Özel ses seçilmedi" : Path.GetFileName(settings.SoundFile);
    }
    void BrowseSound() {
      using (var dialog = new OpenFileDialog { Filter = "WAV ses dosyası (*.wav)|*.wav", CheckFileExists = true,
        Title = "Bildirim sesini seç" }) {
        if (dialog.ShowDialog(this) != DialogResult.OK) return;
        try {
          using (var sample = new SoundPlayer(dialog.FileName)) sample.Load();
          string folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "ServiceCoreMonitor");
          Directory.CreateDirectory(folder);
          string target = Path.Combine(folder, "notification.wav");
          if (customPlayer != null) { customPlayer.Dispose(); customPlayer = null; }
          if (!String.Equals(Path.GetFullPath(dialog.FileName), Path.GetFullPath(target), StringComparison.OrdinalIgnoreCase))
            File.Copy(dialog.FileName, target, true);
          settings.SoundFile = target; soundChoice.SelectedIndex = 6;
          UpdateSoundFileLabel(); SaveState(); PlayNotificationSound(true);
        } catch (Exception ex) { MessageBox.Show("WAV dosyası açılamadı: " + ex.Message); }
      }
    }
    void PlayNotificationSound(bool manual) {
      try {
        switch (SoundCode()) {
          case "silent": return;
          case "default": if (manual) SystemSounds.Asterisk.Play(); return;
          case "asterisk": SystemSounds.Asterisk.Play(); return;
          case "exclamation": SystemSounds.Exclamation.Play(); return;
          case "hand": SystemSounds.Hand.Play(); return;
          case "beep": SystemSounds.Beep.Play(); return;
          case "custom":
            if (String.IsNullOrEmpty(settings.SoundFile) || !File.Exists(settings.SoundFile)) {
              if (manual) MessageBox.Show("Önce WAV dosyası seçin.");
              return;
            }
            if (customPlayer == null) { customPlayer = new SoundPlayer(settings.SoundFile); customPlayer.Load(); }
            customPlayer.Play(); return;
        }
      } catch (Exception ex) { if (manual) MessageBox.Show("Ses çalınamadı: " + ex.Message); }
    }
    void Notify(string title, string message, string url) {
      notificationUrl = url;
      if (SoundCode() == "default") tray.ShowBalloonTip(6000, title, message, ToolTipIcon.Info);
      else PlayNotificationSound(false);
      var popup = new Form { Text = title, Size = new Size(390, 145), FormBorderStyle = FormBorderStyle.FixedToolWindow,
        TopMost = true, ShowInTaskbar = false, StartPosition = FormStartPosition.Manual };
      Rectangle area = Screen.PrimaryScreen.WorkingArea;
      popup.Location = new Point(area.Right - popup.Width - 12, area.Bottom - popup.Height - 12);
      var label = new Label { Text = message, Left = 12, Top = 12, Width = 360, Height = 72, AutoEllipsis = true };
      var open = new Button { Text = "Kaydı aç", Left = 12, Top = 88, Width = 100 };
      open.Click += (s, e) => { ShowWindow(); tabs.SelectedIndex = 0; if (ValidUrl(url) && initialized) views[0].CoreWebView2.Navigate(url); popup.Close(); };
      popup.Controls.Add(label); popup.Controls.Add(open);
      var dismiss = new Timer { Interval = 12000 }; dismiss.Tick += (s, e) => popup.Close();
      popup.FormClosed += (s, e) => { dismiss.Stop(); dismiss.Dispose(); };
      popup.Show(); dismiss.Start();
    }
    void ShowWindow() { Show(); WindowState = FormWindowState.Normal; Activate(); }
    void LoadState() {
      try {
        if (!File.Exists(statePath)) return;
        var data = json.DeserializeObject(File.ReadAllText(statePath)) as Dictionary<string, object>;
        if (data == null) return;
        if (data.ContainsKey("settings")) settings = json.Deserialize<Settings>(json.Serialize(data["settings"]));
        if (settings.Urls == null || settings.Urls.Length != 3) settings.Urls = new string[3];
        if (!settings.RememberLinks) {
          settings.HomeUrl = ""; settings.Urls = new string[3]; settings.Enabled = false;
        }
        if (data.ContainsKey("highWatermarks")) {
          var map = data["highWatermarks"] as Dictionary<string, object>;
          if (map != null) foreach (var entry in map) {
            highWatermarks[entry.Key] = Convert.ToInt64(entry.Value);
          }
        }
      } catch { settings = new Settings(); highWatermarks.Clear(); }
    }
    void ConfigureBundledSound() {
      string bundled = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "notification.wav");
      if (!File.Exists(bundled) || (settings.SoundMode != "default" && !String.IsNullOrEmpty(settings.SoundFile))) return;
      try {
        using (var sample = new SoundPlayer(bundled)) sample.Load();
        string folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "ServiceCoreMonitor");
        Directory.CreateDirectory(folder);
        string target = Path.Combine(folder, "notification.wav");
        if (!String.Equals(Path.GetFullPath(bundled), Path.GetFullPath(target), StringComparison.OrdinalIgnoreCase))
          File.Copy(bundled, target, true);
        settings.SoundMode = "custom"; settings.SoundFile = target;
      } catch { /* User can choose another WAV from the interface. */ }
    }
    void SaveState() {
      try {
        Directory.CreateDirectory(Path.GetDirectoryName(statePath));
        var stored = new Settings { HomeUrl = settings.RememberLinks ? settings.HomeUrl : "",
          Urls = settings.RememberLinks ? (string[])settings.Urls.Clone() : new string[3],
          Seconds = settings.Seconds, Enabled = settings.RememberLinks && settings.Enabled,
          RememberLinks = settings.RememberLinks, SoundMode = settings.SoundMode, SoundFile = settings.SoundFile };
        File.WriteAllText(statePath, json.Serialize(new Dictionary<string, object> { { "settings", stored }, { "highWatermarks", highWatermarks } }));
      } catch (Exception ex) { MessageBox.Show("Ayarlar kaydedilemedi: " + ex.Message); }
    }
    [STAThread] public static void Main() { Application.EnableVisualStyles(); Application.SetCompatibleTextRenderingDefault(false); Application.Run(new MainForm()); }
  }
}
