Imports System.Net
Imports System.Net.Sockets
Imports System.Threading

Public Class PageSetupLink

    Private Sub PageSetupLink_Loaded(sender As Object, e As RoutedEventArgs) Handles Me.Loaded

        '重复加载部分
        PanBack.ScrollToHome()
        If LocateSakuraOnLoad Then
            LocateSakuraOnLoad = False
            AniStart({AaCode(Sub() LocateSakura(), 120)}, "Locate Sakura") '等布局完成后再定位
        End If

        '非重复加载部分
        Static IsLoaded As Boolean = False
        If IsLoaded Then Return
        IsLoaded = True

        AniControlEnabled += 1
        Reload()
        AniControlEnabled -= 1

    End Sub
    Public Sub Reload()
        SettingService.RefreshSettings(Me)
        RefreshSakuraLogin()
    End Sub

    '初始化
    Public Sub Reset()
        Try
            SettingService.ResetSettings(Me)
            Logger.Info("已初始化联机页设置")
            Hint("已初始化联机页设置！", HintType.Green, False)
        Catch ex As Exception
            Logger.Error(ex, "初始化联机页设置失败", LogBehavior.Alert)
        End Try
        Reload()
    End Sub

#Region "自用服务器节点"

    ''' <summary>
    ''' 自用服务器节点地址。点击「自用服务器节点」按钮即可把它填进自定义节点。
    ''' </summary>
    Public Const SelfPeer As String = "tcp://xywm.dpdns.org:11010"

    Private Sub BtnAddSelfPeer_Click(sender As Object, e As RoutedEventArgs) Handles BtnAddSelfPeer.Click
        Dim OldPeers As String = If(Settings.Get(Of String)("LinkCustomPeer"), "").Trim
        Dim Peers As New List(Of String)
        If OldPeers <> "" Then
            For Each Item In OldPeers.Split(New Char() {","c, ";"c, " "c, vbCrLf, vbLf, vbTab}, StringSplitOptions.RemoveEmptyEntries)
                If Item.Trim <> "" Then Peers.Add(Item.Trim)
            Next
        End If
        If Peers.Exists(Function(P) String.Equals(P, SelfPeer, StringComparison.OrdinalIgnoreCase)) Then
            Hint("自定义节点里已经有自用服务器节点了", HintType.Blue, False)
            Return
        End If
        Peers.Add(SelfPeer)
        Dim NewPeers As String = String.Join(",", Peers)
        Settings.Set("LinkCustomPeer", NewPeers)
        TextCustomPeer.Text = NewPeers
        Logger.Info($"已添加自用服务器节点：{SelfPeer}，当前自定义节点：{NewPeers}")
        Hint($"已添加自用服务器节点：{SelfPeer}", HintType.Green, False)
    End Sub

#End Region

#Region "测试自定义节点连接状态"

    '测试正在进行中（防止重复点击）
    Private IsCheckingPeer As Boolean

    Private Sub BtnCheckPeer_Click(sender As Object, e As RoutedEventArgs) Handles BtnCheckPeer.Click
        If IsCheckingPeer Then Return
        Dim RawPeers As String = If(Settings.Get(Of String)("LinkCustomPeer"), "").Trim
        If String.IsNullOrWhiteSpace(RawPeers) Then
            Hint("请先在「自定义节点」里填写节点地址，例如 tcp://1.2.3.4:11010", HintType.Red, False)
            Return
        End If
        Dim Peers As String() = RawPeers.Split(New Char() {","c, ";"c, " "c, vbCrLf, vbLf, vbTab}, StringSplitOptions.RemoveEmptyEntries)
        If Peers.Length = 0 Then
            Hint("请先在「自定义节点」里填写节点地址，例如 tcp://1.2.3.4:11010", HintType.Red, False)
            Return
        End If
        '后台线程发起连接，避免在 UI 线程发起网络请求
        IsCheckingPeer = True
        BtnCheckPeer.Text = "正在测试……"
        Dim Thread As New Threading.Thread(
            Sub()
                Dim Lines As New List(Of String)
                For Each Peer In Peers
                    Lines.Add(TestOnePeer(Peer.Trim))
                Next
                Dispatcher.Invoke(
                    Sub()
                        IsCheckingPeer = False
                        BtnCheckPeer.Text = "测试自定义节点连接状态"
                        Dim ResultText As String = String.Join(vbCrLf, Lines)
                        Logger.Info($"自定义节点连接测试结果：{vbCrLf}{ResultText}")
                        MyMsgBox(ResultText, "自定义节点连接测试", "确定")
                    End Sub)
            End Sub) With {.IsBackground = True}
        Thread.Start()
    End Sub

    ''' <summary>
    ''' 测试单个节点是否可以连通，返回一行结果文本。仅做 TCP 连接测试，不会加入联机房间。
    ''' </summary>
    Private Shared Function TestOnePeer(Peer As String) As String
        '拆分协议、主机与端口
        Dim Scheme As String = "tcp"
        Dim HostPart As String = Peer
        Dim SchemeIndex As Integer = Peer.IndexOf("://", StringComparison.Ordinal)
        If SchemeIndex >= 0 Then
            Scheme = Peer.Substring(0, SchemeIndex).ToLowerInvariant()
            HostPart = Peer.Substring(SchemeIndex + 3)
        End If
        Dim SlashIndex As Integer = HostPart.IndexOf("/"c)
        If SlashIndex >= 0 Then HostPart = HostPart.Substring(0, SlashIndex)
        Dim Host As String
        Dim Port As Integer
        Try
            If HostPart.StartsWith("[") Then
                'IPv6 形如 [::1]:11010
                Dim EndBracket As Integer = HostPart.IndexOf("]"c)
                If EndBracket < 0 Then Return $"{Peer}　✗ 地址格式无法识别"
                Host = HostPart.Substring(1, EndBracket - 1)
                Port = If(EndBracket + 1 < HostPart.Length AndAlso HostPart(EndBracket + 1) = ":"c,
                          Integer.Parse(HostPart.Substring(EndBracket + 2)), ModLinkConfig.DefaultPort)
            ElseIf HostPart.Contains(":") Then
                Dim ColonIndex As Integer = HostPart.LastIndexOf(":"c)
                Host = HostPart.Substring(0, ColonIndex)
                Port = Integer.Parse(HostPart.Substring(ColonIndex + 1))
            Else
                Host = HostPart
                Port = ModLinkConfig.DefaultPort
            End If
        Catch ex As Exception
            Return $"{Peer}　✗ 端口不是有效数字"
        End Try
        If String.IsNullOrWhiteSpace(Host) Then Return $"{Peer}　✗ 地址格式无法识别"
        If Port <= 0 OrElse Port > 65535 Then Return $"{Peer}　✗ 端口超出范围（1 ~ 65535）"
        'UDP 节点无法用 TCP 探测，只能检查域名能否解析
        If Scheme = "udp" Then
            Try
                Dns.GetHostAddresses(Host)
                Return $"{Peer}　？ UDP 节点无法直接探测，域名解析正常"
            Catch ex As Exception
                Return $"{Peer}　✗ 域名无法解析（{ex.GetBaseException.Message}）"
            End Try
        End If
        'TCP 连接测试
        Dim Watch As Diagnostics.Stopwatch = Diagnostics.Stopwatch.StartNew()
        Try
            Using Client As New TcpClient()
                Dim ConnectTask As Task = Client.ConnectAsync(Host, Port)
                If Not ConnectTask.Wait(5000) Then Return $"{Peer}　✗ 连接超时（5 秒无响应）"
                If Client.Connected Then
                    Return $"{Peer}　✓ 可以连通（{Watch.ElapsedMilliseconds} ms）"
                Else
                    Return $"{Peer}　✗ 无法连通"
                End If
            End Using
        Catch ex As AggregateException
            Return $"{Peer}　✗ 连接失败：{ex.GetBaseException.Message}"
        Catch ex As Exception
            Return $"{Peer}　✗ 连接失败：{ex.GetBaseException.Message}"
        End Try
    End Function

