using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Ellipse = System.Windows.Shapes.Ellipse;

namespace CodexConversationNavigator
{
    internal static class InteractiveDesktop
    {
        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr OpenInputDesktop(uint flags, bool inherit, uint desiredAccess);
        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool SetThreadDesktop(IntPtr desktop);

        private static IntPtr _desktop = IntPtr.Zero;

        public static bool Attach()
        {
            if (_desktop != IntPtr.Zero) return true;
            _desktop = OpenInputDesktop(0, false, 0x01FF);
            if (_desktop == IntPtr.Zero) return false;
            return SetThreadDesktop(_desktop);
        }
    }

    internal enum MessageRole { User, Assistant }
    internal enum ConversationViewMode { All, AssistantOnly, UserOnly }

    internal sealed class MessageEntry
    {
        public int Number { get; set; }
        public int RoleNumber { get; set; }
        public MessageRole Role { get; set; }
        public string Preview { get; set; }
    }

    internal sealed class ReadResult
    {
        public IntPtr WindowHandle { get; set; }
        public List<MessageEntry> Messages { get; set; }
        public string Error { get; set; }
    }

    internal static class WarmGlassTheme
    {
        public static readonly Brush Ink = Solid("#2B241F");
        public static readonly Brush MutedInk = Solid("#75685E");
        public static readonly Brush QuietInk = Solid("#948276");
        public static readonly Brush Accent = Solid("#E9782F");
        public static readonly Brush AccentDark = Solid("#B84E18");
        public static readonly Brush Error = Solid("#A83232");
        public static readonly Brush Card = Solid("#8EFFF9F0");
        public static readonly Brush CardHover = Solid("#BCFFF8EC");
        public static readonly Brush CardSelected = Solid("#DAFFF1DD");
        public static readonly Brush AssistantBubble = Solid("#A8FFFDF7");
        public static readonly Brush AssistantBubbleHover = Solid("#CEFFF9EF");
        public static readonly Brush UserBubble = Solid("#C8FFE1C4");
        public static readonly Brush UserBubbleHover = Solid("#E4FFE7CF");
        public static readonly Brush GlassBorder = Solid("#F0FFFFFF");
        public static readonly Brush SoftBorder = Solid("#C6FFFFFF");

        public static Brush Solid(string color)
        {
            var brush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(color));
            brush.Freeze();
            return brush;
        }

        public static Brush PanelGradient()
        {
            var brush = new LinearGradientBrush
            {
                StartPoint = new Point(0, 0),
                EndPoint = new Point(1, 1)
            };
            brush.GradientStops.Add(new GradientStop((Color)ColorConverter.ConvertFromString("#A8FFFDF8"), 0));
            brush.GradientStops.Add(new GradientStop((Color)ColorConverter.ConvertFromString("#8FEFE5D8"), 0.58));
            brush.GradientStops.Add(new GradientStop((Color)ColorConverter.ConvertFromString("#AAD8BEA1"), 1));
            brush.Freeze();
            return brush;
        }

        public static Brush EdgeGradient()
        {
            var brush = new LinearGradientBrush
            {
                StartPoint = new Point(0, 0),
                EndPoint = new Point(1, 1)
            };
            brush.GradientStops.Add(new GradientStop(Colors.White, 0));
            brush.GradientStops.Add(new GradientStop((Color)ColorConverter.ConvertFromString("#E8FFFFFF"), 0.32));
            brush.GradientStops.Add(new GradientStop((Color)ColorConverter.ConvertFromString("#7DFFFFFF"), 0.70));
            brush.GradientStops.Add(new GradientStop((Color)ColorConverter.ConvertFromString("#9A8E735F"), 1));
            brush.Freeze();
            return brush;
        }

        public static Brush InnerRimGradient()
        {
            var brush = new LinearGradientBrush
            {
                StartPoint = new Point(0, 0),
                EndPoint = new Point(1, 1)
            };
            brush.GradientStops.Add(new GradientStop((Color)ColorConverter.ConvertFromString("#E6FFFFFF"), 0));
            brush.GradientStops.Add(new GradientStop((Color)ColorConverter.ConvertFromString("#28FFFFFF"), 0.52));
            brush.GradientStops.Add(new GradientStop((Color)ColorConverter.ConvertFromString("#7A6A4F3F"), 1));
            brush.Freeze();
            return brush;
        }

