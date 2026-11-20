using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Win32;
using System.Reflection;

namespace wfaLayoutConverter
{
    public partial class Form1 : Form
    {
        [DllImport("user32.dll")] private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);
        [DllImport("user32.dll")] private static extern bool UnregisterHotKey(IntPtr hWnd, int id);
        [DllImport("user32.dll")] private static extern void keybd_event(byte bVk, byte bScan, uint dwFlags, UIntPtr dwExtraInfo);

        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll")]
        private static extern uint GetWindowThreadProcessId(IntPtr hWnd, IntPtr processId);

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern IntPtr LoadKeyboardLayout(string pwszKLID, uint Flags);

        [DllImport("user32.dll")]
        private static extern IntPtr ActivateKeyboardLayout(IntPtr hkl, uint Flags);

        [DllImport("user32.dll")]
        private static extern bool GetGUIThreadInfo(uint idThread, ref GUITHREADINFO lpgui);

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern bool PostMessage(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);

        [StructLayout(LayoutKind.Sequential)]
        private struct RECT
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct GUITHREADINFO
        {
            public int cbSize;
            public uint flags;
            public IntPtr hwndActive;
            public IntPtr hwndFocus;
            public IntPtr hwndCapture;
            public IntPtr hwndMenuOwner;
            public IntPtr hwndMoveSize;
            public IntPtr hwndCaret;
            public RECT rcCaret;
        }

        private const uint KLF_ACTIVATE = 0x00000001;

        private const uint WM_INPUTLANGCHANGEREQUEST = 0x0050;

        private static readonly IntPtr RussianLayout =
            LoadKeyboardLayout("00000419", KLF_ACTIVATE);

        private static readonly IntPtr EnglishLayout =
            LoadKeyboardLayout("00000409", KLF_ACTIVATE);

        private NotifyIcon trayIcon;
        private ContextMenuStrip trayMenu;

        private const int HOTKEY_ID = 1;
        private const uint MOD_ALT = 0x0001;
        private const uint VK_Q = 0x51;
        private const int WM_HOTKEY = 0x0312;
        private const uint KEYDOWN = 0x0000;
        private const uint KEYUP = 0x0002;
        private const byte KEY_CTRL = 0x11;
        private const byte KEY_ALT = 0x12;
        private const byte KEY_C = 0x43;
        private const byte KEY_V = 0x56;

        private bool isEnabled = true;
        private ToolStripMenuItem toggleItem;

        private static readonly Dictionary<char, char> EnToRu = new()
        {
            ['q'] = 'й',
            ['w'] = 'ц',
            ['e'] = 'у',
            ['r'] = 'к',
            ['t'] = 'е',
            ['y'] = 'н',
            ['u'] = 'г',
            ['i'] = 'ш',
            ['o'] = 'щ',
            ['p'] = 'з',
            ['['] = 'х',
            [']'] = 'ъ',
            ['a'] = 'ф',
            ['s'] = 'ы',
            ['d'] = 'в',
            ['f'] = 'а',
            ['g'] = 'п',
            ['h'] = 'р',
            ['j'] = 'о',
            ['k'] = 'л',
            ['l'] = 'д',
            [';'] = 'ж',
            ['\''] = 'э',
            ['z'] = 'я',
            ['x'] = 'ч',
            ['c'] = 'с',
            ['v'] = 'м',
            ['b'] = 'и',
            ['n'] = 'т',
            ['m'] = 'ь',
            [','] = 'б',
            ['.'] = 'ю',
            ['`'] = 'ё',
            ['{'] = 'Х',
            ['}'] = 'Ъ',
            [':'] = 'Ж',
            ['"'] = 'Э',
            ['<'] = 'Б',
            ['>'] = 'Ю',
            ['~'] = 'Ё'
        };

        private static readonly Dictionary<char, char> RuToEn =
            EnToRu.ToDictionary(kv => kv.Value, kv => kv.Key);

        public Form1()
        {
            InitializeComponent();

            trayMenu = new ContextMenuStrip();

            trayMenu.Items.Add("Hotkey: Alt + Q");

            toggleItem = new ToolStripMenuItem("Enabled");
            toggleItem.Checked = isEnabled;
            toggleItem.CheckOnClick = false;
            toggleItem.Click += ToggleEnabled;
            trayMenu.Items.Add(toggleItem);
            trayIcon = new NotifyIcon();
            trayIcon.Icon = SystemIcons.Application;
            //trayIcon.Icon = new Icon("favicon.ico");
            trayIcon.Text = "Layout Converter";
            trayIcon.ContextMenuStrip = trayMenu;
            trayIcon.Visible = true;
            this.ShowInTaskbar = false;
            this.WindowState = FormWindowState.Minimized;
            this.Hide();

            RegisterHotKey(this.Handle, HOTKEY_ID, MOD_ALT, VK_Q);
            AddToStartup(true);
        }

