Imports System.IO
Imports System.Text.RegularExpressions

''' <summary>
''' 本地开服器：管理服务端目录、启动参数、备份、插件、玩家与文件。
''' 配置保存在 XingYue\Server\server.ini。
''' </summary>
Public Class ServerManager

    Public Shared ReadOnly Property DataDir As String = Paths.Base & "XingYue\Server\"
    ''' <summary>配置文件。用属性而不是字段：字段初始化按源码顺序执行，DataDir 可能还没赋值。</summary>
    Public Shared ReadOnly Property ConfigFile As String
        Get
            Return DataDir & "server.ini"
        End Get
    End Property

    '===============================
    '  配置
    '===============================

    Private Shared _Name As String = ""
    Private Shared _Dir As String = ""
    Private Shared _Java As String = ""
    Private Shared _Args As String = ""
    Private Shared _MinMem As Integer = 1024
    Private Shared _MaxMem As Integer = 2048
    Private Shared _Core As String = ""
    Private Shared _Mode As String = "java"
    Private Shared _Command As String = ""

    ''' <summary>实例显示名称。</summary>
    Public Shared Property Name As String
        Get
            Return If(_Name, "")
        End Get
        Set(value As String)
            _Name = If(value, "").Trim()
        End Set
    End Property

    ''' <summary>服务端所在文件夹。</summary>
    Public Shared Property Dir As String
        Get
            Return _Dir
        End Get
        Set(value As String)
            _Dir = If(value, "").TrimEnd("\"c)
            _Java = If(_Java, "")
            If _Core = "" Then _Core = DetectJar()
        End Set
    End Property

    ''' <summary>Java 路径，空则自动查找。</summary>
    Public Shared Property JavaPath As String
        Get
            Return _Java
        End Get
        Set(value As String)
            _Java = If(value, "").Trim()
        End Set
    End Property

    ''' <summary>额外 JVM 参数。</summary>
    Public Shared Property JvmArgs As String
        Get
            Return _Args
        End Get
        Set(value As String)
            _Args = If(value, "").Trim()
        End Set
    End Property

    Public Shared Property MinMemory As Integer
        Get
            Return _MinMem
        End Get
        Set(value As Integer)
            _MinMem = Math.Max(256, Math.Min(65536, value))
            If _MinMem > _MaxMem Then _MaxMem = _MinMem
        End Set
    End Property

    Public Shared Property MaxMemory As Integer
        Get
            Return _MaxMem
        End Get
        Set(value As Integer)
            _MaxMem = Math.Max(256, Math.Min(65536, value))
            If _MaxMem < _MinMem Then _MinMem = _MaxMem
        End Set
    End Property

    ''' <summary>兼容旧代码：读写最大内存。</summary>
    Public Shared Property Memory As Integer
        Get
            Return _MaxMem
        End Get
        Set(value As Integer)
            MaxMemory = value
            MinMemory = Math.Min(_MinMem, value)
        End Set
    End Property

    ''' <summary>服务端核心：jar 文件名，或 NeoForge/Forge 的 @args 文件。</summary>
    Public Shared Property Core As String
        Get
            Return _Core
        End Get
        Set(value As String)
            _Core = If(value, "").Trim()
        End Set
    End Property

    ''' <summary>兼容旧代码：jar 文件名。</summary>
    Public Shared Property JarName As String
        Get
            Return _Core
        End Get
        Set(value As String)
            _Core = value
        End Set
    End Property

    ''' <summary>启动方式：java / custom。</summary>
    Public Shared Property StartMode As String
        Get
            Return _Mode
        End Get
        Set(value As String)
            _Mode = If(value.ToLowerInvariant() = "custom", "custom", "java")
        End Set
    End Property

    ''' <summary>自定义启动命令（StartMode=custom 时使用，工作目录为服务端目录）。</summary>
    Public Shared Property CustomCommand As String
        Get
            Return _Command
        End Get
        Set(value As String)
            _Command = If(value, "").Trim()
        End Set
    End Property

    Public Shared Sub ReadConfig()
        Try
            Dim Lines As String() = If(FileUtils.Exists(ConfigFile), File.ReadAllLines(ConfigFile, Text.Encoding.UTF8), New String() {})
            For Each RawLine As String In Lines
                Dim Line As String = RawLine.Trim()
                If Line = "" OrElse Line.StartsWith("#") Then Continue For
                Dim Index As Integer = Line.IndexOf("="c)
                If Index <= 0 Then Continue For
                Dim Key As String = Line.Substring(0, Index).Trim()
                Dim Value As String = Line.Substring(Index + 1).Trim()
                Dim Number As Integer
                Select Case Key
                    Case "Name"
                        _Name = Value
                    Case "Dir"
                        _Dir = Value.TrimEnd("\"c)
                    Case "Java"
                        _Java = Value
                    Case "Args"
                        _Args = Value
                    Case "MinMem"
                        If Integer.TryParse(Value, Number) Then _MinMem = Number
                    Case "MaxMem"
                        If Integer.TryParse(Value, Number) Then _MaxMem = Number
                    Case "Core", "Jar"
                        _Core = Value
                    Case "Mode"
                        _Mode = If(Value.ToLowerInvariant() = "custom", "custom", "java")
                    Case "Command"
                        _Command = Value
                    Case "Memory" '旧版本只有单内存
                        If Integer.TryParse(Value, Number) Then
                            _MaxMem = Number
                            If _MinMem = 0 Then _MinMem = Math.Min(1024, Number)
                        End If
                End Select
            Next
            If _Dir = "" Then
                '没配置过时默认用启动器目录下的 XingYue\Server，核心也下载到这里
                Try
                    Directory.CreateDirectory(DataDir)
                    _Dir = DataDir.TrimEnd("\"c)
                Catch ex As Exception
                    Logger.Warn(ex, "创建默认服务端目录失败")
                End Try
            End If
            If _Core = "" Then _Core = DetectJar()
            If _Name = "" AndAlso _Dir <> "" Then _Name = IO.Path.GetFileName(_Dir)
        Catch ex As Exception
            Logger.Warn(ex, "读取开服器配置失败")
        End Try
    End Sub

    Public Shared Sub WriteConfig()
        Try
            Directory.CreateDirectory(DataDir)
            File.WriteAllLines(ConfigFile, {
                "# 星玥启动器 - 开服器配置",
                "Name=" & _Name,
                "Dir=" & _Dir,
                "Java=" & _Java,
                "Args=" & _Args,
                "MinMem=" & _MinMem.ToString(),
                "MaxMem=" & _MaxMem.ToString(),
                "Core=" & _Core,
                "Mode=" & _Mode,
                "Command=" & _Command
            }, Text.Encoding.UTF8)
        Catch ex As Exception
            Logger.Warn(ex, "保存开服器配置失败")
        End Try
    End Sub

    '===============================
    '  运行状态
    '===============================

    Private Shared _Process As Process
    Private Shared _StartTime As DateTime

    ''' <summary>控制台输出回调（在后台线程触发）。</summary>
    Public Shared Property OnLog As Action(Of String)
    ''' <summary>状态变化回调（在后台线程或 UI 线程触发，页面自己用 RunInUi 包一下）。</summary>
    Public Shared Property OnStateChanged As Action

    Public Shared Function IsRunning() As Boolean
        Try
            Return _Process IsNot Nothing AndAlso Not _Process.HasExited
        Catch
            Return False
        End Try
    End Function

    ''' <summary>已运行时长，未运行为 TimeSpan.Zero。</summary>
    Public Shared ReadOnly Property Uptime As TimeSpan
        Get
            If Not IsRunning() Then Return TimeSpan.Zero
            Return Date.Now - _StartTime
        End Get
    End Property

    ''' <summary>当前在线的玩家名单。</summary>
    Public Shared ReadOnly Property Players As New List(Of String)

    Public Shared Sub ClearPlayers()
        Players.Clear()
    End Sub

    '===============================
    '  查找
    '===============================

    ''' <summary>在 Dir 里找服务端 jar。</summary>
    Public Shared Function DetectJar() As String
        Try
            If _Dir = "" OrElse Not Directory.Exists(_Dir) Then Return ""
            Dim Files() As String = Directory.GetFiles(_Dir, "*.jar", SearchOption.TopDirectoryOnly)
            If Files.Length = 0 Then Return ""
            For Each File As String In Files
                If Path.GetFileName(File).ToLowerInvariant() = "server.jar" Then Return Path.GetFileName(File)
            Next
            For Each File As String In Files
                If Path.GetFileName(File).ToLowerInvariant().Contains("server") Then Return Path.GetFileName(File)
            Next
            Return Path.GetFileName(Files(0))
        Catch ex As Exception
            Logger.Warn(ex, "查找服务端 jar 失败")
            Return ""
        End Try
    End Function

    ''' <summary>列出可用的 java.exe（含版本号尽力识别）。</summary>
    Public Shared Function ListJava() As List(Of String)
        Dim Result As New List(Of String)
        Try
            Dim PathVar As String = Environment.GetEnvironmentVariable("PATH")
            If PathVar IsNot Nothing Then
                For Each Part As String In PathVar.Split(";"c)
                    Try
                        Dim Candidate As String = Path.Combine(Part.Trim(), "java.exe")
                        If FileUtils.Exists(Candidate) AndAlso Not Result.Contains(Candidate) Then Result.Add(Candidate)
                    Catch
                    End Try
                Next
            End If
            Dim ProgramFiles As String = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles)
            Dim ProgramFilesX86 As String = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86)
            Dim Roots As String() = {
                ProgramFiles & "\Java",
                ProgramFiles & "\Eclipse Adoptium",
                ProgramFiles & "\BellSoft\LibericaJDK",
                ProgramFiles & "\Microsoft",
                ProgramFiles & "\Zulu",
                ProgramFilesX86 & "\Java",
                Paths.AppData & ".minecraft\runtime"
            }
            For Each Root As String In Roots
                Try
                    If Not Directory.Exists(Root) Then Continue For
                    For Each Found As String In Directory.GetFiles(Root, "java.exe", SearchOption.AllDirectories)
                        If Not Result.Contains(Found) Then Result.Add(Found)
                    Next
                Catch
                End Try
            Next
        Catch
        End Try
        Return Result
    End Function

    ''' <summary>找一个可用的 java.exe。</summary>
    Public Shared Function FindJava() As String
        If _Java <> "" AndAlso FileUtils.Exists(_Java) Then Return _Java
        Dim All As List(Of String) = ListJava()
        If All.Count = 0 Then Return ""
        '优先 jdk/jre 目录里带版本号较大的（简单取第一个）
        Return All(0)
    End Function

    '===============================
    '  启动 / 停止
    '===============================

    Private Shared Function BuildJavaArguments() As String
        Dim Args As New List(Of String) From {
            "-Dfile.encoding=UTF-8", "-Dstdout.encoding=UTF-8", "-Dstderr.encoding=UTF-8",
            "-Dstdin.encoding=UTF-8", "-Dsun.stdout.encoding=UTF-8", "-Dsun.stderr.encoding=UTF-8", "-Dsun.stdin.encoding=UTF-8",
            $"-Xms{_MinMem}M", $"-Xmx{_MaxMem}M"
        }
        If _Args <> "" Then Args.Add(_Args)
        If _Core.StartsWith("@") Then
            'NeoForge/Forge 的 @args 文件：java @user_jvm_args.txt @libraries/.../win_args.txt nogui
            Dim CoreFile As String = Path.Combine(_Dir, _Core.TrimStart("@"c))
            If IO.File.Exists(CoreFile) Then
                Args.Add(_Core)
            Else
                Args.Add(_Core)
            End If
            Args.Add("nogui")
        Else
            Args.Add($"-jar ""{_Core}""")
            Args.Add("nogui")
        End If
        Return String.Join(" ", Args)
    End Function

    ''' <summary>启动服务端。调用前请保证 Dir 与 Core 有效。</summary>
    Public Shared Sub StartServer()
        If IsRunning() Then Return
        If _Dir = "" OrElse Not Directory.Exists(_Dir) Then Throw New Exception("请先选择服务端文件夹。")
        If _Mode = "custom" Then
            If _Command = "" Then Throw New Exception("请先填写自定义启动命令。")
        Else
            If _Core = "" Then _Core = DetectJar()
            If _Core = "" Then Throw New Exception("在所选文件夹里没有找到服务端核心，请在实例设置里指定。")
            If Not _Core.StartsWith("@") AndAlso Not IO.File.Exists(Path.Combine(_Dir, _Core)) Then Throw New Exception("服务端核心文件不存在：" & _Core)
        End If
        'EULA：没同意过就不启动，交给页面去询问
        Dim EulaFile As String = Path.Combine(_Dir, "eula.txt")
        If Not IO.File.Exists(EulaFile) Then Throw New ServerEulaException()
        If Not IO.File.ReadAllText(EulaFile).Contains("eula=true") Then Throw New ServerEulaException()
        Dim Java As String
        Dim Arguments As String
        If _Mode = "custom" Then
            Java = "cmd.exe"
            Arguments = "/c " & _Command
        Else
            Java = FindJava()
            If Java = "" Then Throw New Exception("没有找到 Java，请先安装 Java 或在实例设置里指定路径。")
            Arguments = BuildJavaArguments()
        End If
        Players.Clear()
        Dim Psi As New ProcessStartInfo(Java, Arguments) With {
            .UseShellExecute = False,
            .CreateNoWindow = True,
            .RedirectStandardOutput = True,
            .RedirectStandardError = True,
            .RedirectStandardInput = True,
            .StandardOutputEncoding = Text.Encoding.UTF8,
            .StandardErrorEncoding = Text.Encoding.UTF8,
            .WorkingDirectory = _Dir
        }
        Dim Proc As New Process With {.StartInfo = Psi}
        AddHandler Proc.OutputDataReceived,
            Sub(Sender As Object, E As DataReceivedEventArgs)
                If E.Data Is Nothing Then Return
                HandleLine(E.Data)
            End Sub
        AddHandler Proc.ErrorDataReceived,
            Sub(Sender As Object, E As DataReceivedEventArgs)
                If E.Data Is Nothing Then Return
                HandleLine(E.Data)
            End Sub
        Proc.Start()
        Proc.EnableRaisingEvents = True
        AddHandler Proc.Exited,
            Sub()
                '服务端自己退出（崩溃或正常关闭）时同步状态
                If _Process Is Proc Then
                    _Process = Nothing
                    Players.Clear()
                    RaiseState()
                End If
            End Sub
        Proc.BeginOutputReadLine()
        Proc.BeginErrorReadLine()
        _Process = Proc
        _StartTime = Date.Now
        Logger.Info($"开服器：已启动服务端（{Java} {Arguments}）")
        RaiseState()
    End Sub

    ''' <summary>写入 eula=true。</summary>
    Public Shared Sub AcceptEula()
        Try
            Directory.CreateDirectory(_Dir)
            IO.File.WriteAllText(Path.Combine(_Dir, "eula.txt"), "eula=true" & vbCrLf, Text.Encoding.UTF8)
        Catch ex As Exception
            Logger.Warn(ex, "写入 eula.txt 失败")
        End Try
    End Sub

    ''' <summary>停止服务端：先发 stop，等不到就强杀。</summary>
    Public Shared Sub StopServer()
        If Not IsRunning() Then Return
        Try
            SendCommand("stop")
        Catch
        End Try
        Dim Proc As Process = _Process
        If Proc Is Nothing Then Return
        Try
            If Not Proc.WaitForExit(15000) Then
                Logger.Warn("开服器：服务端 15 秒内没有退出，强制结束进程")
                Proc.Kill()
                Proc.WaitForExit(3000)
            End If
        Catch ex As Exception
            Logger.Warn(ex, "停止服务端失败")
        End Try
        _Process = Nothing
        Players.Clear()
        RaiseState()
    End Sub

    Public Shared Sub SendCommand(Command As String)
        If Not IsRunning() Then Return
        Try
            '直接写 UTF-8 字节，避免 StreamWriter 用系统默认编码（GBK）导致服务端收到乱码
            Dim Bytes As Byte() = Text.Encoding.UTF8.GetBytes(Command & vbLf)
            _Process.StandardInput.BaseStream.Write(Bytes, 0, Bytes.Length)
            _Process.StandardInput.BaseStream.Flush()
            If OnLog IsNot Nothing Then OnLog.Invoke("> " & Command)
        Catch ex As Exception
            Logger.Warn(ex, "向服务端发送指令失败")
        End Try
    End Sub

    '===============================
    '  输出解析
    '===============================

    Private Shared ReadOnly AnsiRegex As New Regex("\x1B\[[0-9;]*[A-Za-z]", RegexOptions.Compiled)

    Private Shared Sub HandleLine(RawLine As String)
        Dim Line As String = AnsiRegex.Replace(RawLine, "")
        Dim PlayersChanged As Boolean = False
        Try
            PlayersChanged = ParsePlayers(Line)
        Catch
        End Try
        If OnLog IsNot Nothing Then OnLog.Invoke(Line)
        If PlayersChanged Then RaiseState()
    End Sub

    ''' <summary>从日志行里解析玩家进出，返回是否发生了变化。</summary>
    Private Shared Function ParsePlayers(Line As String) As Boolean
        Dim Joined As Match = Regex.Match(Line, "([A-Za-z0-9_]{1,16}) joined the game")
        If Joined.Success Then
            Dim Name As String = Joined.Groups(1).Value
            If Not Players.Contains(Name) Then
                Players.Add(Name)
                Return True
            End If
            Return False
        End If
        Dim Left As Match = Regex.Match(Line, "([A-Za-z0-9_]{1,16}) left the game")
        If Left.Success Then Return Players.Remove(Left.Groups(1).Value)
        Return False
    End Function

    Private Shared Sub RaiseState()
        If OnStateChanged IsNot Nothing Then OnStateChanged.Invoke()
    End Sub

    '===============================
    '  server.properties
    '===============================

    ''' <summary>读取 server.properties（保留顺序）。</summary>
    Public Shared Function ReadPropertyLines() As List(Of String)
        Dim Result As New List(Of String)
        Try
            Dim File As String = Path.Combine(_Dir, "server.properties")
            If IO.File.Exists(File) Then Result.AddRange(IO.File.ReadAllLines(File, Text.Encoding.UTF8))
        Catch ex As Exception
            Logger.Warn(ex, "读取 server.properties 失败")
        End Try
        Return Result
    End Function

    Public Shared Function ReadProperties() As Dictionary(Of String, String)
        Dim Result As New Dictionary(Of String, String)(StringComparer.OrdinalIgnoreCase)
        For Each RawLine As String In ReadPropertyLines()
            Dim Line As String = RawLine.Trim()
            If Line = "" OrElse Line.StartsWith("#") Then Continue For
            Dim Index As Integer = Line.IndexOf("="c)
            If Index <= 0 Then Continue For
            Result(Line.Substring(0, Index).Trim()) = Line.Substring(Index + 1).Trim()
        Next
        Return Result
    End Function

    ''' <summary>写回 server.properties（保留原有顺序，新增项追加到末尾）。</summary>
    Public Shared Sub WriteProperties(Values As Dictionary(Of String, String))
        Try
            Dim File As String = Path.Combine(_Dir, "server.properties")
            Dim Lines As New List(Of String)
            Dim Done As New HashSet(Of String)(StringComparer.OrdinalIgnoreCase)
            For Each RawLine As String In ReadPropertyLines()
                Dim Line As String = RawLine.Trim()
                Dim Index As Integer = Line.IndexOf("="c)
                If Line <> "" AndAlso Not Line.StartsWith("#") AndAlso Index > 0 Then
                    Dim Key As String = Line.Substring(0, Index).Trim()
                    If Values.ContainsKey(Key) Then
                        Lines.Add(Key & "=" & Values(Key))
                        Done.Add(Key)
                    Else
                        Lines.Add(RawLine)
                    End If
                Else
                    Lines.Add(RawLine)
                End If
            Next
            For Each Pair As KeyValuePair(Of String, String) In Values
                If Not Done.Contains(Pair.Key) Then Lines.Add(Pair.Key & "=" & Pair.Value)
            Next
            IO.File.WriteAllLines(File, Lines, Text.Encoding.UTF8)
        Catch ex As Exception
            Logger.Warn(ex, "保存 server.properties 失败")
            Throw
        End Try
    End Sub

    '===============================
    '  备份
    '===============================

    Public Shared ReadOnly Property BackupDir As String
        Get
            Return If(_Dir = "", "", Path.Combine(_Dir, "xy-backups"))
        End Get
    End Property

    ''' <summary>列出备份文件。</summary>
    Public Shared Function ListBackups() As List(Of FileInfo)
        Dim Result As New List(Of FileInfo)
        Try
            If _Dir = "" OrElse Not Directory.Exists(BackupDir) Then Return Result
            Result.AddRange(New DirectoryInfo(BackupDir).GetFiles("*.zip").OrderByDescending(Function(F) F.CreationTime))
        Catch ex As Exception
            Logger.Warn(ex, "读取备份列表失败")
        End Try
        Return Result
    End Function

    ''' <summary>创建备份（zip，排除备份目录本身与 logs/cache）。返回文件路径。</summary>
    Public Shared Function CreateBackup(Progress As Action(Of String)) As String
        If _Dir = "" OrElse Not Directory.Exists(_Dir) Then Throw New Exception("请先选择服务端文件夹。")
        Directory.CreateDirectory(BackupDir)
        Dim Target As String = Path.Combine(BackupDir, Date.Now.ToString("yyyyMMdd-HHmmss") & ".zip")
        If Progress IsNot Nothing Then Progress("正在打包服务端文件……")
        Dim Skip As New HashSet(Of String)(StringComparer.OrdinalIgnoreCase) From {"xy-backups", "logs", "cache", "crash-reports"}
        Using Archive As System.IO.Compression.ZipArchive = System.IO.Compression.ZipFile.Open(Target, System.IO.Compression.ZipArchiveMode.Create)
            Dim Files As String() = Directory.GetFiles(_Dir, "*", SearchOption.AllDirectories)
            Dim Done As Integer = 0
            For Each File As String In Files
                Dim Relative As String = File.Substring(_Dir.Length).TrimStart("\"c).Replace("\"c, "/"c)
                Dim Top As String = Relative.Split("/"c)(0)
                If Skip.Contains(Top) Then Continue For
                Try
                    System.IO.Compression.ZipFileExtensions.CreateEntryFromFile(Archive, File, Relative, System.IO.Compression.CompressionLevel.Optimal)
                Catch
                End Try
                Done += 1
                If Progress IsNot Nothing AndAlso Done Mod 50 = 0 Then Progress($"正在打包…… {Done}/{Files.Length}")
            Next
        End Using
        If Progress IsNot Nothing Then Progress("备份完成：" & Path.GetFileName(Target))
        Return Target
    End Function

    ''' <summary>删除备份。</summary>
    Public Shared Sub DeleteBackup(File As String)
        Try
            If IO.File.Exists(File) Then IO.File.Delete(File)
        Catch ex As Exception
            Logger.Warn(ex, "删除备份失败")
            Throw
        End Try
    End Sub

    ''' <summary>把备份解压回服务端目录（覆盖同名文件）。</summary>
    Public Shared Sub RestoreBackup(File As String, Progress As Action(Of String))
        If _Dir = "" Then Throw New Exception("请先选择服务端文件夹。")
        If Not IO.File.Exists(File) Then Throw New Exception("备份文件不存在。")
        If Progress IsNot Nothing Then Progress("正在还原备份……")
        Using Archive As System.IO.Compression.ZipArchive = System.IO.Compression.ZipFile.OpenRead(File)
            For Each Entry As System.IO.Compression.ZipArchiveEntry In Archive.Entries
                Try
                    Dim Dest As String = Path.Combine(_Dir, Entry.FullName.Replace("/"c, "\"c))
                    Directory.CreateDirectory(Path.GetDirectoryName(Dest))
                    If Entry.Name <> "" Then Entry.ExtractToFile(Dest, True)
                Catch ex As Exception
                    Logger.Warn(ex, "还原备份文件失败：" & Entry.FullName)
                End Try
            Next
        End Using
        If Progress IsNot Nothing Then Progress("还原完成。")
    End Sub

    '===============================
    '  插件 / 模组
    '===============================

    ''' <summary>返回插件或模组目录（不存在返回空）。</summary>
    Public Shared Function PluginDir(IsMod As Boolean) As String
        If _Dir = "" Then Return ""
        Dim Sub_ As String = If(IsMod, "mods", "plugins")
        Dim Full As String = Path.Combine(_Dir, Sub_)
        Return If(Directory.Exists(Full), Full, "")
    End Function

    ''' <summary>列出插件/模组文件（含 .disabled 状态）。</summary>
    Public Shared Function ListPlugins(IsMod As Boolean) As List(Of FileInfo)
        Dim Result As New List(Of FileInfo)
        Dim Base As String = If(IsMod, "mods", "plugins")
        Try
            If _Dir = "" Then Return Result
            Dim Full As String = Path.Combine(_Dir, Base)
            If Not Directory.Exists(Full) Then Return Result
            Result.AddRange(New DirectoryInfo(Full).GetFiles("*.jar*").OrderBy(Function(F) F.Name))
        Catch ex As Exception
            Logger.Warn(ex, "读取插件列表失败")
        End Try
        Return Result
    End Function

    Public Shared Function IsPluginEnabled(File As FileInfo) As Boolean
        Return Not File.Name.ToLowerInvariant().EndsWith(".disabled")
    End Function

    ''' <summary>启用/禁用（改后缀）。</summary>
    Public Shared Sub SetPluginEnabled(File As FileInfo, Enabled As Boolean)
        Try
            If Enabled Then
                If File.Name.EndsWith(".disabled", StringComparison.OrdinalIgnoreCase) Then
                    IO.File.Move(File.FullName, Path.ChangeExtension(File.FullName, Nothing))
                End If
            Else
                If Not File.Name.EndsWith(".disabled", StringComparison.OrdinalIgnoreCase) Then
                    IO.File.Move(File.FullName, File.FullName & ".disabled")
                End If
            End If
        Catch ex As Exception
            Logger.Warn(ex, "修改插件启用状态失败")
            Throw
        End Try
    End Sub

    '===============================
    '  玩家
    '===============================

    Public Enum PlayerListType
        Whitelist
        Ops
        Banned
    End Enum

    Private Shared Function PlayerListFile(Type As PlayerListType) As String
        Select Case Type
            Case PlayerListType.Whitelist : Return Path.Combine(_Dir, "whitelist.json")
            Case PlayerListType.Ops : Return Path.Combine(_Dir, "ops.json")
            Case Else : Return Path.Combine(_Dir, "banned-players.json")
        End Select
    End Function

    ''' <summary>读取玩家名单（whitelist/ops/banned），返回显示名列表。</summary>
    Public Shared Function ReadPlayerList(Type As PlayerListType) As List(Of String)
        Dim Result As New List(Of String)
        Try
            Dim File As String = PlayerListFile(Type)
            If Not IO.File.Exists(File) Then Return Result
            Dim Json As Newtonsoft.Json.Linq.JArray = Newtonsoft.Json.Linq.JArray.Parse(IO.File.ReadAllText(File))
            For Each Item As Newtonsoft.Json.Linq.JToken In Json
                Dim PlayerName As String = Item("name")?.ToString()
                If Not String.IsNullOrEmpty(PlayerName) Then Result.Add(PlayerName)
            Next
        Catch ex As Exception
            Logger.Warn(ex, "读取玩家名单失败")
        End Try
        Return Result
    End Function

    ''' <summary>通过控制台指令增删白名单/OP/封禁。</summary>
    Public Shared Sub PlayerCommand(Type As PlayerListType, PlayerName As String, Add As Boolean)
        Dim Cmd As String
        Select Case Type
            Case PlayerListType.Whitelist : Cmd = If(Add, "whitelist add ", "whitelist remove ")
            Case PlayerListType.Ops : Cmd = If(Add, "op ", "deop ")
            Case Else : Cmd = If(Add, "ban ", "pardon ")
        End Select
        SendCommand(Cmd & PlayerName)
    End Sub

    '===============================
    '  文件
    '===============================

    ''' <summary>列出目录项（目录在前）。</summary>
    Public Shared Function ListFiles(Relative As String) As List(Of FileSystemInfo)
        Dim Result As New List(Of FileSystemInfo)
        Try
            Dim Full As String = Path.Combine(_Dir, If(Relative, ""))
            If Not Directory.Exists(Full) Then Return Result
            Dim Info As New DirectoryInfo(Full)
            Result.AddRange(Info.GetDirectories().OrderBy(Function(D) D.Name).Cast(Of FileSystemInfo)())
            Result.AddRange(Info.GetFiles().OrderBy(Function(F) F.Name).Cast(Of FileSystemInfo)())
        Catch ex As Exception
            Logger.Warn(ex, "列出文件失败")
        End Try
        Return Result
    End Function

    ''' <summary>读取文本文件（超过 1MB 拒绝）。</summary>
    Public Shared Function ReadTextFile(Relative As String) As String
        Dim Full As String = Path.Combine(_Dir, Relative)
        Dim Info As New FileInfo(Full)
        If Not Info.Exists Then Throw New Exception("文件不存在。")
        If Info.Length > 1024 * 1024 Then Throw New Exception("文件超过 1 MB，请用记事本打开。")
        Return IO.File.ReadAllText(Full, Text.Encoding.UTF8)
    End Function

    Public Shared Sub WriteTextFile(Relative As String, Content As String)
        IO.File.WriteAllText(Path.Combine(_Dir, Relative), Content, Text.Encoding.UTF8)
    End Sub

    Public Shared Sub DeleteEntry(Relative As String)
        Dim Full As String = Path.Combine(_Dir, Relative)
        If Directory.Exists(Full) Then
            Directory.Delete(Full, True)
        ElseIf IO.File.Exists(Full) Then
            IO.File.Delete(Full)
        End If
    End Sub

    Public Shared Sub CreateFolder(Relative As String)
        Directory.CreateDirectory(Path.Combine(_Dir, Relative))
    End Sub

    '===============================
    '  玩家名单（白名单 / OP / 封禁）
    '===============================

    ''' <summary>添加或移除名单成员：服务器运行时走控制台，否则直接改 json。</summary>
    Public Shared Sub PlayerListSet(Type As PlayerListType, PlayerName As String, Add As Boolean)
        If String.IsNullOrEmpty(PlayerName) Then Return
        If IsRunning() Then
            PlayerCommand(Type, PlayerName, Add)
            Return
        End If
        Dim File As String = PlayerListFile(Type)
        Dim List As Newtonsoft.Json.Linq.JArray
        Try
            If IO.File.Exists(File) Then
                List = Newtonsoft.Json.Linq.JArray.Parse(IO.File.ReadAllText(File, Text.Encoding.UTF8))
            Else
                List = New Newtonsoft.Json.Linq.JArray()
            End If
        Catch
            List = New Newtonsoft.Json.Linq.JArray()
        End Try

        Dim Exist As Newtonsoft.Json.Linq.JObject = Nothing
        For Each Item As Newtonsoft.Json.Linq.JToken In List
            If String.Equals(Item("name")?.ToString(), PlayerName, StringComparison.OrdinalIgnoreCase) Then
                Exist = TryCast(Item, Newtonsoft.Json.Linq.JObject)
                Exit For
            End If
        Next

        If Add Then
            If Exist Is Nothing Then
                Dim Item As New Newtonsoft.Json.Linq.JObject()
                Item("uuid") = PlayerOfflineUuid(PlayerName)
                Item("name") = PlayerName
                If Type = PlayerListType.Ops Then
                    Item("level") = 4
                    Item("bypassesPlayerLimit") = False
                ElseIf Type = PlayerListType.Banned Then
                    Item("created") = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss +0800")
                    Item("source") = "XingYue Launcher"
                    Item("expires") = "forever"
                    Item("reason") = "Banned by an operator."
                End If
                List.Add(Item)
            End If
        Else
            If Exist IsNot Nothing Then Exist.Remove()
        End If

        IO.Directory.CreateDirectory(_Dir)
        IO.File.WriteAllText(File, List.ToString(Newtonsoft.Json.Formatting.Indented), Text.Encoding.UTF8)
    End Sub

    ''' <summary>由玩家名计算离线 UUID（与 Minecraft 的 OfflinePlayer 算法一致）。</summary>
    Private Shared Function PlayerOfflineUuid(PlayerName As String) As String
        Using Md5 As Security.Cryptography.MD5 = Security.Cryptography.MD5.Create()
            Dim Hash As Byte() = Md5.ComputeHash(Text.Encoding.UTF8.GetBytes("OfflinePlayer:" & PlayerName))
            Hash(6) = (Hash(6) And &H0F) Or &H30
            Hash(8) = (Hash(8) And &H3F) Or &H80
            Dim Hex As String = BitConverter.ToString(Hash).Replace("-", "").ToLowerInvariant()
            Return Hex.Substring(0, 8) & "-" & Hex.Substring(8, 4) & "-" & Hex.Substring(12, 4) & "-" & Hex.Substring(16, 4) & "-" & Hex.Substring(20, 12)
        End Using
    End Function

    ''' <summary>切换服务器属性里的 white-list / enable-command-block 等布尔项。</summary>
    Public Shared Sub SetProperty(Key As String, Value As String)
        Dim Props As Dictionary(Of String, String) = ReadProperties()
        Props(Key) = Value
        WriteProperties(Props)
    End Sub

    '===============================
    '  定时任务
    '===============================

    Public Class ScheduledTask
        Public Property Time As String = "04:00"
        Public Property Action As String = "backup"
        Public Property Command As String = ""
        Public Property Enabled As Boolean = True
        Public Property LastRun As String = ""
    End Class

    Private Shared _Tasks As List(Of ScheduledTask)

    Public Shared Property Tasks As List(Of ScheduledTask)
        Get
            If _Tasks Is Nothing Then LoadTasks()
            Return _Tasks
        End Get
        Set(value As List(Of ScheduledTask))
            _Tasks = value
        End Set
    End Property

    Private Shared ReadOnly Property TaskFile As String
        Get
            Return Path.Combine(Paths.Base, "XingYue\Server\tasks.json")
        End Get
    End Property

    Public Shared Sub LoadTasks()
        _Tasks = New List(Of ScheduledTask)
        Try
            If IO.File.Exists(TaskFile) Then
                Dim Arr As Newtonsoft.Json.Linq.JArray = Newtonsoft.Json.Linq.JArray.Parse(IO.File.ReadAllText(TaskFile, Text.Encoding.UTF8))
                For Each Item As Newtonsoft.Json.Linq.JToken In Arr
                    _Tasks.Add(New ScheduledTask With {
                        .Time = If(Item("time")?.ToString(), "04:00"),
                        .Action = If(Item("action")?.ToString(), "backup"),
                        .Command = If(Item("command")?.ToString(), ""),
                        .Enabled = If(Item("enabled") Is Nothing, True, Item("enabled").ToObject(Of Boolean)()),
                        .LastRun = If(Item("lastRun")?.ToString(), "")
                    })
                Next
            End If
        Catch ex As Exception
            Logger.Warn(ex, "读取定时任务失败")
        End Try
    End Sub

    Public Shared Sub SaveTasks()
        Try
            IO.Directory.CreateDirectory(Path.GetDirectoryName(TaskFile))
            Dim Arr As New Newtonsoft.Json.Linq.JArray()
            For Each Task As ScheduledTask In Tasks
                Dim Item As New Newtonsoft.Json.Linq.JObject()
                Item("time") = Task.Time
                Item("action") = Task.Action
                Item("command") = Task.Command
                Item("enabled") = Task.Enabled
                Item("lastRun") = Task.LastRun
                Arr.Add(Item)
            Next
            IO.File.WriteAllText(TaskFile, Arr.ToString(Newtonsoft.Json.Formatting.Indented), Text.Encoding.UTF8)
        Catch ex As Exception
            Logger.Warn(ex, "保存定时任务失败")
        End Try
    End Sub

    ''' <summary>检查并执行到点的定时任务（每分钟调用一次）。</summary>
    Public Shared Sub RunDueTasks()
        Dim NowText As String = DateTime.Now.ToString("HH:mm")
        Dim Today As String = DateTime.Now.ToString("yyyy-MM-dd")
        For Each Task As ScheduledTask In Tasks
            If Not Task.Enabled Then Continue For
            If Task.Time <> NowText Then Continue For
            If Task.LastRun = Today Then Continue For
            Task.LastRun = Today
            SaveTasks()
            Try
                Select Case Task.Action.ToLowerInvariant()
                    Case "backup"
                        CreateBackup(Nothing)
                        If OnLog IsNot Nothing Then OnLog.Invoke("（定时任务）已创建自动备份")
                    Case "restart"
                        If IsRunning() Then
                            StopServer()
                            Threading.Thread.Sleep(3000)
                            StartServer()
                        Else
                            StartServer()
                        End If
                    Case "stop"
                        If IsRunning() Then StopServer()
                    Case "start"
                        If Not IsRunning() Then StartServer()
                    Case "command"
                        SendCommand(Task.Command)
                End Select
            Catch ex As Exception
                Logger.Warn(ex, "执行定时任务失败")
            End Try
        Next
    End Sub

End Class

''' <summary>服务端还没同意 EULA。</summary>
Public Class ServerEulaException
    Inherits Exception
    Public Sub New()
        MyBase.New("启动服务器前需要同意 Minecraft 最终用户许可协议（EULA）。")
    End Sub
End Class
