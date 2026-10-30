Imports System.IO
Imports System.Net
Imports System.Net.Http
Imports System.Security.Cryptography
Imports System.Text
Imports Newtonsoft.Json.Linq

''' <summary>
''' 樱花映射（SakuraFrp）联机支持：访问密钥登录、隧道管理、frpc 进程控制。
''' 官方 API 文档：https://api.natfrp.com/docs/ ，接口根路径为 https://api.natfrp.com/v4 。
''' 所有网络方法都禁止在 UI 线程调用（内部会检查），请在后台线程使用。
''' 这里刻意使用 Class 而非 Module：Module 的成员会污染整个项目的全局命名空间，容易和局部变量重名。
''' </summary>
Public Class SakuraFrp

    '===============================
    '  常量与状态
    '===============================

    Public Const ApiBase As String = "https://api.natfrp.com/v4"
    ''' <summary>frpc 下载地址的兜底值（优先从 /system/clients 获取最新版本，这里只是网络异常时的备用）。</summary>
    Private Const FrpcFallbackUrl As String = "https://nya.globalslb.net/natfrp/client/frpc/0.51.0-sakura-15.1/frpc_windows_amd64.exe"

    Private Const KeyToken As String = "LinkSakuraToken"
    Private Const KeyUserName As String = "LinkSakuraUserName"
    Private Const KeyTunnelId As String = "LinkSakuraTunnelId"
    Private Const KeyLocalPort As String = "LinkSakuraLocalPort"
    Private Const KeyNodeId As String = "LinkSakuraNodeId"

    Private Shared ReadOnly Http As HttpClient = CreateHttpClient()
    Private Shared Function CreateHttpClient() As HttpClient
        '强制 TLS 1.2，避免部分系统默认协议过旧导致握手失败
        Try
            ServicePointManager.SecurityProtocol = ServicePointManager.SecurityProtocol Or SecurityProtocolType.Tls12
        Catch
        End Try
        Dim Client As New HttpClient() With {.Timeout = TimeSpan.FromSeconds(20)}
        Client.DefaultRequestHeaders.UserAgent.ParseAdd("XingYueLauncher/" & VersionCode.ToString())
        Return Client
    End Function

    '===============================
    '  本地配置
    '  不用 Settings 类：它要求设置项必须先在 Entries 里注册，否则读取会抛 KeyNotFoundException。
    '  这里用一份放在 XingYue\SakuraFrp\sakura.ini 的极简键值文件，随启动器目录走，删目录即清空。
    '===============================

    ''' <summary>配置文件路径。用属性而不是字段：字段初始化按源码顺序执行，FrpcDir 可能还没赋值。</summary>
    Private Shared ReadOnly Property ConfigFile As String
        Get
            Return FrpcDir & "sakura.ini"
        End Get
    End Property

    Private Shared Function ReadConfig() As Dictionary(Of String, String)
        Dim Result As New Dictionary(Of String, String)(StringComparer.OrdinalIgnoreCase)
        Try
            If File.Exists(ConfigFile) Then
                For Each Line As String In File.ReadAllLines(ConfigFile, Encoding.UTF8)
                    Dim Trimmed As String = Line.Trim()
                    If Trimmed = "" OrElse Trimmed.StartsWith("#") Then Continue For
                    Dim Index As Integer = Trimmed.IndexOf("="c)
                    If Index <= 0 Then Continue For
                    Result(Trimmed.Substring(0, Index).Trim()) = Trimmed.Substring(Index + 1).Trim()
                Next
            End If
        Catch ex As Exception
            Logger.Warn(ex, "樱花映射：读取本地配置失败")
        End Try
        Return Result
    End Function

    Private Shared Sub WriteConfig(Values As Dictionary(Of String, String))
        Try
            Directory.CreateDirectory(FrpcDir)
            Dim Lines As New List(Of String) From {"# 星玥启动器 - 樱花映射配置", "# 访问密钥等同于账号凭据，请勿分享此文件", ""}
            For Each Pair As KeyValuePair(Of String, String) In Values
                Lines.Add(Pair.Key & "=" & Pair.Value)
            Next
            File.WriteAllLines(ConfigFile, Lines, Encoding.UTF8)
        Catch ex As Exception
            Logger.Warn(ex, "樱花映射：保存本地配置失败")
        End Try
    End Sub

    Private Shared Function ReadValue(Key As String, DefaultValue As String) As String
        Dim Got As String = Nothing
        If ReadConfig().TryGetValue(Key, Got) Then Return Got
        Return DefaultValue
    End Function

    Private Shared Sub WriteValue(Key As String, Value As String)
        Dim Values As Dictionary(Of String, String) = ReadConfig()
        Values(Key) = If(Value, "")
        WriteConfig(Values)
    End Sub

    ''' <summary>
    ''' 已保存的访问密钥。为空表示未登录。
    ''' </summary>
    Public Shared ReadOnly Property Token As String
        Get
            Return If(ReadValue(KeyToken, ""), "").Trim
        End Get
    End Property

    ''' <summary>
    ''' 登录后保存的用户名，仅用于显示。
    ''' </summary>
    Public Shared ReadOnly Property UserName As String
        Get
            Return If(ReadValue(KeyUserName, ""), "").Trim
        End Get
    End Property

    ''' <summary>
    ''' 是否已登录（有访问密钥，且曾成功验证过）。
    ''' </summary>
    Public Shared ReadOnly Property IsLoggedIn As Boolean
        Get
            Return Token <> "" AndAlso UserName <> ""
        End Get
    End Property

    ''' <summary>
    ''' 上次使用的隧道 ID，0 表示没有。
    ''' </summary>
    Public Shared ReadOnly Property LastTunnelId As Integer
        Get
            Return CInt(ReadValue(KeyTunnelId, "0"))
        End Get
    End Property

    ''' <summary>
    ''' 上次使用的本地端口（Minecraft 默认为 25565）。
    ''' </summary>
    Public Shared ReadOnly Property LastLocalPort As Integer
        Get
            Dim Port As Integer = CInt(ReadValue(KeyLocalPort, "0"))
            Return If(Port > 0 AndAlso Port <= 65535, Port, 25565)
        End Get
    End Property

    ''' <summary>
    ''' 上次使用的节点 ID，0 表示没有。
    ''' </summary>
    Public Shared ReadOnly Property LastNodeId As Integer
        Get
            Return CInt(ReadValue(KeyNodeId, "0"))
        End Get
    End Property

    Public Shared Sub SaveLogin(NewToken As String, NewUserName As String)
        WriteValue(KeyToken, NewToken.Trim)
        WriteValue(KeyUserName, NewUserName)
        Logger.Info($"樱花映射：已登录账号 {NewUserName}")
    End Sub

    Public Shared Sub SaveTunnelSettings(TunnelId As Integer, NodeId As Integer, LocalPort As Integer)
        If TunnelId > 0 Then WriteValue(KeyTunnelId, TunnelId.ToString())
        If NodeId > 0 Then WriteValue(KeyNodeId, NodeId.ToString())
        If LocalPort > 0 Then WriteValue(KeyLocalPort, LocalPort.ToString())
    End Sub

    Public Shared Sub Logout()
        StopFrpc()
        WriteValue(KeyToken, "")
        WriteValue(KeyUserName, "")
        Logger.Info("樱花映射：已退出登录")
    End Sub

    '===============================
    '  API
    '===============================

    ''' <summary>
    ''' 调用 API 并返回原始响应文本。非 2xx 时抛出带服务端 msg 的异常。
    ''' </summary>
    Private Shared Function ApiRequest(Method As HttpMethod, Path As String, Optional Form As Dictionary(Of String, String) = Nothing) As String
        If RunInUi() Then Throw New Exception("不能在 UI 线程调用樱花映射 API")
        Dim Url As String = ApiBase & Path
        Using Request As New HttpRequestMessage(Method, Url)
            Request.Headers.TryAddWithoutValidation("Authorization", "Bearer " & Token)
            Request.Headers.TryAddWithoutValidation("Accept", "application/json")
            If Form IsNot Nothing Then
                Request.Content = New FormUrlEncodedContent(Form)
            End If
            Try
                Using Response = Http.SendAsync(Request).GetAwaiter().GetResult()
                    Dim Body As String = Response.Content.ReadAsStringAsync().GetAwaiter().GetResult()
                    If Not Response.IsSuccessStatusCode Then
                        Throw New SakuraFrpException(ExtractErrorMessage(Body, CInt(Response.StatusCode)), CInt(Response.StatusCode))
                    End If
                    Return Body
                End Using
            Catch ex As SakuraFrpException
                Throw
            Catch ex As Exception
                ex.ThrowIfCanceled()
                Logger.Warn(ex, $"樱花映射 API 请求失败（{Method.Method} {Path}）")
                Throw New SakuraFrpException("无法连接樱花映射服务器，请检查网络连接后重试。", 0, ex)
            End Try
        End Using
    End Function

    ''' <summary>
    ''' 从 API 的错误返回中提取人类可读的消息。
    ''' </summary>
    Private Shared Function ExtractErrorMessage(Body As String, StatusCode As Integer) As String
        If Not String.IsNullOrWhiteSpace(Body) Then
            Try
                Dim Json As JObject = JObject.Parse(Body)
                Dim MsgToken As JToken = Json("msg")
                If MsgToken Is Nothing Then MsgToken = Json("message")
                Dim Msg As String = If(MsgToken Is Nothing, "", MsgToken.ToString())
                If Not String.IsNullOrWhiteSpace(Msg) Then Return Msg
            Catch
            End Try
        End If
        Select Case StatusCode
            Case 401, 403
                Return "访问密钥无效或已失效，请到 设置 → 联机 中重新登录。"
            Case 404
                Return "接口不存在（API 可能已更新）。"
            Case 429
                Return "请求过于频繁，请稍后再试。"
            Case Else
                Return $"请求失败（HTTP {StatusCode}）。"
        End Select
    End Function

    ''' <summary>
    ''' 用访问密钥验证登录，成功则保存凭据并返回用户信息。
    ''' </summary>
    Public Shared Function Login(NewToken As String) As JObject
        Dim OldToken As String = Token
        WriteValue(KeyToken, NewToken.Trim)
        Try
            Dim Info As JObject = GetUserInfo()
            Dim Name As String = If(Info("name") Is Nothing, "用户", Info("name").ToString)
            SaveLogin(NewToken, Name)
            Return Info
        Catch
            WriteValue(KeyToken, If(OldToken, ""))
            Throw
        End Try
    End Function

    ''' <summary>
    ''' 获取用户基本信息（可用来验证访问密钥是否有效）。
    ''' </summary>
    Public Shared Function GetUserInfo() As JObject
        Return JObject.Parse(ApiRequest(HttpMethod.Get, "/user/info"))
    End Function

    ''' <summary>
    ''' 获取节点列表，返回 节点ID(字符串) → 节点信息 的对象。
    ''' </summary>
    Public Shared Function GetNodes() As JObject
        Return JObject.Parse(ApiRequest(HttpMethod.Get, "/nodes"))
    End Function

    Public Shared Function GetTunnels() As JArray
        Return JArray.Parse(ApiRequest(HttpMethod.Get, "/tunnels"))
    End Function

    ''' <summary>
    ''' 创建一条 TCP 隧道。
    ''' </summary>
    Public Shared Function CreateTcpTunnel(Name As String, NodeId As Integer, LocalPort As Integer, Optional LocalIp As String = "127.0.0.1") As JObject
        Dim Form As New Dictionary(Of String, String) From {
            {"name", Name},
            {"type", "tcp"},
            {"node", NodeId.ToString()},
            {"local_ip", LocalIp},
            {"local_port", LocalPort.ToString()}
        }
        Dim Result As JObject = JObject.Parse(ApiRequest(HttpMethod.Post, "/tunnels", Form))
        '创建接口只返回 id/name/remote，补齐本地需要的字段
        If Result("node") Is Nothing Then Result("node") = NodeId
        If Result("local_port") Is Nothing Then Result("local_port") = LocalPort
        If Result("local_ip") Is Nothing Then Result("local_ip") = LocalIp
        Return Result
    End Function

    Public Shared Sub DeleteTunnel(TunnelId As Integer)
        ApiRequest(HttpMethod.Post, "/tunnel/delete", New Dictionary(Of String, String) From {{"ids", TunnelId.ToString()}})
    End Sub

    ''' <summary>
    ''' 计算隧道的公网访问地址（host:port）。节点信息缺失时返回端口。
    ''' </summary>
    Public Shared Function GetTunnelAddress(Tunnel As JObject, Nodes As JObject) As String
        If Tunnel Is Nothing Then Return ""
        Dim RemoteToken As JToken = Tunnel("remote")
        Dim Remote As String = If(RemoteToken Is Nothing, "", RemoteToken.ToString)
        If Remote = "" Then Return ""
        Dim Host As String = ""
        Dim NodeToken As JToken = Tunnel("node")
        '注意：创建隧道的接口只返回 id/name/remote，没有 node 字段，这里必须判空
        If Nodes IsNot Nothing AndAlso NodeToken IsNot Nothing Then
            Dim Node As JObject = TryCast(Nodes(NodeToken.ToString), JObject)
            If Node IsNot Nothing Then
                Dim HostToken As JToken = Node("host")
                If HostToken IsNot Nothing Then Host = HostToken.ToString
            End If
        End If
        If Host = "" Then Return Remote
        Return Host & ":" & Remote
    End Function

    ''' <summary>
    ''' 判断节点是否可用作普通（非公开游戏）TCP 穿透。
    ''' flag 位含义见官方 API 文档 /nodes：1&lt;&lt;2 可创建隧道，1&lt;&lt;9 离线。
    ''' </summary>
    Public Shared Function IsNodeUsable(Node As JObject) As Boolean
        If Node Is Nothing Then Return False
        Dim Flag As Long = 0
        Try
            Flag = Node("flag").ToObject(Of Long)()
        Catch
            Return True '解析不出来就别拦着用户
        End Try
        If (Flag And (1L << 2)) = 0 Then Return False '满载或不允许创建隧道
        If (Flag And (1L << 9)) <> 0 Then Return False '离线
        Return True
    End Function

    '===============================
    '  frpc 进程
    '===============================

    ''' <summary>frpc 所在目录，以 \ 结尾。放在启动器目录下，跟着启动器一起删。</summary>
    Public Shared ReadOnly Property FrpcDir As String = Paths.Base & "XingYue\SakuraFrp\"
    Public Shared ReadOnly Property FrpcPath As String = FrpcDir & "frpc.exe"

    '每条隧道一个 frpc 进程，这样可以同时映射多条隧道（不同本地端口），互不影响
    Private Shared TunnelProcesses As New Dictionary(Of Integer, Process)

    ''' <summary>
    ''' 指定隧道的 frpc 是否正在运行。
    ''' </summary>
    Public Shared Function IsFrpcRunning(TunnelId As Integer) As Boolean
        Dim Proc As Process = Nothing
        If Not TunnelProcesses.TryGetValue(TunnelId, Proc) OrElse Proc Is Nothing Then Return False
        Try
            Return Not Proc.HasExited
        Catch
            Return False
        End Try
    End Function

    ''' <summary>
    ''' 是否有任意一条隧道的 frpc 在运行。
    ''' </summary>
    Public Shared Function IsFrpcRunning() As Boolean
        For Each TunnelId As Integer In TunnelProcesses.Keys.ToList()
            If IsFrpcRunning(TunnelId) Then Return True
        Next
        Return False
    End Function

    ''' <summary>
    ''' 正在运行的隧道 ID 列表。
    ''' </summary>
    Public Shared Function RunningTunnelIds() As List(Of Integer)
        Dim Result As New List(Of Integer)
        For Each TunnelId As Integer In TunnelProcesses.Keys.ToList()
            If IsFrpcRunning(TunnelId) Then Result.Add(TunnelId)
        Next
        Return Result
    End Function

    ''' <summary>
    ''' 测试节点延迟：对节点的 frp 服务端口做一次 TCP 连接计时。返回毫秒数，超时或失败返回 -1。
    ''' </summary>
    Public Shared Function PingNode(Host As String, Optional Port As Integer = 7000, Optional TimeoutMs As Integer = 2000) As Integer
        If String.IsNullOrWhiteSpace(Host) Then Return -1
        Dim Client As New Net.Sockets.TcpClient()
        Try
            Dim Watch As New Diagnostics.Stopwatch()
            Watch.Start()
            Dim ConnectTask As Task = Client.ConnectAsync(Host, Port)
            If Not ConnectTask.Wait(TimeoutMs) Then Return -1
            Watch.Stop()
            Return If(Client.Connected, CInt(Watch.ElapsedMilliseconds), -1)
        Catch
            Return -1
        Finally
            Try
                Client.Close()
            Catch
            End Try
        End Try
    End Function

    ''' <summary>
    ''' 确保 frpc.exe 已就绪，缺失时从官方源下载（约 14 MB）。返回 frpc 路径。
    ''' </summary>
    ''' <param name="Progress">进度回调（文本）。</param>
    Public Shared Function EnsureFrpc(Optional Progress As Action(Of String) = Nothing) As String
        If FileUtils.Exists(FrpcPath) Then Return FrpcPath
        If RunInUi() Then Throw New Exception("不能在 UI 线程下载樱花映射客户端")
        Directory.CreateDirectory(FrpcDir)
        '从官方接口获取当前版本的下载地址与哈希
        Dim Url As String = FrpcFallbackUrl, Hash As String = ""
        Try
            If Progress IsNot Nothing Then Progress.Invoke("正在获取客户端下载信息……")
            Dim ClientsJson As String
            Using InfoClient As New HttpClient()
                InfoClient.Timeout = TimeSpan.FromSeconds(20)
                ClientsJson = InfoClient.GetStringAsync(ApiBase & "/system/clients?download=true").GetAwaiter().GetResult()
            End Using
            Dim Clients As JObject = JObject.Parse(ClientsJson)
            Dim FrpcPart As JObject = TryCast(Clients("frpc"), JObject)
            Dim Archs As JObject = If(FrpcPart Is Nothing, Nothing, TryCast(FrpcPart("archs"), JObject))
            Dim Arch As JObject = If(Archs Is Nothing, Nothing, TryCast(Archs("windows_amd64"), JObject))
            If Arch IsNot Nothing AndAlso Arch("url") IsNot Nothing Then
                Url = Arch("url").ToString
                Hash = If(Arch("hash") Is Nothing, "", Arch("hash").ToString.ToLowerInvariant())
            End If
        Catch ex As Exception
            Logger.Warn(ex, "获取樱花映射客户端下载信息失败，使用内置备用地址")
        End Try
        '下载到临时文件，校验后再改名，避免半截文件被当成可用
        Dim TempPath As String = FrpcPath & ".download"
        FileUtils.Delete(TempPath)
        If Progress IsNot Nothing Then Progress.Invoke("正在下载樱花映射客户端（约 14 MB）……")
        Try
            Using Client As New HttpClient() With {.Timeout = TimeSpan.FromMinutes(5)}
                Using Response = Client.GetAsync(Url, HttpCompletionOption.ResponseHeadersRead).GetAwaiter().GetResult()
                    Response.EnsureSuccessStatusCode()
                    Using Source = Response.Content.ReadAsStreamAsync().GetAwaiter().GetResult()
                        Using Target As New FileStream(TempPath, FileMode.Create, FileAccess.Write, FileShare.None)
                            Source.CopyTo(Target)
                        End Using
                    End Using
                End Using
            End Using
        Catch ex As Exception
            FileUtils.Delete(TempPath)
            Throw New SakuraFrpException("下载樱花映射客户端失败：" & ex.GetBaseException.Message, 0, ex)
        End Try
        '校验 MD5
        If Hash <> "" Then
            If Progress IsNot Nothing Then Progress.Invoke("正在校验客户端……")
            Dim Actual As String = GetMd5(TempPath)
            If Not String.Equals(Actual, Hash, StringComparison.OrdinalIgnoreCase) Then
                FileUtils.Delete(TempPath)
                Throw New SakuraFrpException($"樱花映射客户端校验失败（期望 {Hash}，实际 {Actual}），请重试。")
            End If
        End If
        File.Move(TempPath, FrpcPath)
        If Progress IsNot Nothing Then Progress.Invoke("客户端已就绪")
        Logger.Info($"樱花映射客户端已下载：{FrpcPath}")
        Return FrpcPath
    End Function

    Private Shared Function GetMd5(Path As String) As String
        Using Stream As New FileStream(Path, FileMode.Open, FileAccess.Read, FileShare.Read)
            Using Md5 As MD5 = MD5.Create()
                Return BitConverter.ToString(Md5.ComputeHash(Stream)).Replace("-", "").ToLowerInvariant()
            End Using
        End Using
    End Function

    ''' <summary>
    ''' 启动指定隧道的 frpc，把它映射到本机。可以同时启动多条隧道（各自独立进程）。
    ''' </summary>
    Public Shared Sub StartFrpc(TunnelId As Integer)
        If Token = "" Then Throw New SakuraFrpException("尚未登录樱花映射。")
        If IsFrpcRunning(TunnelId) Then Return
        StopFrpc(TunnelId)
        EnsureFrpc()
        Dim Psi As New ProcessStartInfo(FrpcPath, $"-f {Token}:{TunnelId}") With {
            .UseShellExecute = False,
            .CreateNoWindow = True,
            .RedirectStandardOutput = True,
            .RedirectStandardError = True,
            .WorkingDirectory = FrpcDir
        }
        Dim Proc As New Process With {.StartInfo = Psi}
        AddHandler Proc.OutputDataReceived,
            Sub(Sender As Object, E As DataReceivedEventArgs)
                If E.Data IsNot Nothing Then Logger.Info($"[樱花映射 {TunnelId}] {E.Data}")
            End Sub
        AddHandler Proc.ErrorDataReceived,
            Sub(Sender As Object, E As DataReceivedEventArgs)
                If E.Data IsNot Nothing Then Logger.Warn($"[樱花映射 {TunnelId}] {E.Data}")
            End Sub
        Proc.Start()
        Proc.BeginOutputReadLine()
        Proc.BeginErrorReadLine()
        TunnelProcesses(TunnelId) = Proc
        RegisterExitCleanup()
        Logger.Info($"樱花映射：已启动 frpc，隧道 {TunnelId}")
    End Sub

    ''' <summary>
    ''' frpc 是子进程，启动器被关闭时不会自己退出，这里挂个退出钩子把它一起结束掉。
    ''' </summary>
    Private Shared ExitCleanupRegistered As Boolean = False
    Private Shared Sub RegisterExitCleanup()
        If ExitCleanupRegistered Then Return
        ExitCleanupRegistered = True
        Try
            AddHandler AppDomain.CurrentDomain.ProcessExit,
                Sub(Sender As Object, E As EventArgs)
                    Try
                        StopFrpc()
                    Catch
                    End Try
                End Sub
        Catch ex As Exception
            Logger.Warn(ex, "注册樱花映射退出清理失败")
        End Try
    End Sub

    ''' <summary>
    ''' 停止指定隧道的 frpc。
    ''' </summary>
    Public Shared Sub StopFrpc(TunnelId As Integer)
        Dim Proc As Process = Nothing
        If Not TunnelProcesses.TryGetValue(TunnelId, Proc) Then Return
        TunnelProcesses.Remove(TunnelId)
        If Proc Is Nothing Then Return
        Try
            If Not Proc.HasExited Then
                Proc.Kill()
                Proc.WaitForExit(3000)
            End If
        Catch ex As Exception
            Logger.Warn(ex, "停止樱花映射客户端时出错")
        Finally
            Try
                Proc.Dispose()
            Catch
            End Try
        End Try
        Logger.Info($"樱花映射：已停止 frpc，隧道 {TunnelId}")
    End Sub

    ''' <summary>
    ''' 停止所有隧道的 frpc（退出程序、退出登录、删除隧道时调用）。
    ''' </summary>
    Public Shared Sub StopFrpc()
        For Each TunnelId As Integer In TunnelProcesses.Keys.ToList()
            StopFrpc(TunnelId)
        Next
    End Sub

End Class

''' <summary>
''' 樱花映射操作失败（带服务端消息）。
''' </summary>
Public Class SakuraFrpException
    Inherits Exception
    Public ReadOnly Property StatusCode As Integer
    Public Sub New(Message As String, Optional StatusCode As Integer = 0, Optional InnerException As Exception = Nothing)
        MyBase.New(Message, InnerException)
        Me.StatusCode = StatusCode
    End Sub
End Class