        private void ToggleEnabled(object? sender, EventArgs e)
        {
            isEnabled = !isEnabled;

            if (isEnabled)
            {
                RegisterHotKey(this.Handle, HOTKEY_ID, MOD_ALT, VK_Q);
                AddToStartup(true);

                toggleItem.Text = "Enabled";
                toggleItem.Checked = true;
                trayIcon.Text = "Layout Converter — enabled";
            }
            else
            {
                UnregisterHotKey(this.Handle, HOTKEY_ID);
                AddToStartup(false);

                toggleItem.Text = "Disabled";
                toggleItem.Checked = false;
                trayIcon.Text = "Layout Converter — disabled";
            }
        }

        protected override void SetVisibleCore(bool value)
        {
            base.SetVisibleCore(false);
        }

        protected override void WndProc(ref Message m)
        {
            if (isEnabled && m.Msg == WM_HOTKEY && m.WParam.ToInt32() == HOTKEY_ID)
                BeginInvoke(new Action(async () => await ConvertSelectedTextAsync()));

            base.WndProc(ref m);
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            UnregisterHotKey(this.Handle, HOTKEY_ID);
            base.OnFormClosed(e);
        }

        private async Task ConvertSelectedTextAsync()
        {
            keybd_event(KEY_CTRL, 0, KEYUP, UIntPtr.Zero);
            keybd_event(KEY_ALT, 0, KEYUP, UIntPtr.Zero);
            await Task.Delay(50);

            string? saved = Clipboard.ContainsText() ? Clipboard.GetText() : null;
            Clipboard.Clear();

            keybd_event(KEY_CTRL, 0, KEYDOWN, UIntPtr.Zero);
            keybd_event(KEY_C, 0, KEYDOWN, UIntPtr.Zero);
            keybd_event(KEY_C, 0, KEYUP, UIntPtr.Zero);
            keybd_event(KEY_CTRL, 0, KEYUP, UIntPtr.Zero);

            await Task.Delay(200);

            if (!Clipboard.ContainsText())
            {
                RestoreClipboard(saved);
                return;
            }

            string original = Clipboard.GetText();

            bool toRussian = DetectDirection(original);
            string converted = ConvertLayout(original, toRussian);

            if (converted == original)
            {
                RestoreClipboard(saved);
                return;
            }

            Clipboard.SetText(converted);
            await Task.Delay(50);

            keybd_event(KEY_CTRL, 0, KEYDOWN, UIntPtr.Zero);
            keybd_event(KEY_V, 0, KEYDOWN, UIntPtr.Zero);
            keybd_event(KEY_V, 0, KEYUP, UIntPtr.Zero);
            keybd_event(KEY_CTRL, 0, KEYUP, UIntPtr.Zero);

            await Task.Delay(150);

            SetKeyboardLayout(toRussian);

            RestoreClipboard(saved);
        }

        private static void SetKeyboardLayout(bool toRussian)
        {
            IntPtr targetLayout = toRussian ? RussianLayout : EnglishLayout;

            IntPtr hwndForeground = GetForegroundWindow();
            if (hwndForeground == IntPtr.Zero) return;

            uint foregroundThreadId = GetWindowThreadProcessId(hwndForeground, IntPtr.Zero);
            if (foregroundThreadId == 0) return;

            IntPtr targetWindow = hwndForeground;

            var info = new GUITHREADINFO();
            info.cbSize = Marshal.SizeOf(typeof(GUITHREADINFO));

            if (GetGUIThreadInfo(foregroundThreadId, ref info) && info.hwndFocus != IntPtr.Zero)
            {
                targetWindow = info.hwndFocus;
            }

            PostMessage(targetWindow, WM_INPUTLANGCHANGEREQUEST, IntPtr.Zero, targetLayout);
        }

        private static void RestoreClipboard(string? text)
        {
            if (text != null) Clipboard.SetText(text);
            else Clipboard.Clear();
        }

        private static string ConvertLayout(string input, bool toRussian)
        {
            var map = toRussian ? EnToRu : RuToEn;

            var sb = new StringBuilder(input.Length);
            foreach (char ch in input)
            {
                char lower = char.ToLower(ch);
                if (map.TryGetValue(lower, out char converted)) sb.Append(char.IsUpper(ch) ? char.ToUpper(converted) : converted);
                else
                    sb.Append(ch);
            }
            return sb.ToString();
        }

        private static bool DetectDirection(string input)
        {
            foreach (char ch in input)
            {
                char lower = char.ToLower(ch);
                if (EnToRu.ContainsKey(lower)) return true;
                if (RuToEn.ContainsKey(lower)) return false;
            }
            return true;
        }

        private void AddToStartup(bool enable)
        {
            const string appName = "LayoutConverter";

            string exePath = System.Diagnostics.Process.GetCurrentProcess().MainModule!.FileName;

            using RegistryKey? key = Registry.CurrentUser.OpenSubKey(
                @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run", writable: true);

            if (key == null) return;

            if (enable) key.SetValue(appName, $"\"{exePath}\"");
            else key.DeleteValue(appName, false);
        }
    }
}