        public static Brush WarmOrb()
        {
            var brush = new RadialGradientBrush
            {
                Center = new Point(0.38, 0.32),
                GradientOrigin = new Point(0.30, 0.25),
                RadiusX = 0.72,
                RadiusY = 0.72
            };
            brush.GradientStops.Add(new GradientStop((Color)ColorConverter.ConvertFromString("#FFFFD6A4"), 0));
            brush.GradientStops.Add(new GradientStop((Color)ColorConverter.ConvertFromString("#FFFF9A43"), 0.55));
            brush.GradientStops.Add(new GradientStop((Color)ColorConverter.ConvertFromString("#FFE0611D"), 1));
            brush.Freeze();
            return brush;
        }
    }

    internal static class BackdropCapture
    {
        [StructLayout(LayoutKind.Sequential)]
        private struct NativeRect
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
        }

        [DllImport("user32.dll")]
        private static extern bool GetWindowRect(IntPtr window, out NativeRect rect);

        [DllImport("gdi32.dll")]
        private static extern bool DeleteObject(IntPtr handle);

        public static BitmapSource Capture(Window window)
        {
            try
            {
                int left;
                int top;
                int width;
                int height;
                if (!window.IsVisible)
                {
                    using (System.Drawing.Graphics display = System.Drawing.Graphics.FromHwnd(IntPtr.Zero))
                    {
                        double scaleX = display.DpiX / 96.0;
                        double scaleY = display.DpiY / 96.0;
                        left = (int)Math.Round(window.Left * scaleX);
                        top = (int)Math.Round(window.Top * scaleY);
                        width = Math.Max(1, (int)Math.Round(window.Width * scaleX));
                        height = Math.Max(1, (int)Math.Round(window.Height * scaleY));
                    }
                }
                else
                {
                    IntPtr handle = new WindowInteropHelper(window).Handle;
                    NativeRect rect;
                    if (handle == IntPtr.Zero || !GetWindowRect(handle, out rect)) return null;
                    left = rect.Left;
                    top = rect.Top;
                    width = Math.Max(1, rect.Right - rect.Left);
                    height = Math.Max(1, rect.Bottom - rect.Top);
                }

                return CapturePixels(left, top, width, height);
            }
            catch
            {
                return null;
            }
        }

        public static BitmapSource Capture(Rect logicalBounds)
        {
            try
            {
                using (System.Drawing.Graphics display = System.Drawing.Graphics.FromHwnd(IntPtr.Zero))
                {
                    double scaleX = display.DpiX / 96.0;
                    double scaleY = display.DpiY / 96.0;
                    int left = (int)Math.Round(logicalBounds.Left * scaleX);
                    int top = (int)Math.Round(logicalBounds.Top * scaleY);
                    int width = Math.Max(1, (int)Math.Round(logicalBounds.Width * scaleX));
                    int height = Math.Max(1, (int)Math.Round(logicalBounds.Height * scaleY));
                    return CapturePixels(left, top, width, height);
                }
            }
            catch
            {
                return null;
            }
        }

        private static BitmapSource CapturePixels(int left, int top, int width, int height)
        {
            using (var bitmap = new System.Drawing.Bitmap(
                width, height, System.Drawing.Imaging.PixelFormat.Format32bppPArgb))
            {
                using (System.Drawing.Graphics graphics = System.Drawing.Graphics.FromImage(bitmap))
                {
                    graphics.CopyFromScreen(left, top, 0, 0, bitmap.Size,
                        System.Drawing.CopyPixelOperation.SourceCopy);
                }

                IntPtr bitmapHandle = bitmap.GetHbitmap();
                try
                {
                    BitmapSource source = Imaging.CreateBitmapSourceFromHBitmap(
                        bitmapHandle, IntPtr.Zero, Int32Rect.Empty,
                        BitmapSizeOptions.FromEmptyOptions());
                    source.Freeze();
                    return source;
                }
                finally
                {
                    DeleteObject(bitmapHandle);
                }
            }
        }
    }

    internal sealed class Anchor
    {
        public MessageRole Role { get; set; }
        public AutomationElement Marker { get; set; }
        public AutomationElement Target { get; set; }
        public double Top { get; set; }
    }

    internal sealed class AutomationMessageService
    {
        private delegate bool EnumWindowsProc(IntPtr hwnd, IntPtr lParam);

        [DllImport("user32.dll")]
        private static extern bool EnumWindows(EnumWindowsProc callback, IntPtr lParam);
        [DllImport("user32.dll")]
        private static extern bool IsWindowVisible(IntPtr hwnd);
        [DllImport("user32.dll")]
        private static extern uint GetWindowThreadProcessId(IntPtr hwnd, out uint processId);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern int GetClassName(IntPtr hwnd, StringBuilder className, int maxCount);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern int GetWindowText(IntPtr hwnd, StringBuilder title, int maxCount);
        [DllImport("user32.dll")]
        private static extern bool SetForegroundWindow(IntPtr hwnd);
        [DllImport("user32.dll")]
        private static extern bool ShowWindow(IntPtr hwnd, int command);
        [DllImport("user32.dll")]
        private static extern bool IsIconic(IntPtr hwnd);

        private const int SwRestore = 9;
        private static readonly Regex TimePattern = new Regex("^\\d{1,2}:\\d{2}$", RegexOptions.Compiled);

        public ReadResult ReadMessages()
        {
            try
            {
                IntPtr hwnd;
                AutomationElement root = FindMainCodexWindow(out hwnd);
                if (root == null)
                    return Failure("没有找到 Codex 主窗口，请先打开一个对话。");

                List<AutomationElement> textElements = GetTextElements(root);
                List<Anchor> anchors = BuildAnchors(textElements);
                if (anchors.Count == 0)
                {
                    ShowWindow(hwnd, SwRestore);
                    SetForegroundWindow(hwnd);
                    Thread.Sleep(420);
                    root = AutomationElement.FromHandle(hwnd);
                    textElements = GetTextElements(root);
                    anchors = BuildAnchors(textElements);
                }
                if (anchors.Count == 0 &&
                    (CodexPetActivator.OpenActiveNotification() || CodexThreadActivator.OpenMostRecentThread()))
                {
                    Thread.Sleep(1100);
                    root = FindMainCodexWindow(out hwnd);
                    if (root != null)
                    {
                        textElements = GetTextElements(root);
                        anchors = BuildAnchors(textElements);
                    }
                }
                if (anchors.Count == 0)
                    return Failure("当前窗口里还没有可读取的对话消息。");

                double contentLeft = anchors.Where(a => a.Role == MessageRole.Assistant)
                    .Select(a => SafeRect(a.Marker).Left).DefaultIfEmpty(SafeRect(root).Left + 300).Min();
                double contentRight = SafeRect(root).Right + 24;
                int userNumber = 0;
                int assistantNumber = 0;
                var messages = new List<MessageEntry>();

                for (int i = 0; i < anchors.Count; i++)
                {
                    Anchor anchor = anchors[i];
                    double bottom = i + 1 < anchors.Count ? anchors[i + 1].Top - 1 : double.MaxValue;
                    AutomationElement previewElement;
                    string preview = FindPreview(textElements, anchor.Top, bottom, contentLeft, contentRight, out previewElement);
                    anchor.Target = previewElement ?? anchor.Marker;
                    int roleNumber = anchor.Role == MessageRole.User ? ++userNumber : ++assistantNumber;
                    messages.Add(new MessageEntry
                    {
                        Number = i + 1,
                        RoleNumber = roleNumber,
                        Role = anchor.Role,
                        Preview = preview
                    });
                }

                return new ReadResult { WindowHandle = hwnd, Messages = messages, Error = null };
            }
            catch (Exception ex)
            {
                return Failure("读取失败：" + FriendlyError(ex));
            }
        }

        public string NavigateTo(MessageEntry entry)
        {
            try
            {
                IntPtr hwnd;
                AutomationElement root = FindMainCodexWindow(out hwnd);
                if (root == null) return "Codex 窗口已经关闭。";

                List<AutomationElement> textElements = GetTextElements(root);
                List<Anchor> anchors = BuildAnchors(textElements);
                Anchor anchor = anchors.Where(a => a.Role == entry.Role).Skip(entry.RoleNumber - 1).FirstOrDefault();
                if (anchor == null) return "对话内容已变化，请刷新目录后重试。";

                double contentLeft = anchors.Where(a => a.Role == MessageRole.Assistant)
                    .Select(a => SafeRect(a.Marker).Left).DefaultIfEmpty(SafeRect(root).Left + 300).Min();
                double contentRight = SafeRect(root).Right + 24;
                int overallIndex = anchors.IndexOf(anchor);
                double bottom = overallIndex + 1 < anchors.Count ? anchors[overallIndex + 1].Top - 1 : double.MaxValue;
                AutomationElement target;
                FindPreview(textElements, anchor.Top, bottom, contentLeft, contentRight, out target);
                target = target ?? anchor.Marker;

                if (IsIconic(hwnd)) ShowWindow(hwnd, SwRestore);
                if (!ScrollIntoView(target) && !ScrollIntoView(anchor.Marker))
                    return "这条消息暂时不能滚动定位，请刷新目录后重试。";

                Thread.Sleep(90);
                ScrollIntoView(target);
                SetForegroundWindow(hwnd);
                return null;
            }
            catch (Exception ex)
            {
                return "定位失败：" + FriendlyError(ex);
            }
        }

        private static ReadResult Failure(string message)
        {
            return new ReadResult { WindowHandle = IntPtr.Zero, Messages = new List<MessageEntry>(), Error = message };
        }

        private static AutomationElement FindMainCodexWindow(out IntPtr hwnd)
        {
            hwnd = IntPtr.Zero;
            var candidates = new List<IntPtr>();
            EnumWindows(delegate(IntPtr handle, IntPtr ignored)
            {
                if (!IsWindowVisible(handle)) return true;
                uint pid;
                GetWindowThreadProcessId(handle, out pid);
                string processName = "";
                try { processName = Process.GetProcessById((int)pid).ProcessName; } catch { }
                if (!string.Equals(processName, "ChatGPT", StringComparison.OrdinalIgnoreCase)) return true;

                var className = new StringBuilder(256);
                var title = new StringBuilder(256);
                GetClassName(handle, className, className.Capacity);
                GetWindowText(handle, title, title.Capacity);
                if (className.ToString() == "Chrome_WidgetWin_1" && title.ToString() == "ChatGPT")
                    candidates.Add(handle);
                return true;
            }, IntPtr.Zero);

            AutomationElement best = null;
            IntPtr bestHandle = IntPtr.Zero;
            int bestScore = -1;
            foreach (IntPtr candidate in candidates)
            {
                try
                {
                    AutomationElement element = AutomationElement.FromHandle(candidate);
                    if (element.Current.ControlType == ControlType.Window)
                    {
                        if (IsIconic(candidate))
                        {
                            ShowWindow(candidate, SwRestore);
                            Thread.Sleep(260);
                            element = AutomationElement.FromHandle(candidate);
                        }
                        int score = CountMessageMarkers(element);
                        if (score > bestScore)
                        {
                            best = element;
                            bestHandle = candidate;
                            bestScore = score;
                        }
                    }
                }
                catch { }
            }
            hwnd = bestHandle;
            return best;
        }

        private static int CountMessageMarkers(AutomationElement root)
        {
            int count = 0;
            try
            {
                var condition = new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Text);
                AutomationElementCollection elements = root.FindAll(TreeScope.Descendants, condition);
                for (int i = 0; i < elements.Count; i++)
                {
                    string name = SafeName(elements[i]).Trim();
                    if (name == "你说：" || name == "You said:" ||
                        name == "ChatGPT 说：" || name == "ChatGPT said:") count++;
                }
            }
            catch { }
            return count;
        }

        private static List<AutomationElement> GetTextElements(AutomationElement root)
        {
            var result = new List<AutomationElement>();
            var condition = new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Text);
            AutomationElementCollection collection = root.FindAll(TreeScope.Descendants, condition);
            for (int i = 0; i < collection.Count; i++) result.Add(collection[i]);
            return result;
        }

        private static List<Anchor> BuildAnchors(List<AutomationElement> elements)
        {
            var anchors = new List<Anchor>();
            foreach (AutomationElement element in elements)
            {
                string name = SafeName(element).Trim();
                MessageRole role;
                if (name == "你说：" || name == "You said:") role = MessageRole.User;
                else if (name == "ChatGPT 说：" || name == "ChatGPT said:") role = MessageRole.Assistant;
                else continue;

                Rect rect = SafeRect(element);
                if (!IsUsableRect(rect)) continue;
                anchors.Add(new Anchor { Role = role, Marker = element, Target = element, Top = rect.Top });
            }
            return anchors.OrderBy(a => a.Top).ToList();
        }

        private static string FindPreview(
            List<AutomationElement> elements,
            double top,
            double bottom,
            double contentLeft,
            double contentRight,
            out AutomationElement bestElement)
        {
            bestElement = null;
            var candidates = new List<Tuple<AutomationElement, string, Rect>>();
            foreach (AutomationElement element in elements)
            {
                string text = Normalize(SafeName(element));
                if (!IsMeaningful(text)) continue;
                Rect rect = SafeRect(element);
                if (!IsUsableRect(rect) || rect.Width <= 4 || rect.Height <= 4) continue;
                if (rect.Top < top - 2 || rect.Top >= bottom) continue;
                if (rect.Left < contentLeft - 24 || rect.Right > contentRight) continue;
                candidates.Add(Tuple.Create(element, text, rect));
            }

            if (candidates.Count == 0) return "（内容加载中）";
            double firstTop = candidates.Min(c => c.Item3.Top);
            var early = candidates.Where(c => c.Item3.Top <= firstTop + 150)
                .OrderByDescending(c => c.Item2.Length)
                .ThenBy(c => c.Item3.Top)
                .ToList();
            var chosen = early.First();
            bestElement = chosen.Item1;
            return Ellipsize(chosen.Item2, 92);
        }

        private static bool IsMeaningful(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return false;
            if (text == "你说：" || text == "ChatGPT 说：" || text == "You said:" || text == "ChatGPT said:") return false;
            if (TimePattern.IsMatch(text)) return false;
            if (text.StartsWith("已处理 ", StringComparison.Ordinal) || text.StartsWith("Thought for ", StringComparison.OrdinalIgnoreCase)) return false;
            return true;
        }

        private static bool ScrollIntoView(AutomationElement element)
        {
            AutomationElement current = element;
            for (int level = 0; level < 8 && current != null; level++)
            {
                try
                {
                    object value;
                    if (current.TryGetCurrentPattern(ScrollItemPattern.Pattern, out value))
                    {
                        ((ScrollItemPattern)value).ScrollIntoView();
                        return true;
                    }
                }
                catch { }
                try { current = TreeWalker.RawViewWalker.GetParent(current); }
                catch { current = null; }
            }
            return false;
        }

        private static string SafeName(AutomationElement element)
        {
            try { return element.Current.Name ?? ""; } catch { return ""; }
        }

        private static Rect SafeRect(AutomationElement element)
        {
            try { return element.Current.BoundingRectangle; } catch { return Rect.Empty; }
        }

        private static bool IsUsableRect(Rect rect)
        {
            return !rect.IsEmpty && !double.IsInfinity(rect.Left) && !double.IsNaN(rect.Left) && rect.Width >= 0 && rect.Height >= 0;
        }

        private static string Normalize(string value)
        {
            if (value == null) return "";
            return Regex.Replace(value.Replace('\r', ' ').Replace('\n', ' '), "\\s+", " ").Trim();
        }

        private static string Ellipsize(string value, int length)
        {
            return value.Length <= length ? value : value.Substring(0, length - 1) + "…";
        }

        private static string FriendlyError(Exception exception)
        {
            if (exception is ElementNotAvailableException) return "Codex 界面刚刚发生变化，请重试。";
            return exception.Message;
        }
    }

    internal static class CodexPetActivator
    {
        private delegate bool EnumWindowsProc(IntPtr hwnd, IntPtr lParam);
        [DllImport("user32.dll")] private static extern bool EnumWindows(EnumWindowsProc callback, IntPtr lParam);
        [DllImport("user32.dll")] private static extern bool IsWindowVisible(IntPtr hwnd);
        [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(IntPtr hwnd, out uint processId);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern int GetClassName(IntPtr hwnd, StringBuilder className, int maxCount);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern int GetWindowText(IntPtr hwnd, StringBuilder title, int maxCount);

        public static bool OpenActiveNotification()
        {
            var roots = new List<AutomationElement>();
            EnumWindows(delegate(IntPtr handle, IntPtr ignored)
            {
                if (!IsWindowVisible(handle)) return true;
                uint pid;
                GetWindowThreadProcessId(handle, out pid);
                string processName = "";
                try { processName = Process.GetProcessById((int)pid).ProcessName; } catch { }
                if (!string.Equals(processName, "ChatGPT", StringComparison.OrdinalIgnoreCase)) return true;
                var className = new StringBuilder(256);
                var title = new StringBuilder(256);
                GetClassName(handle, className, className.Capacity);
                GetWindowText(handle, title, title.Capacity);
                if (className.ToString() != "Chrome_WidgetWin_1" || title.ToString() != "ChatGPT") return true;
                try
                {
                    AutomationElement root = AutomationElement.FromHandle(handle);
                    if (root.Current.ControlType == ControlType.Pane) roots.Add(root);
                }
                catch { }
                return true;
            }, IntPtr.Zero);

            foreach (AutomationElement root in roots)
            {
                try
                {
                    var condition = new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Button);
                    AutomationElementCollection buttons = root.FindAll(TreeScope.Descendants, condition);
                    for (int i = 0; i < buttons.Count; i++)
                    {
                        string name = buttons[i].Current.Name ?? "";
                        if (name.IndexOf("打开通知", StringComparison.Ordinal) < 0 &&
                            name.IndexOf("Open notification", StringComparison.OrdinalIgnoreCase) < 0) continue;
                        object pattern;
                        if (buttons[i].TryGetCurrentPattern(InvokePattern.Pattern, out pattern))
                        {
                            ((InvokePattern)pattern).Invoke();
                            return true;
                        }
                    }
                }
                catch { }
            }
            return false;
        }
    }

    internal static class CodexThreadActivator
    {
        private static readonly Regex ThreadRecordPattern = new Regex(
            "\\\"id\\\"\\s*:\\s*\\\"([0-9a-fA-F-]{36})\\\"[\\s\\S]*?\\\"updatedAt\\\"\\s*:\\s*(\\d+)",
            RegexOptions.Compiled);

        public static bool OpenMostRecentThread()
        {
            Process server = null;
            try
            {
                string executable = FindCodexExecutable();
                if (executable == null) return false;

                var startInfo = new ProcessStartInfo(executable, "app-server")
                {
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardInput = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    StandardOutputEncoding = Encoding.UTF8,
                    StandardErrorEncoding = Encoding.UTF8
                };
                server = Process.Start(startInfo);
                if (server == null) return false;

                Send(server, "{\"method\":\"initialize\",\"id\":1,\"params\":{\"clientInfo\":{\"name\":\"conversation_navigator\",\"title\":\"Conversation Navigator\",\"version\":\"0.1.0\"},\"capabilities\":{\"experimentalApi\":true}}}");
                if (ReadResponse(server, 1, 1800) == null) return false;
                Send(server, "{\"method\":\"initialized\",\"params\":{}}");
                Send(server, "{\"method\":\"thread/list\",\"id\":2,\"params\":{\"limit\":20,\"useStateDbOnly\":true}}");
                string response = ReadResponse(server, 2, 2200);
                if (response == null) return false;
                MatchCollection matches = ThreadRecordPattern.Matches(response);
                if (matches.Count == 0) return false;

                Match match = matches.Cast<Match>()
                    .OrderByDescending(value =>
                    {
                        long updated;
                        return long.TryParse(value.Groups[2].Value, out updated) ? updated : 0;
                    })
                    .First();

                Guid threadId;
                if (!Guid.TryParse(match.Groups[1].Value, out threadId)) return false;
                Process.Start(new ProcessStartInfo("codex://threads/" + threadId.ToString()) { UseShellExecute = true });
                return true;
            }
            catch
            {
                return false;
            }
            finally
            {
                if (server != null)
                {
                    try { if (!server.HasExited) server.Kill(); } catch { }
                    server.Dispose();
                }
            }
        }

        private static string FindCodexExecutable()
        {
            string root = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "OpenAI", "Codex", "bin");
            if (!Directory.Exists(root)) return null;
            return Directory.GetFiles(root, "codex.exe", SearchOption.AllDirectories)
                .OrderByDescending(File.GetLastWriteTimeUtc)
                .FirstOrDefault();
        }

        private static void Send(Process process, string json)
        {
            process.StandardInput.WriteLine(json);
            process.StandardInput.Flush();
        }

        private static string ReadResponse(Process process, int requestId, int timeoutMilliseconds)
        {
            DateTime deadline = DateTime.UtcNow.AddMilliseconds(timeoutMilliseconds);
            while (DateTime.UtcNow < deadline && !process.HasExited)
            {
                string line = ReadLine(process, Math.Max(50, (int)(deadline - DateTime.UtcNow).TotalMilliseconds));
                if (line == null) return null;
                if (Regex.IsMatch(line, "\\\"id\\\"\\s*:\\s*" + requestId + "(?:\\s*[,}])")) return line;
            }
            return null;
        }

        private static string ReadLine(Process process, int timeoutMilliseconds)
        {
            string line = null;
            var completed = new ManualResetEvent(false);
            var reader = new Thread(new ThreadStart(delegate
            {
                try { line = process.StandardOutput.ReadLine(); } catch { }
                completed.Set();
            }));
            reader.IsBackground = true;
            reader.Start();
            bool signaled = completed.WaitOne(timeoutMilliseconds);
            completed.Dispose();
            return signaled ? line : null;
        }
    }

    internal static class LocalSettings
    {
        private static readonly string DirectoryPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "CodexConversationNavigator");
        private static readonly string SettingsPath = Path.Combine(DirectoryPath, "position.txt");

        public static Point LoadPosition()
        {
            try
            {
                string[] parts = File.ReadAllText(SettingsPath).Split('|');
                double left, top;
                if (parts.Length == 2 &&
                    double.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out left) &&
                    double.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out top))
                    return new Point(left, top);
            }
            catch { }
            Rect area = SystemParameters.WorkArea;
            return new Point(area.Right - 76, area.Top + area.Height * 0.42);
        }

        public static void SavePosition(double left, double top)
        {
            try
            {
                Directory.CreateDirectory(DirectoryPath);
                File.WriteAllText(SettingsPath,
                    left.ToString(CultureInfo.InvariantCulture) + "|" + top.ToString(CultureInfo.InvariantCulture));
            }
            catch { }
        }
    }

    internal sealed class BubbleWindow : Window
    {
        private const double IdleOpacity = 0.62;
        private static readonly TimeSpan IdleDelay = TimeSpan.FromSeconds(8);
        private readonly NavigatorController _controller;
        private Point _mouseDown;
        private Point _windowDown;
        private bool _dragging;
        private readonly ToolTip _tooltip;
        private readonly System.Windows.Controls.Image _collapsedIcon;
        private readonly System.Windows.Controls.Image _expandedIcon;
        private readonly DispatcherTimer _idleTimer;
        private bool _isExpanded;

        public BubbleWindow(NavigatorController controller)
        {
            _controller = controller;
            Width = 58;
            Height = 58;
            WindowStyle = WindowStyle.None;
            AllowsTransparency = true;
            Background = Brushes.Transparent;
            ResizeMode = ResizeMode.NoResize;
            ShowInTaskbar = false;
            Topmost = true;
            Focusable = true;

            Point position = LocalSettings.LoadPosition();
            Rect area = SystemParameters.WorkArea;
            Left = Math.Max(area.Left, Math.Min(position.X, area.Right - Width));
            Top = Math.Max(area.Top, Math.Min(position.Y, area.Bottom - Height));

            var iconLayer = new Grid { Background = Brushes.Transparent };
            _collapsedIcon = CreateOrbImage("floating-orb-purple.png");
            _expandedIcon = CreateOrbImage("floating-orb-cyan-gold.png");
            _expandedIcon.Opacity = 0;
            iconLayer.Children.Add(_collapsedIcon);
            iconLayer.Children.Add(_expandedIcon);
            if (_collapsedIcon.Source == null || _expandedIcon.Source == null)
            {
                iconLayer.Children.Add(new TextBlock
                {
                    Text = "目",
                    FontFamily = new FontFamily("Microsoft YaHei UI"),
                    FontSize = 22,
                    FontWeight = FontWeights.SemiBold,
                    Foreground = Brushes.White,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center
                });
            }
            Content = iconLayer;

            _tooltip = new ToolTip { Content = "对话目录（拖动调整位置，右键退出）" };
            ToolTip = _tooltip;
            AutomationProperties.SetName(this, "打开 Codex 对话目录");

            _idleTimer = new DispatcherTimer(DispatcherPriority.Background)
            {
                Interval = IdleDelay
            };
            _idleTimer.Tick += delegate
            {
                _idleTimer.Stop();
                if (!_isExpanded && !IsMouseOver && !IsKeyboardFocusWithin && !IsMouseCaptured)
                {
                    AnimateWindowOpacity(IdleOpacity, 280);
                    AutomationProperties.SetHelpText(this, "闲置状态，悬浮球半透明");
                }
            };

            MouseLeftButtonDown += OnMouseDown;
            MouseMove += OnMouseMove;
            MouseLeftButtonUp += OnMouseUp;
            MouseEnter += delegate { Wake(); };
            MouseLeave += delegate { RestartIdleTimer(); };
            GotKeyboardFocus += delegate { Wake(); };
            LostKeyboardFocus += delegate { RestartIdleTimer(); };
            KeyDown += delegate(object sender, KeyEventArgs e)
            {
                Wake();
                if (e.Key == Key.Enter || e.Key == Key.Space) _controller.TogglePanel();
                if (e.Key == Key.Escape) _controller.HidePanel();
            };

            var context = new ContextMenu();
            var open = new MenuItem { Header = "打开并刷新目录" };
            open.Click += delegate { _controller.ShowPanel(true); };
            var exit = new MenuItem { Header = "退出" };
            exit.Click += delegate { _controller.Exit(); };
            context.Items.Add(open);
            context.Items.Add(new Separator());
            context.Items.Add(exit);
            ContextMenu = context;

            Loaded += delegate { RestartIdleTimer(); };
            Closed += delegate { _idleTimer.Stop(); };
        }

        private static System.Windows.Controls.Image CreateOrbImage(string fileName)
        {
            string path = Path.GetFullPath(Path.Combine(
                AppDomain.CurrentDomain.BaseDirectory,
                "..",
                "assets",
                fileName));
            var image = new System.Windows.Controls.Image
            {
                Stretch = Stretch.Uniform,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                VerticalAlignment = VerticalAlignment.Stretch,
                IsHitTestVisible = false,
                SnapsToDevicePixels = true
            };
            RenderOptions.SetBitmapScalingMode(image, BitmapScalingMode.HighQuality);
            if (!File.Exists(path)) return image;

            var bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.UriSource = new Uri(path, UriKind.Absolute);
            bitmap.EndInit();
            bitmap.Freeze();
            image.Source = bitmap;
            return image;
        }

        public void SetExpanded(bool expanded)
        {
            _isExpanded = expanded;
            AnimateIcon(_collapsedIcon, expanded ? 0 : 1);
            AnimateIcon(_expandedIcon, expanded ? 1 : 0);
            AutomationProperties.SetName(this,
                expanded ? "收起 Codex 对话目录" : "打开 Codex 对话目录");
            AutomationProperties.SetHelpText(this,
                expanded ? "目录已展开" : "目录已收起");
            if (expanded)
            {
                _idleTimer.Stop();
                AnimateWindowOpacity(1, 140);
            }
            else
            {
                AnimateWindowOpacity(1, 140);
                RestartIdleTimer();
            }
        }

        private static void AnimateIcon(UIElement icon, double targetOpacity)
        {
            icon.BeginAnimation(UIElement.OpacityProperty, new DoubleAnimation
            {
                To = targetOpacity,
                Duration = TimeSpan.FromMilliseconds(170),
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
            }, HandoffBehavior.SnapshotAndReplace);
        }

        private void AnimateWindowOpacity(double targetOpacity, int durationMilliseconds)
        {
            BeginAnimation(Window.OpacityProperty, new DoubleAnimation
            {
                To = targetOpacity,
                Duration = TimeSpan.FromMilliseconds(durationMilliseconds),
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
            }, HandoffBehavior.SnapshotAndReplace);
        }

        private void Wake()
        {
            _idleTimer.Stop();
            AnimateWindowOpacity(1, 120);
            AutomationProperties.SetHelpText(this,
                _isExpanded ? "目录已展开" : "目录已收起");
        }

        private void RestartIdleTimer()
        {
            _idleTimer.Stop();
            if (_isExpanded || IsMouseOver || IsKeyboardFocusWithin || IsMouseCaptured) return;
            _idleTimer.Start();
        }

        public void SetStatus(string text)
        {
            _tooltip.Content = text + "\n拖动调整位置，右键退出";
            Wake();
            RestartIdleTimer();
        }

        private void OnMouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton != MouseButton.Left) return;
            Wake();
            _controller.PreparePanelBackdrop();
            _mouseDown = PointToScreen(e.GetPosition(this));
            _windowDown = new Point(Left, Top);
            _dragging = false;
            CaptureMouse();
            e.Handled = true;
        }

        private void OnMouseMove(object sender, MouseEventArgs e)
        {
            if (e.LeftButton != MouseButtonState.Pressed || !IsMouseCaptured) return;
            Point current = PointToScreen(e.GetPosition(this));
            Vector delta = current - _mouseDown;
            if (Math.Abs(delta.X) + Math.Abs(delta.Y) > 5) _dragging = true;
            if (!_dragging) return;

            Rect area = SystemParameters.WorkArea;
            Left = Math.Max(area.Left, Math.Min(_windowDown.X + delta.X, area.Right - Width));
            Top = Math.Max(area.Top, Math.Min(_windowDown.Y + delta.Y, area.Bottom - Height));
            _controller.RepositionPanel();
        }

        private void OnMouseUp(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton != MouseButton.Left) return;
            ReleaseMouseCapture();
            if (_dragging)
            {
                LocalSettings.SavePosition(Left, Top);
                _controller.RefreshBackdrop();
                RestartIdleTimer();
            }
            else _controller.TogglePanel();
            e.Handled = true;
        }

    }

    internal sealed class DirectoryWindow : Window
    {
        private readonly NavigatorController _controller;
        private readonly TextBox _filter;
        private readonly ListBox _list;
        private readonly TextBlock _count;
        private readonly TextBlock _status;
        private readonly Button _modeButton;
        private readonly Button _refreshButton;
        private readonly System.Windows.Controls.Image _backdropImage;
        private readonly Grid _contentRoot;
        private DispatcherTimer _refreshResetTimer;
        private bool _manualRefreshPending;
        private int _backdropRefreshGeneration;
        private Point _headerMouseDown;
        private Point _panelDown;
        private Point _bubbleDown;
        private bool _headerDragging;
        private List<MessageEntry> _messages = new List<MessageEntry>();
        private ConversationViewMode _viewMode = ConversationViewMode.All;

        public DirectoryWindow(NavigatorController controller)
        {
            _controller = controller;
            Width = 432;
            Height = 592;
            WindowStyle = WindowStyle.None;
            AllowsTransparency = true;
            Background = Brushes.Transparent;
            ResizeMode = ResizeMode.NoResize;
            ShowInTaskbar = false;
            Topmost = true;
            ShowActivated = false;
            FontFamily = new FontFamily("Microsoft YaHei UI");

            var root = new Grid();
            _contentRoot = root;
            var shell = new Border
            {
                Margin = new Thickness(8),
                CornerRadius = new CornerRadius(28),
                Background = WarmGlassTheme.EdgeGradient(),
                BorderThickness = new Thickness(0),
                Padding = new Thickness(5),
                Effect = new DropShadowEffect
                {
                    Color = (Color)ColorConverter.ConvertFromString("#71362318"),
                    BlurRadius = 24,
                    ShadowDepth = 7,
                    Opacity = 0.42
                }
            };

            var material = new Grid { Background = WarmGlassTheme.PanelGradient() };
            material.SizeChanged += delegate
            {
                material.Clip = new RectangleGeometry(
                    new Rect(0, 0, material.ActualWidth, material.ActualHeight), 23, 23);
            };

            _backdropImage = new System.Windows.Controls.Image
            {
                Stretch = Stretch.Fill,
                Margin = new Thickness(-18),
                Opacity = 0.88,
                IsHitTestVisible = false,
                Effect = new BlurEffect { Radius = 24, KernelType = KernelType.Gaussian }
            };
            RenderOptions.SetBitmapScalingMode(_backdropImage, BitmapScalingMode.HighQuality);
            material.Children.Add(_backdropImage);
            material.Children.Add(new Border
            {
                Background = WarmGlassTheme.PanelGradient(),
                IsHitTestVisible = false
            });
            material.Children.Add(new Ellipse
            {
                Width = 190,
                Height = 190,
                Fill = WarmGlassTheme.WarmOrb(),
                Opacity = 0.78,
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Top,
                Margin = new Thickness(0, 90, 20, 0),
                IsHitTestVisible = false,
                Effect = new BlurEffect { Radius = 18, KernelType = KernelType.Gaussian }
            });
            material.Children.Add(new Border
            {
                Background = WarmGlassTheme.Solid("#2EFFFFFF"),
                IsHitTestVisible = false
            });

            var layout = new Grid();
            layout.Margin = new Thickness(15, 13, 15, 12);
            layout.RowDefinitions.Add(new RowDefinition { Height = new GridLength(50) });
            layout.RowDefinitions.Add(new RowDefinition { Height = new GridLength(46) });
            layout.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            layout.RowDefinitions.Add(new RowDefinition { Height = new GridLength(34) });

            var header = new Grid
            {
                Background = Brushes.Transparent,
                Cursor = Cursors.SizeAll,
                Focusable = true,
                ToolTip = "拖动顶部标题栏可移动悬浮球与目录；方向键可微调"
            };
            AutomationProperties.SetName(header, "移动悬浮球与对话目录");
            AutomationProperties.SetHelpText(header, "拖动顶部非按钮区域移动；方向键移动 10 像素，Shift 加方向键移动 1 像素");
            header.PreviewMouseLeftButtonDown += OnHeaderMouseDown;
            header.PreviewMouseMove += OnHeaderMouseMove;
            header.PreviewMouseLeftButtonUp += OnHeaderMouseUp;
            header.LostMouseCapture += OnHeaderLostMouseCapture;
            header.KeyDown += OnHeaderKeyDown;
            header.GotKeyboardFocus += delegate(object sender, KeyboardFocusChangedEventArgs e)
            {
                if (ReferenceEquals(e.NewFocus, header))
                    header.Background = WarmGlassTheme.Solid("#20FFFFFF");
            };
            header.LostKeyboardFocus += delegate { header.Background = Brushes.Transparent; };
            header.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            var titleStack = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
            titleStack.Children.Add(new Ellipse
            {
                Width = 10,
                Height = 10,
                Fill = WarmGlassTheme.Accent,
                Margin = new Thickness(0, 0, 9, 0),
                Effect = new DropShadowEffect { Color = Colors.Orange, BlurRadius = 9, ShadowDepth = 0, Opacity = 0.55 }
            });
            titleStack.Children.Add(new TextBlock
            {
                Text = "对话目录",
                FontSize = 18,
                FontWeight = FontWeights.SemiBold,
                Foreground = WarmGlassTheme.Ink,
                VerticalAlignment = VerticalAlignment.Center
            });
            _count = new TextBlock
            {
                Margin = new Thickness(9, 2, 0, 0),
                FontSize = 12,
                Foreground = WarmGlassTheme.MutedInk,
                VerticalAlignment = VerticalAlignment.Center
            };
            titleStack.Children.Add(_count);

            Grid.SetColumn(titleStack, 0);
            header.Children.Add(titleStack);

            _modeButton = HeaderButton("全部 ⇄", "对话视图：全部对话，点击切换", false);
            _modeButton.MinWidth = 62;
            _modeButton.Background = WarmGlassTheme.UserBubble;
            _modeButton.BorderBrush = WarmGlassTheme.Accent;
            UpdateModeButton();
            _modeButton.Click += delegate { CycleViewMode(); };
            Grid.SetColumn(_modeButton, 1);
            header.Children.Add(_modeButton);

            _refreshButton = HeaderButton("刷新", "刷新对话目录", false);
            _refreshButton.Width = 58;
            _refreshButton.ToolTip = "刷新当前对话目录";
            _refreshButton.RenderTransformOrigin = new Point(0.5, 0.5);
            _refreshButton.RenderTransform = new ScaleTransform(1, 1);
            _refreshButton.PreviewMouseLeftButtonDown += delegate { ShowRefreshPressedState(); };
            _refreshButton.PreviewMouseLeftButtonUp += delegate { ReleaseRefreshPressedState(); };
            _refreshButton.LostMouseCapture += delegate { ReleaseRefreshPressedState(); };
            _refreshButton.PreviewKeyDown += delegate(object sender, KeyEventArgs e)
            {
                if (e.Key == Key.Space || e.Key == Key.Enter) ShowRefreshPressedState();
            };
            _refreshButton.PreviewKeyUp += delegate(object sender, KeyEventArgs e)
            {
                if (e.Key == Key.Space || e.Key == Key.Enter) ReleaseRefreshPressedState();
            };
            _refreshButton.LostKeyboardFocus += delegate { ReleaseRefreshPressedState(); };
            _refreshButton.Click += delegate { BeginManualRefresh(); };
            Grid.SetColumn(_refreshButton, 2);
            header.Children.Add(_refreshButton);
            Button close = HeaderButton("×", "收起对话目录", true);
            close.FontSize = 20;
            close.Click += delegate { _controller.HidePanel(); };
            Grid.SetColumn(close, 3);
            header.Children.Add(close);
            Grid.SetRow(header, 0);
            layout.Children.Add(header);

            var filterBorder = new Border
            {
                Background = WarmGlassTheme.Card,
                BorderBrush = WarmGlassTheme.EdgeGradient(),
                BorderThickness = new Thickness(1.2, 1.2, 1.7, 1.7),
                CornerRadius = new CornerRadius(12),
                Margin = new Thickness(0, 0, 0, 8),
                Padding = new Thickness(12, 0, 12, 0)
            };
            var filterGrid = new Grid();
            filterGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            filterGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            filterGrid.Children.Add(new TextBlock
            {
                Text = "筛选",
                Foreground = WarmGlassTheme.MutedInk,
                FontSize = 12,
                Margin = new Thickness(0, 0, 9, 0),
                VerticalAlignment = VerticalAlignment.Center
            });
            _filter = new TextBox
            {
                Background = Brushes.Transparent,
                BorderThickness = new Thickness(0),
                Foreground = WarmGlassTheme.Ink,
                CaretBrush = WarmGlassTheme.AccentDark,
                FontSize = 13,
                VerticalContentAlignment = VerticalAlignment.Center
            };
            _filter.GotKeyboardFocus += delegate
            {
                filterBorder.BorderBrush = WarmGlassTheme.Accent;
                filterBorder.BorderThickness = new Thickness(2);
            };
            _filter.LostKeyboardFocus += delegate
            {
                filterBorder.BorderBrush = WarmGlassTheme.SoftBorder;
                filterBorder.BorderThickness = new Thickness(1.2, 1.2, 1.7, 1.7);
            };
            AutomationProperties.SetName(_filter, "筛选对话目录");
            _filter.TextChanged += delegate { ApplyFilter(); };
            Grid.SetColumn(_filter, 1);
            filterGrid.Children.Add(_filter);
            filterBorder.Child = filterGrid;
            Grid.SetRow(filterBorder, 1);
            layout.Children.Add(filterBorder);

            _list = new ListBox
            {
                Background = Brushes.Transparent,
                BorderThickness = new Thickness(0),
                Foreground = WarmGlassTheme.Ink,
                HorizontalContentAlignment = HorizontalAlignment.Stretch,
                FocusVisualStyle = null
            };
            ScrollViewer.SetCanContentScroll(_list, true);
            ScrollViewer.SetHorizontalScrollBarVisibility(_list, ScrollBarVisibility.Disabled);
            _list.Resources[typeof(ScrollBar)] = CreateGlassScrollBarStyle();
            _list.MouseDoubleClick += delegate { NavigateSelected(); };
            _list.PreviewKeyDown += delegate(object sender, KeyEventArgs e)
            {
                if (e.Key == Key.Enter || e.Key == Key.Space) { NavigateSelected(); e.Handled = true; }
                else if (e.Key == Key.Escape) { _controller.HidePanel(); e.Handled = true; }
                else if (e.Key == Key.R && Keyboard.Modifiers == ModifierKeys.Control) { _controller.Refresh(); e.Handled = true; }
            };
            Grid.SetRow(_list, 2);
            layout.Children.Add(_list);

            _status = new TextBlock
            {
                Text = "打开目录时自动刷新",
                Foreground = WarmGlassTheme.MutedInk,
                FontSize = 11,
                TextTrimming = TextTrimming.CharacterEllipsis,
                VerticalAlignment = VerticalAlignment.Bottom
            };
            Grid.SetRow(_status, 3);
            layout.Children.Add(_status);

            material.Children.Add(layout);
            material.Children.Add(new Border
            {
                Height = 44,
                VerticalAlignment = VerticalAlignment.Top,
                Background = new LinearGradientBrush(
                    (Color)ColorConverter.ConvertFromString("#52FFFFFF"),
                    Colors.Transparent,
                    90),
                IsHitTestVisible = false
            });

            shell.Child = material;
            root.Children.Add(shell);
            Content = root;
            Opacity = 0;
            Deactivated += delegate { };
        }

        public void Prewarm()
        {
            BeginAnimation(OpacityProperty, null);
            Opacity = 0;
            Left = SystemParameters.VirtualScreenLeft - Width - 64;
            Top = SystemParameters.VirtualScreenTop - Height - 64;
            Show();
            UpdateLayout();
            Hide();
            Opacity = 1;
        }

        private Point ScreenPointInDips(Point localPoint)
        {
            Point devicePoint = PointToScreen(localPoint);
            PresentationSource source = PresentationSource.FromVisual(this);
            if (source != null && source.CompositionTarget != null)
                return source.CompositionTarget.TransformFromDevice.Transform(devicePoint);
            return devicePoint;
        }

        private void OnHeaderMouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton != MouseButton.Left) return;
            var surface = (UIElement)sender;
            if (IsHeaderActionSource(e.OriginalSource as DependencyObject, surface)) return;
            surface.Focus();
            _headerMouseDown = ScreenPointInDips(e.GetPosition(this));
            _panelDown = new Point(Left, Top);
            _bubbleDown = new Point(_controller.Bubble.Left, _controller.Bubble.Top);
            _headerDragging = false;
            surface.CaptureMouse();
            e.Handled = true;
        }

        private void OnHeaderMouseMove(object sender, MouseEventArgs e)
        {
            var surface = (UIElement)sender;
            if (e.LeftButton != MouseButtonState.Pressed || !surface.IsMouseCaptured) return;
            Point current = ScreenPointInDips(e.GetPosition(this));
            Vector delta = current - _headerMouseDown;
            if (!_headerDragging && Math.Abs(delta.X) + Math.Abs(delta.Y) > 4)
            {
                _headerDragging = true;
                AnimateBackdropOpacity(0.42, 110);
            }
            if (!_headerDragging) return;
            _controller.MovePanelGroup(_panelDown, _bubbleDown, delta);
            e.Handled = true;
        }

        private void OnHeaderMouseUp(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton != MouseButton.Left) return;
            var surface = (UIElement)sender;
            if (!surface.IsMouseCaptured) return;
            if (surface.IsMouseCaptured) surface.ReleaseMouseCapture();
            FinishHeaderDrag();
            e.Handled = true;
        }

        private void OnHeaderLostMouseCapture(object sender, MouseEventArgs e)
        {
            FinishHeaderDrag();
        }

        private void FinishHeaderDrag()
        {
            if (!_headerDragging) return;
            _headerDragging = false;
            AnimateBackdropOpacity(0.88, 140);
            _controller.CommitPanelGroupMove();
        }

        private void OnHeaderKeyDown(object sender, KeyEventArgs e)
        {
            if (!ReferenceEquals(Keyboard.FocusedElement, sender)) return;
            double step = (Keyboard.Modifiers & ModifierKeys.Shift) != 0 ? 1 : 10;
            Vector delta;
            if (e.Key == Key.Left) delta = new Vector(-step, 0);
            else if (e.Key == Key.Right) delta = new Vector(step, 0);
            else if (e.Key == Key.Up) delta = new Vector(0, -step);
            else if (e.Key == Key.Down) delta = new Vector(0, step);
            else return;
            _controller.NudgePanelGroup(delta);
            e.Handled = true;
        }

        private static bool IsHeaderActionSource(DependencyObject source, UIElement header)
        {
            DependencyObject current = source;
            while (current != null && !ReferenceEquals(current, header))
            {
                if (current is ButtonBase) return true;
                current = VisualTreeHelper.GetParent(current);
            }
            return false;
        }

        private void AnimateBackdropOpacity(double targetOpacity, int durationMilliseconds)
        {
            _backdropImage.BeginAnimation(UIElement.OpacityProperty, new DoubleAnimation
            {
                To = targetOpacity,
                Duration = TimeSpan.FromMilliseconds(durationMilliseconds),
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
            }, HandoffBehavior.SnapshotAndReplace);
        }

        private static Style CreateGlassScrollBarStyle()
        {
            const string xaml = @"
<Style xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation'
       xmlns:x='http://schemas.microsoft.com/winfx/2006/xaml'
       TargetType='{x:Type ScrollBar}'>
  <Setter Property='Width' Value='10'/>
  <Setter Property='Margin' Value='4,5,0,5'/>
  <Setter Property='Opacity' Value='0.55'/>
  <Setter Property='Background' Value='Transparent'/>
  <Setter Property='Template'>
    <Setter.Value>
      <ControlTemplate TargetType='{x:Type ScrollBar}'>
        <Grid Background='Transparent'>
          <Track x:Name='PART_Track'
                 IsDirectionReversed='True'
                 Focusable='False'
                 Minimum='{TemplateBinding Minimum}'
                 Maximum='{TemplateBinding Maximum}'
                 Value='{TemplateBinding Value}'
                 ViewportSize='{TemplateBinding ViewportSize}'
                 Orientation='{TemplateBinding Orientation}'>
            <Track.DecreaseRepeatButton>
              <RepeatButton Command='{x:Static ScrollBar.PageUpCommand}'
                            Focusable='False' Opacity='0'/>
            </Track.DecreaseRepeatButton>
            <Track.Thumb>
              <Thumb MinHeight='42' Focusable='False'>
                <Thumb.Template>
                  <ControlTemplate TargetType='{x:Type Thumb}'>
                    <Border x:Name='ThumbBody'
                            Margin='2,0'
                            CornerRadius='4'
                            BorderThickness='1'
                            BorderBrush='#CFFFFFFF'>
                      <Border.Background>
                        <LinearGradientBrush StartPoint='0,0' EndPoint='1,0'>
                          <GradientStop Color='#D9FFFFFF' Offset='0'/>
                          <GradientStop Color='#9A9B806B' Offset='1'/>
                        </LinearGradientBrush>
                      </Border.Background>
                      <Border.Effect>
                        <DropShadowEffect Color='#5A39291F' BlurRadius='5'
                                          ShadowDepth='1' Opacity='0.35'/>
                      </Border.Effect>
                    </Border>
                    <ControlTemplate.Triggers>
                      <Trigger Property='IsMouseOver' Value='True'>
                        <Setter TargetName='ThumbBody' Property='BorderBrush' Value='#FFE9782F'/>
                      </Trigger>
                    </ControlTemplate.Triggers>
                  </ControlTemplate>
                </Thumb.Template>
              </Thumb>
            </Track.Thumb>
            <Track.IncreaseRepeatButton>
              <RepeatButton Command='{x:Static ScrollBar.PageDownCommand}'
                            Focusable='False' Opacity='0'/>
            </Track.IncreaseRepeatButton>
          </Track>
        </Grid>
        <ControlTemplate.Triggers>
          <Trigger Property='IsMouseOver' Value='True'>
            <Setter Property='Opacity' Value='1'/>
          </Trigger>
        </ControlTemplate.Triggers>
      </ControlTemplate>
    </Setter.Value>
  </Setter>
</Style>";
            return (Style)XamlReader.Parse(xaml);
        }

        public void PrepareEntrance(double horizontalOffset)
        {
            BeginAnimation(OpacityProperty, null);
            Opacity = 1;
            _contentRoot.RenderTransform = new TranslateTransform(horizontalOffset, 0);
        }

        public void UpdateBackdrop()
        {
            CancelPendingBackdropRefresh();
            BitmapSource source = BackdropCapture.Capture(this);
            if (source != null) _backdropImage.Source = source;
        }

        public void QueueBackdropRefresh()
        {
            Rect bounds = new Rect(Left, Top, Width, Height);
            int generation = Interlocked.Increment(ref _backdropRefreshGeneration);
            ThreadPool.QueueUserWorkItem(delegate
            {
                BitmapSource source = BackdropCapture.Capture(bounds);
                if (source == null) return;
                try
                {
                    Dispatcher.BeginInvoke(DispatcherPriority.Background, new Action(delegate
                    {
                        if (generation != _backdropRefreshGeneration) return;
                        _backdropImage.Source = source;
                    }));
                }
                catch { }
            });
        }

        public void CancelPendingBackdropRefresh()
        {
            Interlocked.Increment(ref _backdropRefreshGeneration);
        }

        public void PlayEntrance()
        {
            BeginAnimation(OpacityProperty, null);
            Opacity = 1;
            var transform = _contentRoot.RenderTransform as TranslateTransform;
            if (!SystemParameters.ClientAreaAnimation)
            {
                if (transform != null) transform.X = 0;
                return;
            }
            if (transform != null)
            {
                transform.BeginAnimation(TranslateTransform.XProperty, new DoubleAnimation
                {
                    To = 0,
                    Duration = TimeSpan.FromMilliseconds(90),
                    EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
                }, HandoffBehavior.SnapshotAndReplace);
            }
        }

        public void SetLoading()
        {
            _status.Text = "正在读取当前 Codex 对话…";
            _status.Foreground = WarmGlassTheme.AccentDark;
        }

        public void SetMessages(List<MessageEntry> messages, string error)
        {
            _messages = messages ?? new List<MessageEntry>();
            _status.Text = error ?? ViewModeLabel() + " · 点击任意一行即可定位";
            _status.Foreground = error == null ? WarmGlassTheme.MutedInk : WarmGlassTheme.Error;
            ApplyFilter();
            CompleteManualRefresh(error == null);
        }

        public void SetError(string error)
        {
            _status.Text = error;
            _status.Foreground = WarmGlassTheme.Error;
        }

        public void FocusDirectory()
        {
            Activate();
            if (_list.Items.Count > 0)
            {
                _list.SelectedIndex = Math.Max(0, _list.Items.Count - 1);
                ((ListBoxItem)_list.Items[_list.SelectedIndex]).Focus();
                _list.ScrollIntoView(_list.Items[_list.SelectedIndex]);
            }
            else _filter.Focus();
        }

        private void ApplyFilter()
        {
            string query = (_filter.Text ?? "").Trim();
            _list.Items.Clear();
            foreach (MessageEntry message in _messages)
            {
                if (_viewMode == ConversationViewMode.AssistantOnly && message.Role != MessageRole.Assistant)
                    continue;
                if (_viewMode == ConversationViewMode.UserOnly && message.Role != MessageRole.User)
                    continue;
                if (query.Length > 0 && message.Preview.IndexOf(query, StringComparison.CurrentCultureIgnoreCase) < 0)
                    continue;
                _list.Items.Add(CreateItem(message));
            }
            _count.Text = _list.Items.Count == _messages.Count
                ? _messages.Count + " 条"
                : _list.Items.Count + " / " + _messages.Count + " 条";
        }

        private void BeginManualRefresh()
        {
            if (_manualRefreshPending) return;
            if (_refreshResetTimer != null) _refreshResetTimer.Stop();
            _manualRefreshPending = true;
            _refreshButton.Content = "刷新中";
            _refreshButton.IsEnabled = false;
            _refreshButton.Background = WarmGlassTheme.UserBubble;
            _refreshButton.BorderBrush = WarmGlassTheme.Accent;
            _refreshButton.Foreground = WarmGlassTheme.AccentDark;
            AutomationProperties.SetName(_refreshButton, "正在刷新对话目录");
            AnimateRefreshScale(1, 120);
            _controller.Refresh();
        }

        private void CompleteManualRefresh(bool success)
        {
            if (!_manualRefreshPending) return;
            _manualRefreshPending = false;
            _refreshButton.IsEnabled = true;
            _refreshButton.Content = success ? "完成" : "重试";
            _refreshButton.Background = success
                ? WarmGlassTheme.Accent
                : WarmGlassTheme.Solid("#EFFFF0EC");
            _refreshButton.BorderBrush = success ? WarmGlassTheme.AccentDark : WarmGlassTheme.Error;
            _refreshButton.Foreground = success ? Brushes.White : WarmGlassTheme.Error;
            AutomationProperties.SetName(_refreshButton, success ? "对话目录刷新完成" : "刷新失败，点击重试");
            AnimateRefreshScale(1, 120);

            if (_refreshResetTimer != null) _refreshResetTimer.Stop();
            _refreshResetTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(success ? 900 : 1400)
            };
            _refreshResetTimer.Tick += delegate
            {
                _refreshResetTimer.Stop();
                if (!_manualRefreshPending) RestoreRefreshIdleState();
            };
            _refreshResetTimer.Start();
        }

        private void ShowRefreshPressedState()
        {
            if (_manualRefreshPending || !_refreshButton.IsEnabled) return;
            _refreshButton.Background = WarmGlassTheme.Accent;
            _refreshButton.BorderBrush = WarmGlassTheme.AccentDark;
            _refreshButton.Foreground = Brushes.White;
            AnimateRefreshScale(0.96, 70);
        }

        private void ReleaseRefreshPressedState()
        {
            AnimateRefreshScale(1, 120);
            if (!_manualRefreshPending && _refreshButton.Content as string == "刷新")
                RestoreRefreshIdleState();
        }

        private void RestoreRefreshIdleState()
        {
            _refreshButton.Content = "刷新";
            _refreshButton.IsEnabled = true;
            _refreshButton.Background = WarmGlassTheme.Card;
            _refreshButton.BorderBrush = WarmGlassTheme.EdgeGradient();
            _refreshButton.Foreground = WarmGlassTheme.Ink;
            AutomationProperties.SetName(_refreshButton, "刷新对话目录");
        }

        private void AnimateRefreshScale(double targetScale, int durationMilliseconds)
        {
            var transform = _refreshButton.RenderTransform as ScaleTransform;
            if (transform == null || !SystemParameters.ClientAreaAnimation) return;
            var easing = new QuadraticEase { EasingMode = EasingMode.EaseOut };
            transform.BeginAnimation(ScaleTransform.ScaleXProperty, new DoubleAnimation
            {
                To = targetScale,
                Duration = TimeSpan.FromMilliseconds(durationMilliseconds),
                EasingFunction = easing
            }, HandoffBehavior.SnapshotAndReplace);
            transform.BeginAnimation(ScaleTransform.ScaleYProperty, new DoubleAnimation
            {
                To = targetScale,
                Duration = TimeSpan.FromMilliseconds(durationMilliseconds),
                EasingFunction = easing
            }, HandoffBehavior.SnapshotAndReplace);
        }

        private void CycleViewMode()
        {
            if (_viewMode == ConversationViewMode.All)
                _viewMode = ConversationViewMode.AssistantOnly;
            else if (_viewMode == ConversationViewMode.AssistantOnly)
                _viewMode = ConversationViewMode.UserOnly;
            else
                _viewMode = ConversationViewMode.All;

            UpdateModeButton();
            ApplyFilter();
            _status.Text = ViewModeLabel() + " · 点击任意一行即可定位";
            _status.Foreground = WarmGlassTheme.MutedInk;
        }

        private void UpdateModeButton()
        {
            string shortLabel = _viewMode == ConversationViewMode.All
                ? "全部 ⇄"
                : (_viewMode == ConversationViewMode.AssistantOnly ? "模型 ⇄" : "用户 ⇄");
            _modeButton.Content = shortLabel;
            string accessibleName = "对话视图：" + ViewModeLabel() + "，点击切换";
            AutomationProperties.SetName(_modeButton, accessibleName);
            AutomationProperties.SetHelpText(_modeButton, "依次切换全部对话、只看大模型、只看用户");
            _modeButton.ToolTip = accessibleName;
        }

        private string ViewModeLabel()
        {
            if (_viewMode == ConversationViewMode.AssistantOnly) return "只看大模型";
            if (_viewMode == ConversationViewMode.UserOnly) return "只看用户";
            return "全部对话";
        }

        private ListBoxItem CreateItem(MessageEntry message)
        {
            var item = new ListBoxItem
            {
                Tag = message,
                Height = 58,
                Padding = new Thickness(0),
                Margin = new Thickness(0, 2, 0, 2),
                Background = Brushes.Transparent,
                BorderThickness = new Thickness(0),
                ToolTip = message.Preview,
                HorizontalContentAlignment = HorizontalAlignment.Stretch,
                FocusVisualStyle = null
            };
            AutomationProperties.SetName(item,
                (message.Role == MessageRole.User ? "你说" : "ChatGPT 回复") + "：" + message.Preview);
            item.Template = CleanListItemTemplate();
            item.PreviewMouseLeftButtonUp += delegate { Navigate(message); };

            bool isUser = message.Role == MessageRole.User;
            Brush baseBubble = isUser ? WarmGlassTheme.UserBubble : WarmGlassTheme.AssistantBubble;
            Brush hoverBubble = isUser ? WarmGlassTheme.UserBubbleHover : WarmGlassTheme.AssistantBubbleHover;
            double bubbleWidth = Math.Max(148, Math.Min(292, 42 + message.Preview.Length * 12.5));

            var row = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = isUser ? HorizontalAlignment.Right : HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Center
            };
            var card = new Border
            {
                Width = bubbleWidth,
                Height = 48,
                Background = baseBubble,
                BorderBrush = WarmGlassTheme.EdgeGradient(),
                BorderThickness = new Thickness(1.1, 1.1, 1.8, 1.8),
                CornerRadius = isUser ? new CornerRadius(15, 7, 15, 15) : new CornerRadius(7, 15, 15, 15),
                Padding = new Thickness(9, 2, 9, 2),
                Effect = new DropShadowEffect
                {
                    Color = (Color)ColorConverter.ConvertFromString(isUser ? "#5B9D5B2B" : "#473E2A20"),
                    BlurRadius = 7,
                    ShadowDepth = 2,
                    Opacity = 0.18
                }
            };
            var grid = new Grid();
            if (isUser)
            {
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(28) });
            }
            else
            {
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(28) });
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            }
            var badge = new Border
            {
                Background = isUser ? WarmGlassTheme.Ink : WarmGlassTheme.Accent,
                CornerRadius = new CornerRadius(11),
                Width = 30,
                Height = 30,
                Margin = isUser ? new Thickness(8, 0, 0, 0) : new Thickness(0, 0, 8, 0),
                VerticalAlignment = VerticalAlignment.Center,
                Child = new TextBlock
                {
                    Text = isUser ? "你" : "AI",
                    Foreground = Brushes.White,
                    FontSize = isUser ? 12 : 10,
                    FontWeight = FontWeights.SemiBold,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center
                }
            };
            AutomationProperties.SetName(badge, isUser ? "用户" : "大模型");

            var preview = new TextBlock
            {
                Text = message.Preview,
                Foreground = WarmGlassTheme.Ink,
                FontSize = 13,
                TextTrimming = TextTrimming.CharacterEllipsis,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(3, 0, 3, 0)
            };
            var number = new TextBlock
            {
                Text = message.Number.ToString(CultureInfo.InvariantCulture),
                Foreground = WarmGlassTheme.QuietInk,
                FontSize = 10,
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Center
            };
            Grid.SetColumn(preview, isUser ? 0 : 1);
            Grid.SetColumn(number, isUser ? 1 : 0);
            grid.Children.Add(preview);
            grid.Children.Add(number);
            card.Child = grid;
            if (isUser)
            {
                row.Children.Add(card);
                row.Children.Add(badge);
            }
            else
            {
                row.Children.Add(badge);
                row.Children.Add(card);
            }
            item.Content = row;

            Action updateVisualState = delegate
            {
                bool focused = item.IsKeyboardFocusWithin;
                bool selected = item.IsSelected;
                bool hovered = item.IsMouseOver;
                card.Background = focused || selected
                    ? WarmGlassTheme.CardSelected
                    : (hovered ? hoverBubble : baseBubble);
                card.BorderBrush = focused ? WarmGlassTheme.Accent : WarmGlassTheme.EdgeGradient();
                card.BorderThickness = focused ? new Thickness(2) : new Thickness(1.1, 1.1, 1.8, 1.8);
            };
            item.MouseEnter += delegate { updateVisualState(); };
            item.MouseLeave += delegate { updateVisualState(); };
            item.GotKeyboardFocus += delegate { updateVisualState(); };
            item.LostKeyboardFocus += delegate { updateVisualState(); };
            item.Selected += delegate { updateVisualState(); };
            item.Unselected += delegate { updateVisualState(); };
            return item;
        }

        private static ControlTemplate CleanListItemTemplate()
        {
            var presenter = new FrameworkElementFactory(typeof(ContentPresenter));
            presenter.SetValue(ContentPresenter.HorizontalAlignmentProperty, HorizontalAlignment.Stretch);
            presenter.SetValue(ContentPresenter.VerticalAlignmentProperty, VerticalAlignment.Stretch);
            presenter.SetValue(ContentPresenter.MarginProperty, new TemplateBindingExtension(Control.PaddingProperty));
            return new ControlTemplate(typeof(ListBoxItem)) { VisualTree = presenter };
        }

        private void NavigateSelected()
        {
            var item = _list.SelectedItem as ListBoxItem;
            if (item != null) Navigate((MessageEntry)item.Tag);
        }

        private void Navigate(MessageEntry message)
        {
            _controller.Navigate(message);
        }

        private static Button HeaderButton(string text, string accessibleName, bool dark)
        {
            var button = new Button
            {
                Content = text,
                MinWidth = dark ? 36 : 48,
                Width = dark ? 36 : double.NaN,
                Height = 36,
                Margin = new Thickness(5, 0, 0, 0),
                Padding = new Thickness(8, 0, 8, 0),
                Background = dark ? WarmGlassTheme.Ink : WarmGlassTheme.Card,
                BorderBrush = dark ? WarmGlassTheme.GlassBorder : WarmGlassTheme.EdgeGradient(),
                BorderThickness = dark ? new Thickness(1.5) : new Thickness(1.1, 1.1, 1.8, 1.8),
                Foreground = dark ? Brushes.White : WarmGlassTheme.Ink,
                FontSize = 12,
                VerticalAlignment = VerticalAlignment.Center,
                FocusVisualStyle = null,
                Cursor = Cursors.Hand
            };
            button.Template = RoundedButtonTemplate(dark ? 18 : 12, dark);
            AutomationProperties.SetName(button, accessibleName);
            return button;
        }

        private static ControlTemplate RoundedButtonTemplate(double radius, bool dark)
        {
            var chrome = new FrameworkElementFactory(typeof(Border));
            chrome.Name = "Chrome";
            chrome.SetValue(Border.CornerRadiusProperty, new CornerRadius(radius));
            chrome.SetValue(Border.BackgroundProperty, new TemplateBindingExtension(Control.BackgroundProperty));
            chrome.SetValue(Border.BorderBrushProperty, new TemplateBindingExtension(Control.BorderBrushProperty));
            chrome.SetValue(Border.BorderThicknessProperty, new TemplateBindingExtension(Control.BorderThicknessProperty));

            var presenter = new FrameworkElementFactory(typeof(ContentPresenter));
            presenter.SetValue(ContentPresenter.HorizontalAlignmentProperty, HorizontalAlignment.Center);
            presenter.SetValue(ContentPresenter.VerticalAlignmentProperty, VerticalAlignment.Center);
            presenter.SetValue(ContentPresenter.MarginProperty, new TemplateBindingExtension(Control.PaddingProperty));
            chrome.AppendChild(presenter);

            var template = new ControlTemplate(typeof(Button)) { VisualTree = chrome };
            var hover = new Trigger { Property = UIElement.IsMouseOverProperty, Value = true };
            hover.Setters.Add(new Setter(Control.BackgroundProperty,
                dark ? WarmGlassTheme.Solid("#4B4039") : WarmGlassTheme.CardHover));
            hover.Setters.Add(new Setter(Control.BorderBrushProperty, WarmGlassTheme.Accent));
            template.Triggers.Add(hover);
            var focus = new Trigger { Property = UIElement.IsKeyboardFocusedProperty, Value = true };
            focus.Setters.Add(new Setter(Control.BorderBrushProperty, WarmGlassTheme.Accent));
            focus.Setters.Add(new Setter(Control.BorderThicknessProperty, new Thickness(2)));
            template.Triggers.Add(focus);
            var pressed = new Trigger { Property = Button.IsPressedProperty, Value = true };
            pressed.Setters.Add(new Setter(Control.BackgroundProperty, WarmGlassTheme.CardSelected));
            template.Triggers.Add(pressed);
            return template;
        }
    }

    internal sealed class NavigatorController
    {
        private readonly AutomationMessageService _service = new AutomationMessageService();
        private readonly object _refreshSync = new object();
        private RegisteredWaitHandle _activationRegistration;
        private bool? _panelOnLeft;
        private int _refreshGeneration;
        private bool _refreshWorkerRunning;
        public BubbleWindow Bubble { get; private set; }
        public DirectoryWindow Panel { get; private set; }

        public NavigatorController(EventWaitHandle activationEvent)
        {
            Bubble = new BubbleWindow(this);
            Panel = new DirectoryWindow(this);
            _activationRegistration = ThreadPool.RegisterWaitForSingleObject(
                activationEvent,
                delegate
                {
                    try { Bubble.Dispatcher.BeginInvoke(new Action(TogglePanel)); } catch { }
                },
                null,
                Timeout.Infinite,
                false);
            Bubble.Closed += delegate
            {
                if (_activationRegistration != null) _activationRegistration.Unregister(null);
                try { Panel.Close(); } catch { }
                Application.Current.Shutdown();
            };
        }

        public void Start()
        {
            Panel.Prewarm();
            Bubble.Show();
            Bubble.SetExpanded(false);
            RepositionPanel();
            Panel.QueueBackdropRefresh();
        }

        public void TogglePanel()
        {
            if (Panel.IsVisible) HidePanel();
            else ShowPanel(true);
        }

        public void ShowPanel(bool refresh)
        {
            bool opening = !Panel.IsVisible;
            if (opening) _panelOnLeft = null;
            RepositionPanel();
            if (opening)
            {
                Panel.CancelPendingBackdropRefresh();
                Panel.PrepareEntrance(_panelOnLeft == true ? 4 : -4);
                Panel.Show();
                Panel.PlayEntrance();
            }
            Bubble.SetExpanded(true);
            Panel.Activate();
            if (refresh) Refresh();
            else Panel.FocusDirectory();
        }

        public void HidePanel()
        {
            if (Panel.IsVisible)
            {
                Panel.Hide();
                Panel.QueueBackdropRefresh();
            }
            Bubble.SetExpanded(false);
            _panelOnLeft = null;
        }

        public void Refresh()
        {
            Panel.SetLoading();
            bool startWorker;
            lock (_refreshSync)
            {
                _refreshGeneration++;
                startWorker = !_refreshWorkerRunning;
                if (startWorker) _refreshWorkerRunning = true;
            }
            if (startWorker) ThreadPool.QueueUserWorkItem(delegate { RunRefreshWorker(); });
        }

        private void RunRefreshWorker()
        {
            while (true)
            {
                int generation;
                lock (_refreshSync) generation = _refreshGeneration;

                ReadResult result = _service.ReadMessages();
                bool publish;
                lock (_refreshSync)
                {
                    publish = generation == _refreshGeneration;
                    if (publish) _refreshWorkerRunning = false;
                }
                if (!publish) continue;

                try
                {
                    Panel.Dispatcher.BeginInvoke(DispatcherPriority.Background, new Action(delegate
                    {
                        lock (_refreshSync)
                        {
                            if (generation != _refreshGeneration) return;
                        }
                        Panel.SetMessages(result.Messages, result.Error);
                        Bubble.SetStatus(result.Error ?? ("已读取 " + result.Messages.Count + " 条对话"));
                        if (Panel.IsVisible) Panel.FocusDirectory();
                    }));
                }
                catch { }
                return;
            }
        }

        public void PreparePanelBackdrop()
        {
            if (Panel == null || Panel.IsVisible) return;
            RepositionPanel();
            Panel.QueueBackdropRefresh();
        }

        public void Navigate(MessageEntry message)
        {
            HidePanel();
            string error = _service.NavigateTo(message);
            if (error == null)
            {
                Bubble.SetStatus("已定位到第 " + message.Number + " 条对话");
            }
            else
            {
                Bubble.SetStatus(error);
                ShowPanel(false);
                Panel.SetError(error);
            }
        }

        public void RepositionPanel()
        {
            if (Panel == null || Bubble == null) return;
            Rect area = SystemParameters.WorkArea;
            const double gap = 12;
            double leftRoom = Bubble.Left - area.Left;
            double rightRoom = area.Right - (Bubble.Left + Bubble.Width);
            double requiredRoom = Panel.Width + gap;

            if (!_panelOnLeft.HasValue)
            {
                if (leftRoom >= requiredRoom && rightRoom < requiredRoom) _panelOnLeft = true;
                else if (rightRoom >= requiredRoom && leftRoom < requiredRoom) _panelOnLeft = false;
                else _panelOnLeft = leftRoom >= rightRoom;
            }
            else if (_panelOnLeft == true && leftRoom < requiredRoom && rightRoom >= requiredRoom)
                _panelOnLeft = false;
            else if (_panelOnLeft == false && rightRoom < requiredRoom && leftRoom >= requiredRoom)
                _panelOnLeft = true;

            double left = _panelOnLeft == true
                ? Bubble.Left - Panel.Width - gap
                : Bubble.Left + Bubble.Width + gap;
            left = Math.Max(area.Left, Math.Min(left, area.Right - Panel.Width));
            const double headerCenterFromPanelTop = 51;
            double top = Bubble.Top + Bubble.Height / 2 - headerCenterFromPanelTop;
            top = Math.Max(area.Top, Math.Min(top, area.Bottom - Panel.Height));
            Panel.Left = left;
            Panel.Top = top;
        }

        public void MovePanelGroup(Point panelStart, Point bubbleStart, Vector desiredDelta)
        {
            Rect area = SystemParameters.WorkArea;
            double groupLeft = Math.Min(panelStart.X, bubbleStart.X);
            double groupTop = Math.Min(panelStart.Y, bubbleStart.Y);
            double groupRight = Math.Max(panelStart.X + Panel.Width, bubbleStart.X + Bubble.Width);
            double groupBottom = Math.Max(panelStart.Y + Panel.Height, bubbleStart.Y + Bubble.Height);
            double deltaX = Math.Max(area.Left - groupLeft,
                Math.Min(desiredDelta.X, area.Right - groupRight));
            double deltaY = Math.Max(area.Top - groupTop,
                Math.Min(desiredDelta.Y, area.Bottom - groupBottom));

            Panel.Left = panelStart.X + deltaX;
            Panel.Top = panelStart.Y + deltaY;
            Bubble.Left = bubbleStart.X + deltaX;
            Bubble.Top = bubbleStart.Y + deltaY;
        }

        public void CommitPanelGroupMove()
        {
            LocalSettings.SavePosition(Bubble.Left, Bubble.Top);
            RefreshBackdrop();
        }

        public void NudgePanelGroup(Vector delta)
        {
            MovePanelGroup(
                new Point(Panel.Left, Panel.Top),
                new Point(Bubble.Left, Bubble.Top),
                delta);
            CommitPanelGroupMove();
        }

        public void RefreshBackdrop()
        {
            if (Panel == null) return;
            if (!Panel.IsVisible)
            {
                Panel.QueueBackdropRefresh();
                return;
            }
            Panel.BeginAnimation(UIElement.OpacityProperty, null);
            Panel.Opacity = 0;
            Panel.Dispatcher.Invoke(DispatcherPriority.Render, new Action(delegate { }));
            Panel.UpdateBackdrop();
            Panel.Opacity = 1;
        }

        public void Exit()
        {
            LocalSettings.SavePosition(Bubble.Left, Bubble.Top);
            Bubble.Close();
        }
    }

    internal static class Program
    {
        private static void Main()
        {
            bool created;
            using (var mutex = new Mutex(true, "Local\\CodexConversationNavigator", out created))
            {
                bool eventCreated;
                using (var activationEvent = new EventWaitHandle(
                    false,
                    EventResetMode.AutoReset,
                    "Local\\CodexConversationNavigator.Activate",
                    out eventCreated))
                {
                    if (!created)
                    {
                        activationEvent.Set();
                        return;
                    }
                    var uiThread = new Thread(new ThreadStart(delegate { RunInterface(activationEvent); }));
                    uiThread.SetApartmentState(ApartmentState.STA);
                    uiThread.Start();
                    uiThread.Join();
                }
            }
        }

        private static void RunInterface(EventWaitHandle activationEvent)
        {
            InteractiveDesktop.Attach();
            var application = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
            var controller = new NavigatorController(activationEvent);
            controller.Start();
            application.Run();
        }
    }
}
