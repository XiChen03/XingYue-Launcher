using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.IO.Compression;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Windows.Forms;

namespace XingYueRelay
{
    internal static class Program
    {
        internal const string CoreFileName = "easytier-core.exe";
        internal const string ResourceName = "EasyTierCore.zip";
        internal const int DefaultPort = 11010;

        /// <summary>本程序所在的文件夹。</summary>
        internal static readonly string BaseDir = AppDomain.CurrentDomain.BaseDirectory;

        /// <summary>解压出来的联机模块存放位置（与启动器一样放在程序目录下，整个文件夹搬走即可带走配置）。</summary>
        internal static string WorkDir = Path.Combine(BaseDir, "XingYue", "中转节点");

        /// <summary>旧版本把中转节点数据放在 AppData 里，用于迁移与回退。</summary>
        internal static readonly string LegacyWorkDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "XingYue", "中转节点");

        /// <summary>XingYue Launcher 已经释放好的联机模块（放在启动器目录），存在时直接复用，省一份磁盘空间。</summary>
        internal static readonly string LauncherCore = Path.Combine(
            BaseDir, "XingYue", "EasyTier", "联机模块.exe");

        /// <summary>旧版 XingYue Launcher 把联机模块放在 AppData 里，兼容一下。</summary>
        internal static readonly string LauncherCoreLegacy = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "XingYue", "EasyTier", "联机模块.exe");

        internal static string ConfigPath = Path.Combine(WorkDir, "relay.ini");

        /// <summary>
        /// 定位数据目录：优先用程序目录下的 XingYue\中转节点\；程序目录不可写时（例如直接从压缩包运行、
        /// 或放在 Program Files）退回旧版的 AppData 位置。顺带迁移旧配置并清掉旧目录。
        /// </summary>
        private static void EnsureWorkDir()
        {
            try
            {
                Directory.CreateDirectory(WorkDir);
                string probe = Path.Combine(WorkDir, ".writecheck");
                File.WriteAllText(probe, string.Empty);
                File.Delete(probe);
            }
            catch
            {
                WorkDir = LegacyWorkDir;
                ConfigPath = Path.Combine(WorkDir, "relay.ini");
                return;
            }
            try
            {
                if (!Directory.Exists(LegacyWorkDir)) return;
                if (!File.Exists(ConfigPath))
                {
                    string legacyConfig = Path.Combine(LegacyWorkDir, "relay.ini");
                    if (File.Exists(legacyConfig)) File.Copy(legacyConfig, ConfigPath);
                }
                Directory.Delete(LegacyWorkDir, true);
            }
            catch
            {
                // 旧目录删除失败（例如旧版还在运行）不影响使用
            }
        }

        [STAThread]
        private static void Main()
        {
            EnsureWorkDir();
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new MainForm());
        }
    }

    internal sealed class MainForm : Form
    {
        private readonly NumericUpDown numPort = new NumericUpDown();
        private readonly TextBox txtIp = new TextBox();
        private readonly CheckBox chkRelayOnly = new CheckBox();
        private readonly Button btnStart = new Button();
        private readonly Button btnStop = new Button();
        private readonly Button btnCopy = new Button();
        private readonly Button btnIp = new Button();
        private readonly RichTextBox txtLog = new RichTextBox();
        private readonly Label labStatus = new Label();
        private readonly Label labPort = new Label();
        private readonly Label labIp = new Label();
        private readonly ProcessHolder holder = new ProcessHolder();
        private bool isStopping;

        public MainForm()
        {
            BuildUi();
            LoadConfig();
            Log("XingYue联机中转节点已就绪。");
            Log("说明：本程序在你填写自定义节点的地址后，为房主与加入者提供中转/P2P 打洞，不需要双方都有公网 IP。");
            LogLanAddresses();
        }

        private sealed class ProcessHolder
        {
            public Process Process;
        }

        #region 界面

        private void BuildUi()
        {
            Text = "XingYue联机中转节点";
            StartPosition = FormStartPosition.CenterScreen;
            ClientSize = new Size(640, 500);
            MinimumSize = new Size(580, 420);
            Font = new Font("Microsoft YaHei UI", 9F);
            BackColor = Color.White;

            labPort.Text = "监听端口";
            labPort.SetBounds(14, 16, 66, 24);
            labPort.TextAlign = ContentAlignment.MiddleLeft;

            numPort.SetBounds(84, 14, 88, 26);
            numPort.Minimum = 1;
            numPort.Maximum = 65535;
            numPort.Value = Program.DefaultPort;

            chkRelayOnly.SetBounds(186, 15, 300, 24);
            chkRelayOnly.Text = "仅做中转（不承载流量，推荐）";
            chkRelayOnly.Checked = true;

            labIp.Text = "公网地址";
            labIp.SetBounds(14, 50, 66, 24);
            labIp.TextAlign = ContentAlignment.MiddleLeft;

            txtIp.SetBounds(84, 48, 250, 26);
            txtIp.Text = "";
            txtIp.ForeColor = Color.DimGray;
            txtIp.TextChanged += (s, e) => UpdateState();

            btnIp.SetBounds(342, 47, 92, 27);
            btnIp.Text = "自动获取";
            btnIp.Click += OnDetectIpClick;

            btnStart.SetBounds(14, 86, 110, 32);
            btnStart.Text = "启动节点";
            btnStart.Click += OnStartClick;

            btnStop.SetBounds(132, 86, 110, 32);
            btnStop.Text = "停止节点";
            btnStop.Enabled = false;
            btnStop.Click += OnStopClick;

            btnCopy.SetBounds(250, 86, 150, 32);
            btnCopy.Text = "复制节点地址";
            btnCopy.Click += OnCopyClick;

            labStatus.SetBounds(412, 90, 214, 24);
            labStatus.Text = "未启动";
            labStatus.ForeColor = Color.DimGray;
            labStatus.TextAlign = ContentAlignment.MiddleLeft;

            txtLog.SetBounds(14, 130, 612, 356);
            txtLog.ReadOnly = true;
            txtLog.BackColor = Color.FromArgb(248, 248, 250);
            txtLog.Font = new Font("Consolas", 9F);
            txtLog.BorderStyle = BorderStyle.FixedSingle;
            txtLog.WordWrap = true;
            txtLog.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Bottom;

            Controls.AddRange(new Control[]
            {
                labPort, numPort, chkRelayOnly, labIp, txtIp, btnIp,
                btnStart, btnStop, btnCopy, labStatus, txtLog
            });

            FormClosing += (s, e) => StopServer();
        }

        #endregion

        #region 日志

        private void Log(string line)
        {
            if (InvokeRequired)
            {
                try { BeginInvoke((Action<string>)Log, line); } catch { }
                return;
            }
            txtLog.AppendText($"{DateTime.Now:HH:mm:ss}  {line}\r\n");
            txtLog.SelectionStart = txtLog.TextLength;
            txtLog.ScrollToCaret();
        }

        #endregion

        #region 端口与地址

        private int Port
        {
            get { return (int)numPort.Value; }
        }

        private string Address
        {
            get
            {
                string ip = txtIp.Text.Trim();
                return string.IsNullOrEmpty(ip) ? null : $"tcp://{ip}:{Port}";
            }
        }

        private void LogLanAddresses()
        {
            try
            {
                var sb = new StringBuilder();
                foreach (var addr in Dns.GetHostAddresses(Dns.GetHostName()))
                {
                    if (addr.AddressFamily != AddressFamily.InterNetwork) continue;
                    string text = addr.ToString();
                    if (text.StartsWith("127.")) continue;
                    if (sb.Length > 0) sb.Append("，");
                    sb.Append($"tcp://{text}:{Port}");
                }
                if (sb.Length > 0)
                {
                    Log("本机内网地址：" + sb);
                }
            }
            catch (Exception ex)
            {
                Log("获取本机地址失败：" + ex.Message);
            }
            Log($"如果要让外网的朋友连进来，请在路由器/服务器防火墙放行 TCP 和 UDP 的 {Port} 端口。");
            UpdateState();
        }

        private void OnDetectIpClick(object sender, EventArgs e)
        {
            btnIp.Enabled = false;
            Log("正在获取公网 IP……");
            ThreadPool.QueueUserWorkItem(_ =>
            {
                string ip = null;
                string error = null;
                string[] urls =
                {
                    "https://api.ipify.org",
                    "https://ipv4.icanhazip.com",
                    "https://api64.ipify.org"
                };
                foreach (string url in urls)
                {
                    try
                    {
                        var req = (HttpWebRequest)WebRequest.Create(url);
                        req.Timeout = 6000;
                        req.ReadWriteTimeout = 6000;
                        req.UserAgent = "XingYueRelay/1.0";
                        using (var resp = req.GetResponse())
                        using (var reader = new StreamReader(resp.GetResponseStream(), Encoding.UTF8))
                        {
                            string text = reader.ReadToEnd().Trim();
                            if (IPAddress.TryParse(text, out IPAddress parsed))
                            {
                                ip = parsed.ToString();
                                break;
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        error = ex.Message;
                    }
                }
                BeginInvoke((Action)(() =>
                {
                    btnIp.Enabled = true;
                    if (ip == null)
                    {
                        Log("获取公网 IP 失败：" + (error ?? "返回内容无法解析") + "，可以手动填写。");
                        return;
                    }
                    txtIp.Text = ip;
                    Log("公网 IP：" + ip + "，节点地址为 tcp://" + ip + ":" + Port);
                }));
            });
        }

        private void OnCopyClick(object sender, EventArgs e)
        {
            string addr = Address;
            if (addr == null)
            {
                MessageBox.Show(this,
                    "请先填写或自动获取服务器的公网 IP，再复制节点地址。",
                    "还没有地址", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            try
            {
                Clipboard.SetText(addr);
                Log("已复制节点地址：" + addr);
                MessageBox.Show(this,
                    "已复制节点地址：\r\n\r\n" + addr +
                    "\r\n\r\n把它填到 XingYue Launcher → 设置 → 联机 → 自定义节点 里，房主和加入者都填这一行即可。",
                    "已复制", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                Log("复制失败：" + ex.Message);
            }
        }

        #endregion

        #region 启动与停止

        private string BuildArguments()
        {
            var sb = new StringBuilder();
            sb.Append("--listeners tcp://0.0.0.0:").Append(Port).Append(' ');
            sb.Append("--listeners udp://0.0.0.0:").Append(Port).Append(' ');
            sb.Append("--rpc-portal 127.0.0.1:").Append(Port + 1).Append(' ');
            sb.Append("--no-tun ");
            if (chkRelayOnly.Checked)
            {
                sb.Append("--relay-network-whitelist --relay-all-peer-rpc");
            }
            return sb.ToString().Trim();
        }

        private static string EnsureCore()
        {
            // 优先复用 XingYue Launcher 已经释放好的联机模块
            if (File.Exists(Program.LauncherCore)) return Program.LauncherCore;
            if (File.Exists(Program.LauncherCoreLegacy)) return Program.LauncherCoreLegacy;
            string exe = Path.Combine(Program.WorkDir, Program.CoreFileName);
            if (File.Exists(exe)) return exe;
            Directory.CreateDirectory(Program.WorkDir);
            using (Stream stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(Program.ResourceName))
            {
                if (stream == null) throw new Exception("程序内没有找到内置的联机模块，文件可能已损坏。");
                string zip = Path.Combine(Path.GetTempPath(), "XingYueRelay_" + Guid.NewGuid().ToString("N") + ".zip");
                using (var file = File.Create(zip)) stream.CopyTo(file);
                try
                {
                    ZipFile.ExtractToDirectory(zip, Program.WorkDir);
                }
                finally
                {
                    try { File.Delete(zip); } catch { }
                }
            }
            if (!File.Exists(exe)) throw new Exception("释放联机模块失败，请检查磁盘权限。");
            return exe;
        }

        private void OnStartClick(object sender, EventArgs e)
        {
            if (holder.Process != null) return;
            try
            {
                string core = EnsureCore();
                Directory.CreateDirectory(Program.WorkDir);
                var info = new ProcessStartInfo(core, BuildArguments())
                {
                    WorkingDirectory = Program.WorkDir,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    StandardOutputEncoding = Encoding.UTF8,
                    StandardErrorEncoding = Encoding.UTF8
                };
                Log("联机模块：" + core);
                Log("启动参数：" + info.Arguments);
                var proc = new Process { StartInfo = info, EnableRaisingEvents = true };
                proc.OutputDataReceived += (s, e) => { if (e.Data != null) Log("[core] " + e.Data); };
                proc.ErrorDataReceived += (s, e) => { if (e.Data != null) Log("[core] " + e.Data); };
                proc.Exited += (s, e) =>
                {
                    if (isStopping) return;
                    Log("联机模块已退出（可能启动失败，请检查端口是否被占用）。");
                    holder.Process = null;
                    UpdateState();
                };
                proc.Start();
                proc.BeginOutputReadLine();
                proc.BeginErrorReadLine();
                holder.Process = proc;
                Log($"中转节点已启动，监听 {Port} 端口。");
                if (Address != null) Log("节点地址：" + Address);
                SaveConfig();
                UpdateState();
            }
            catch (Exception ex)
            {
                Log("启动失败：" + ex.Message);
                MessageBox.Show(this, "启动失败：" + ex.Message, "启动失败",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private void OnStopClick(object sender, EventArgs e)
        {
            StopServer();
        }

        private void StopServer()
        {
            Process proc = holder.Process;
            if (proc == null) return;
            isStopping = true;
            try
            {
                if (!proc.HasExited) proc.Kill();
            }
            catch { }
            try { proc.Dispose(); } catch { }
            holder.Process = null;
            isStopping = false;
            Log("中转节点已停止。");
            UpdateState();
        }

        private void UpdateState()
        {
            bool running = holder.Process != null;
            btnStart.Enabled = !running;
            btnStop.Enabled = running;
            numPort.Enabled = !running;
            chkRelayOnly.Enabled = !running;
            btnCopy.Enabled = Address != null;
            labStatus.Text = running ? $"运行中（{Port} 端口）" : "未启动";
            labStatus.ForeColor = running ? Color.SeaGreen : Color.DimGray;
        }

        #endregion

        #region 配置读写

        private void LoadConfig()
        {
            try
            {
                if (!File.Exists(Program.ConfigPath)) return;
                foreach (string line in File.ReadAllLines(Program.ConfigPath))
                {
                    int index = line.IndexOf('=');
                    if (index <= 0) continue;
                    string key = line.Substring(0, index).Trim();
                    string value = line.Substring(index + 1).Trim();
                    if (key == "Port" && int.TryParse(value, out int port) && port > 0 && port <= 65535)
                    {
                        numPort.Value = port;
                    }
                    else if (key == "RelayOnly")
                    {
                        chkRelayOnly.Checked = value == "1";
                    }
                    else if (key == "PublicIp")
                    {
                        txtIp.Text = value;
                    }
                }
            }
            catch (Exception ex)
            {
                Log("读取配置失败：" + ex.Message);
            }
        }

        private void SaveConfig()
        {
            try
            {
                Directory.CreateDirectory(Program.WorkDir);
                File.WriteAllLines(Program.ConfigPath, new[]
                {
                    "Port=" + Port,
                    "RelayOnly=" + (chkRelayOnly.Checked ? "1" : "0"),
                    "PublicIp=" + txtIp.Text.Trim()
                }, Encoding.UTF8);
            }
            catch (Exception ex)
            {
                Log("保存配置失败：" + ex.Message);
            }
        }

        #endregion
    }
}