#End Region

#Region "樱花映射登录"

    ''' <summary>
    ''' 下一次打开本页面时是否定位到樱花映射卡片。联机页跳转过来前会把它设为 True。
    ''' </summary>
    Public Shared LocateSakuraOnLoad As Boolean = False

    Private IsLoggingIn As Boolean

    ''' <summary>
    ''' 把页面滚动到樱花映射卡片的位置。
    ''' </summary>
    Public Sub LocateSakura()
        If Not IsLoaded Then
            LocateSakuraOnLoad = True
            Return
        End If
        Try
            Dim Offset As Double = CardSakura.TranslatePoint(New Windows.Point(0, 0), PanMain).Y
            PanBack.ScrollToVerticalOffset(Math.Max(0, Offset - 20))
        Catch ex As Exception
            Logger.Warn(ex, "定位樱花映射设置失败")
        End Try
    End Sub

    ''' <summary>
    ''' 刷新樱花映射的登录状态显示。
    ''' </summary>
    Public Sub RefreshSakuraLogin()
        If SakuraFrp.IsLoggedIn Then
            TextSakuraToken.Text = SakuraFrp.Token
            LabSakuraLogin.Text = $"已登录：{SakuraFrp.UserName}"
            BtnSakuraLogin.Text = "重新登录"
            BtnSakuraLogout.IsEnabled = True
        Else
            LabSakuraLogin.Text = "尚未登录樱花映射。"
            BtnSakuraLogin.Text = "登录"
            BtnSakuraLogout.IsEnabled = False
        End If
    End Sub

    Private Sub BtnSakuraLogin_Click(sender As Object, e As RoutedEventArgs) Handles BtnSakuraLogin.Click
        If IsLoggingIn Then Return
        Dim NewToken As String = If(TextSakuraToken.Text, "").Trim
        If NewToken = "" Then
            Hint("请先填写樱花映射的访问密钥", HintType.Red, False)
            Return
        End If
        IsLoggingIn = True
        BtnSakuraLogin.Text = "正在登录……"
        Dim Thread As New Threading.Thread(
            Sub()
                Try
                    SakuraFrp.Login(NewToken)
                    RunInUi(
                    Sub()
                        IsLoggingIn = False
                        RefreshSakuraLogin()
                        Hint($"已登录樱花映射：{SakuraFrp.UserName}", HintType.Green, False)
                    End Sub)
                Catch ex As Exception
                    Logger.Warn(ex, "樱花映射登录失败")
                    RunInUi(
                    Sub()
                        IsLoggingIn = False
                        RefreshSakuraLogin()
                        Hint("登录失败：" & ex.Message, HintType.Red, False)
                    End Sub)
                End Try
            End Sub) With {.IsBackground = True}
        Thread.Start()
    End Sub

    Private Sub BtnSakuraLogout_Click(sender As Object, e As RoutedEventArgs) Handles BtnSakuraLogout.Click
        If Not SakuraFrp.IsLoggedIn Then Return
        If MyMsgBox("确定要退出樱花映射登录吗？" & vbCrLf & "退出后会停止正在运行的隧道映射。", "退出登录", "确定", "取消") = 2 Then Return
        SakuraFrp.Logout()
        RefreshSakuraLogin()
        Hint("已退出樱花映射登录", HintType.Blue, False)
    End Sub

    Private Sub BtnSakuraOpenPanel_Click(sender As Object, e As RoutedEventArgs) Handles BtnSakuraOpenPanel.Click
        OpenWebsite("https://www.natfrp.com/user/")
    End Sub

#End Region

End Class
