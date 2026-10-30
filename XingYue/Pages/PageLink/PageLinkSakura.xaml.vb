Imports Newtonsoft.Json.Linq
Imports XingYue

''' <summary>
''' 樱花映射页面：创建隧道、按端口分别列出所有隧道、逐条启动/停止 frpc。
''' </summary>
Public Class PageLinkSakura

    Private SakuraNodes As JObject
    ''' <summary>账号下的所有隧道（按本地端口排序后显示）。</summary>
    Private ReadOnly SakuraTunnels As New List(Of JObject)
    ''' <summary>节点 ID → 延迟（毫秒，-1 表示超时/未测出）。</summary>
    Private ReadOnly NodeLatency As New Dictionary(Of Integer, Integer)
    ''' <summary>正在启动/停止/删除的隧道 ID，操作期间对应按钮置灰。</summary>
    Private ReadOnly BusyTunnels As New HashSet(Of Integer)
    Private IsPinging As Boolean
    Private IsSakuraLoading As Boolean
    Private sakuraInitDone As Boolean

    ''' <summary>
    ''' 每次进入页面都把界面状态刷新一遍。
    ''' 这里必须自己捕获异常：PageEnter 抛出异常会中断 MyPageRight 的进入动画，页面会整个空白。
    ''' </summary>
    Private Sub PageEnterHandler() Handles Me.PageEnter
        Try
            RefreshSakuraPage()
        Catch ex As Exception
            Logger.Error(ex, "刷新樱花映射页面失败")
            Hint("刷新樱花映射页面失败：" & ex.Message, HintType.Red, False)
        End Try
    End Sub

    ''' <summary>
    ''' 进入樱花映射页面时刷新显示。
    ''' </summary>
    Private Sub RefreshSakuraPage()
        Dim LoggedIn As Boolean = SakuraFrp.IsLoggedIn
        PanSakuraLogin.Visibility = If(LoggedIn, Visibility.Collapsed, Visibility.Visible)
        BtnSakuraCreate.IsEnabled = LoggedIn
        BtnSakuraRefreshNodes.IsEnabled = LoggedIn AndAlso Not IsSakuraLoading
        If Not LoggedIn Then
            SakuraTunnels.Clear()
            RefreshTunnelList()
            Return
        End If
        If sakuraInitDone Then
            RefreshTunnelList()
            Return
        End If
        sakuraInitDone = True
        TextSakuraName.Text = "XingYue-" & RandomInteger(1000, 9999).ToString()
        TextSakuraPort.Text = SakuraFrp.LastLocalPort.ToString()
        RefreshTunnelList()
        LoadSakuraNodes()
    End Sub

    Private Sub BtnSakuraGoLogin_Click(sender As Object, e As EventArgs) Handles BtnSakuraGoLogin.Click
        PageSetupLink.LocateSakuraOnLoad = True
        FrmMain.PageChange(New FormMain.PageStackData With {.Page = FormMain.PageType.Setup}, FormMain.PageSubType.SetupLink)
        If FrmSetupLink IsNot Nothing Then FrmSetupLink.LocateSakura()
    End Sub

    '===============================
    '  节点列表与延迟检测
    '===============================

    ''' <summary>
    ''' 获取节点列表与账号下已有的隧道。
    ''' </summary>
    Private Sub LoadSakuraNodes()
        If IsSakuraLoading Then Return
        IsSakuraLoading = True
        BtnSakuraRefreshNodes.Text = "正在获取……"
        UpdateSakuraButtons()
        Dim Thread As New Threading.Thread(
            Sub()
                Try
                    Dim Nodes As JObject = SakuraFrp.GetNodes()
                    Dim Tunnels As JArray = SakuraFrp.GetTunnels()
                    RunInUi(
                    Sub()
                        Try
                            IsSakuraLoading = False
                            BtnSakuraRefreshNodes.Text = "刷新节点"
                            SakuraNodes = Nodes
                            FillSakuraNodes()
                            SetTunnelList(Tunnels)
                            UpdateSakuraButtons()
                        Catch ex As Exception
                            Logger.Error(ex, "刷新樱花映射节点列表失败")
                            Hint("刷新樱花映射节点列表失败：" & ex.Message, HintType.Red, False)
                        End Try
                    End Sub)
                Catch ex As Exception
                    Logger.Warn(ex, "获取樱花映射节点失败")
                    RunInUi(
                    Sub()
                        IsSakuraLoading = False
                        BtnSakuraRefreshNodes.Text = "刷新节点"
                        UpdateSakuraButtons()
                        Hint("获取樱花映射节点失败：" & ex.Message, HintType.Red, False)
                    End Sub)
                End Try
            End Sub) With {.IsBackground = True}
        Thread.Start()
    End Sub

    Private Sub BtnSakuraRefreshNodes_Click(sender As Object, e As EventArgs) Handles BtnSakuraRefreshNodes.Click
        LoadSakuraNodes()
    End Sub

    ''' <summary>
    ''' 把可用节点填进下拉框，随后在后台测一遍延迟。
    ''' </summary>
    Private Sub FillSakuraNodes()
        If SakuraNodes Is Nothing Then Return
        Dim OldNodeId As Integer = SakuraFrp.LastNodeId
        Dim OldItem As MyComboBoxItem = TryCast(ComboSakuraNode.SelectedItem, MyComboBoxItem)
        If OldItem IsNot Nothing Then OldNodeId = Val(OldItem.Tag)
        Try
            AniControlEnabled += 1
            ComboSakuraNode.Items.Clear()
            Dim SelectedIndex As Integer = -1, Index As Integer = 0
            For Each Pair As KeyValuePair(Of String, JToken) In SakuraNodes
                Dim Node As JObject = TryCast(Pair.Value, JObject)
                If Node Is Nothing Then Continue For
                If Not SakuraFrp.IsNodeUsable(Node) Then Continue For
                Dim NodeId As Integer
                If Not Integer.TryParse(Pair.Key, NodeId) Then Continue For
                Dim Item As New MyComboBoxItem With {
                    .Content = GetNodeText(NodeId, Node),
                    .Tag = NodeId
                }
                ComboSakuraNode.Items.Add(Item)
                If NodeId = OldNodeId Then SelectedIndex = Index
                Index += 1
            Next
            If ComboSakuraNode.Items.Count = 0 Then
                Hint("当前没有可用的樱花映射节点，请稍后再试。", HintType.Red, False)
            Else
                ComboSakuraNode.SelectedIndex = If(SelectedIndex >= 0, SelectedIndex, 0)
            End If
        Finally
            AniControlEnabled -= 1
        End Try
        StartPingNodes()
    End Sub

    ''' <summary>
    ''' 单条节点在下拉框里显示的文字，带延迟。
    ''' </summary>
    Private Function GetNodeText(NodeId As Integer, Node As JObject) As String
        Dim Host As String = $"{If(Node("host"), "")}"
        Dim Text As String = $"{If(Node("name"), "")} ({Host})"
        If Host = "" Then Return Text & " · 无地址"
        Dim Lat As Integer = 0
        If Not NodeLatency.TryGetValue(NodeId, Lat) Then Return Text
        Return Text & If(Lat < 0, " · 超时", $" · {Lat}ms")
    End Function

    ''' <summary>
    ''' 后台并发测所有可用节点的延迟，测到一个就刷新一次下拉框文字。
    ''' </summary>
    Private Sub StartPingNodes()
        If IsPinging OrElse SakuraNodes Is Nothing Then Return
        IsPinging = True
        Dim Targets As New List(Of KeyValuePair(Of Integer, String))
        For Each Pair As KeyValuePair(Of String, JToken) In SakuraNodes
            Dim Node As JObject = TryCast(Pair.Value, JObject)
            If Node Is Nothing Then Continue For
            If Not SakuraFrp.IsNodeUsable(Node) Then Continue For
            Dim NodeId As Integer
            If Not Integer.TryParse(Pair.Key, NodeId) Then Continue For
            Dim Host As String = $"{If(Node("host"), "")}"
            If Host = "" Then Continue For
            Targets.Add(New KeyValuePair(Of Integer, String)(NodeId, Host))
        Next
        If Targets.Count = 0 Then
            IsPinging = False
            Return
        End If
        Dim Thread As New Threading.Thread(
            Sub()
                Try
                    Threading.Tasks.Parallel.ForEach(
                        Targets,
                        Sub(Target)
                            Dim Ms As Integer = SakuraFrp.PingNode(Target.Value)
                            Dim Changed As Boolean = False
                            SyncLock NodeLatency
                                Dim Old As Integer = 0
                                If Not NodeLatency.TryGetValue(Target.Key, Old) OrElse Old <> Ms Then
                                    NodeLatency(Target.Key) = Ms
                                    Changed = True
                                End If
                            End SyncLock
                            If Changed Then
                                RunInUi(
                                Sub()
                                    Try
                                        RefreshNodeLatencyText()
                                    Catch ex As Exception
                                        Logger.Warn(ex, "刷新节点延迟失败")
                                    End Try
                                End Sub)
                                Threading.Thread.Sleep(40) '别把 UI 线程刷爆
                            End If
                        End Sub)
                Catch ex As Exception
                    Logger.Warn(ex, "节点延迟检测失败")
                Finally
                    RunInUi(Sub() IsPinging = False)
                End Try
            End Sub) With {.IsBackground = True}
        Thread.Start()
    End Sub

    ''' <summary>
    ''' 按最新的延迟数据改写下拉框里每一项的文字（不重建列表，避免打断用户操作）。
    ''' </summary>
    Private Sub RefreshNodeLatencyText()
        If SakuraNodes Is Nothing Then Return
        Try
            AniControlEnabled += 1
            For Each Item As Object In ComboSakuraNode.Items
                Dim ComboItem As MyComboBoxItem = TryCast(Item, MyComboBoxItem)
                If ComboItem Is Nothing Then Continue For
                Dim NodeId As Integer = Val(ComboItem.Tag)
                Dim Node As JObject = TryCast(SakuraNodes(NodeId.ToString()), JObject)
                If Node Is Nothing Then Continue For
                Dim NewText As String = GetNodeText(NodeId, Node)
                If $"{ComboItem.Content}" <> NewText Then ComboItem.Content = NewText
            Next
        Finally
            AniControlEnabled -= 1
        End Try
    End Sub

    '===============================
    '  隧道列表
    '===============================

    ''' <summary>
    ''' 用接口返回的隧道列表替换本地列表。
    ''' </summary>
    Private Sub SetTunnelList(Tunnels As JArray)
        SakuraTunnels.Clear()
        If Tunnels IsNot Nothing Then
            For Each Item As JToken In Tunnels
                Dim Tunnel As JObject = TryCast(Item, JObject)
                If Tunnel Is Nothing Then Continue For
                If Tunnel("id") Is Nothing Then Continue For
                SakuraTunnels.Add(Tunnel)
            Next
        End If
        '按本地端口排序，端口相同按隧道 ID
        SakuraTunnels.Sort(
            Function(A As JObject, B As JObject)
                Dim Result As Integer = TunnelInt(A, "local_port").CompareTo(TunnelInt(B, "local_port"))
                If Result <> 0 Then Return Result
                Return TunnelInt(A, "id").CompareTo(TunnelInt(B, "id"))
            End Function)
        '端口输入框回填上次用的那条隧道，找不到就用第一条
        If SakuraTunnels.Count > 0 Then
            Dim Preferred As JObject = SakuraTunnels(0)
            For Each Tunnel As JObject In SakuraTunnels
                If TunnelInt(Tunnel, "id") = SakuraFrp.LastTunnelId Then
                    Preferred = Tunnel
                    Exit For
                End If
            Next
            Dim Port As Integer = TunnelInt(Preferred, "local_port")
            If Port > 0 Then TextSakuraPort.Text = Port.ToString()
        End If
        RefreshTunnelList()
    End Sub

    ''' <summary>
    ''' 按端口逐行重画隧道列表。
    ''' </summary>
    Private Sub RefreshTunnelList()
        Try
            AniControlEnabled += 1
            PanSakuraTunnels.Children.Clear()
            For Each Tunnel As JObject In SakuraTunnels
                PanSakuraTunnels.Children.Add(BuildTunnelRow(Tunnel))
            Next
            LabSakuraEmpty.Visibility = If(SakuraTunnels.Count = 0, Visibility.Visible, Visibility.Collapsed)
            If Not SakuraFrp.IsLoggedIn Then LabSakuraEmpty.Text = "尚未登录樱花映射。"
            If SakuraTunnels.Count = 0 AndAlso SakuraFrp.IsLoggedIn Then LabSakuraEmpty.Text = "还没有创建隧道，先在上面创建一条吧。"
        Finally
            AniControlEnabled -= 1
        End Try
        UpdateSakuraButtons()
    End Sub

    ''' <summary>
    ''' 生成一行隧道：地址 + 本地端口/隧道 ID/状态 + 四个按钮。
    ''' </summary>
    Private Function BuildTunnelRow(Tunnel As JObject) As FrameworkElement
        Dim TunnelId As Integer = TunnelInt(Tunnel, "id")
        Dim LocalPort As Integer = TunnelInt(Tunnel, "local_port")
        Dim Address As String = SakuraFrp.GetTunnelAddress(Tunnel, SakuraNodes)
        Dim Running As Boolean = SakuraFrp.IsFrpcRunning(TunnelId)
        Dim Busy As Boolean = BusyTunnels.Contains(TunnelId)

        Dim Row As New Grid With {.Margin = New Thickness(0, 0, 0, 12)}
        Row.RowDefinitions.Add(New RowDefinition With {.Height = GridLength.Auto})
        Row.RowDefinitions.Add(New RowDefinition With {.Height = GridLength.Auto})
        Row.ColumnDefinitions.Add(New ColumnDefinition With {.Width = New GridLength(1, GridUnitType.Star)})
        Row.ColumnDefinitions.Add(New ColumnDefinition With {.Width = GridLength.Auto})

        Dim Texts As New StackPanel With {.VerticalAlignment = VerticalAlignment.Center}
        Dim LabAddress As New TextBlock With {
            .Text = If(Address = "", "（请到樱花映射面板查看地址）", Address),
            .FontSize = 17,
            .FontFamily = New Windows.Media.FontFamily("Consolas"),
            .TextWrapping = TextWrapping.Wrap
        }
        LabAddress.SetResourceReference(TextBlock.ForegroundProperty, If(Running, "ColorBrush2", "ColorBrushGray3"))
        Dim StatusText As String = If(Running, "映射运行中", "未启动")
        If Busy Then StatusText = "处理中……"
        Dim LabInfo As New TextBlock With {
            .Text = $"本地端口 {LocalPort}　隧道 ID {TunnelId}　{StatusText}",
            .FontSize = 12.5,
            .Margin = New Thickness(0, 4, 0, 0)
        }
        LabInfo.SetResourceReference(TextBlock.ForegroundProperty, "ColorBrushGray4")
        Texts.Children.Add(LabAddress)
        Texts.Children.Add(LabInfo)
        Grid.SetColumn(Texts, 0)
        Row.Children.Add(Texts)

        Dim Buttons As New StackPanel With {
            .Orientation = Orientation.Horizontal,
            .VerticalAlignment = VerticalAlignment.Center,
            .Margin = New Thickness(15, 0, 0, 0)
        }
        Dim BtnCopy As New MyButton With {.Text = "复制", .MinWidth = 66, .Padding = New Thickness(12, 5, 12, 5), .Margin = New Thickness(0, 0, 8, 0)}
        Dim BtnStart As New MyButton With {.Text = "启动", .MinWidth = 66, .Padding = New Thickness(12, 5, 12, 5), .Margin = New Thickness(0, 0, 8, 0), .ColorType = MyButton.ColorState.Highlight}
        Dim BtnStop As New MyButton With {.Text = "停止", .MinWidth = 66, .Padding = New Thickness(12, 5, 12, 5), .Margin = New Thickness(0, 0, 8, 0)}
        Dim BtnDelete As New MyButton With {.Text = "删除", .MinWidth = 66, .Padding = New Thickness(12, 5, 12, 5)}
        BtnCopy.IsEnabled = Address <> ""
        BtnStart.IsEnabled = Not Running AndAlso Not Busy
        BtnStop.IsEnabled = Running AndAlso Not Busy
        BtnDelete.IsEnabled = Not Busy
        If Not SakuraFrp.IsLoggedIn Then
            BtnStart.IsEnabled = False
            BtnStop.IsEnabled = False
            BtnDelete.IsEnabled = False
        End If
        AddHandler BtnCopy.Click,
            Sub()
                ClipboardSet(Address, False)
                Hint("已复制映射地址！", HintType.Green, False)
            End Sub
        AddHandler BtnStart.Click, Sub() StartTunnel(TunnelId)
        AddHandler BtnStop.Click, Sub() StopTunnel(TunnelId)
        AddHandler BtnDelete.Click, Sub() DeleteTunnel(TunnelId)
        Buttons.Children.Add(BtnCopy)
        Buttons.Children.Add(BtnStart)
        Buttons.Children.Add(BtnStop)
        Buttons.Children.Add(BtnDelete)
        Grid.SetColumn(Buttons, 1)
        Row.Children.Add(Buttons)

        Dim Separator As New Border With {
            .BorderThickness = New Thickness(0, 0, 0, 1),
            .Padding = New Thickness(0, 0, 0, 12)
        }
        Separator.SetResourceReference(Border.BorderBrushProperty, "ColorBrushGray5")
        Separator.Child = Row
        Return Separator
    End Function

    Private Function TunnelInt(Tunnel As JObject, Key As String) As Integer
        If Tunnel Is Nothing Then Return 0
        Dim Token As JToken = Tunnel(Key)
        If Token Is Nothing Then Return 0
        Dim Value As Integer
        If Integer.TryParse(Token.ToString, Value) Then Return Value
        Return 0
    End Function

    '===============================
    '  隧道操作
    '===============================

    Private Sub StartTunnel(TunnelId As Integer)
        If BusyTunnels.Contains(TunnelId) Then Return
        BusyTunnels.Add(TunnelId)
        RefreshTunnelList()
        Dim Thread As New Threading.Thread(
            Sub()
                Try
                    SakuraFrp.StartFrpc(TunnelId)
                    Threading.Thread.Sleep(800) '等一会，让连接建立
                    RunInUi(
                    Sub()
                        BusyTunnels.Remove(TunnelId)
                        RefreshTunnelList()
                        Hint("映射已启动，把地址发给朋友吧！", HintType.Green, False)
                    End Sub)
                Catch ex As Exception
                    Logger.Warn(ex, "启动樱花映射失败")
                    RunInUi(
                    Sub()
                        BusyTunnels.Remove(TunnelId)
                        RefreshTunnelList()
                        Hint("启动映射失败：" & ex.Message, HintType.Red, False)
                    End Sub)
                End Try
            End Sub) With {.IsBackground = True}
        Thread.Start()
    End Sub

    Private Sub StopTunnel(TunnelId As Integer)
        If BusyTunnels.Contains(TunnelId) Then Return
        BusyTunnels.Add(TunnelId)
        RefreshTunnelList()
        Dim Thread As New Threading.Thread(
            Sub()
                Try
                    SakuraFrp.StopFrpc(TunnelId)
                Catch ex As Exception
                    Logger.Warn(ex, "停止樱花映射失败")
                End Try
                RunInUi(
                Sub()
                    BusyTunnels.Remove(TunnelId)
                    RefreshTunnelList()
                    Hint("已停止这条映射", HintType.Blue, False)
                End Sub)
            End Sub) With {.IsBackground = True}
        Thread.Start()
    End Sub

    Private Sub DeleteTunnel(TunnelId As Integer)
        If BusyTunnels.Contains(TunnelId) Then Return
        If MyMsgBox($"确定要删除隧道 {TunnelId} 吗？{vbCrLf}删除后这个映射地址就失效了。", "删除隧道", "确定", "取消", IsWarn:=True) = 2 Then Return
        BusyTunnels.Add(TunnelId)
        RefreshTunnelList()
        Dim Thread As New Threading.Thread(
            Sub()
                Try
                    SakuraFrp.StopFrpc(TunnelId)
                    SakuraFrp.DeleteTunnel(TunnelId)
                    RunInUi(
                    Sub()
                        BusyTunnels.Remove(TunnelId)
                        For i As Integer = SakuraTunnels.Count - 1 To 0 Step -1
                            If TunnelInt(SakuraTunnels(i), "id") = TunnelId Then SakuraTunnels.RemoveAt(i)
                        Next
                        RefreshTunnelList()
                        Hint("隧道已删除", HintType.Green, False)
                    End Sub)
                Catch ex As Exception
                    RunInUi(
                    Sub()
                        BusyTunnels.Remove(TunnelId)
                        RefreshTunnelList()
                        Hint("删除隧道失败：" & ex.Message, HintType.Red, False)
                    End Sub)
                End Try
            End Sub) With {.IsBackground = True}
        Thread.Start()
    End Sub

    Private Sub BtnSakuraCreate_Click(sender As Object, e As EventArgs) Handles BtnSakuraCreate.Click
        Dim Name As String = If(TextSakuraName.Text, "").Trim
        If Name = "" Then
            Hint("请填写隧道名称", HintType.Red, False)
            Return
        End If
        Dim Port As Integer
        If Not Integer.TryParse(If(TextSakuraPort.Text, "").Trim, Port) OrElse Port < 1 OrElse Port > 65535 Then
            Hint("本地端口必须是 1 ~ 65535 之间的数字", HintType.Red, False)
            Return
        End If
        Dim Item As MyComboBoxItem = TryCast(ComboSakuraNode.SelectedItem, MyComboBoxItem)
        If Item Is Nothing Then
            Hint("请选择一个节点", HintType.Red, False)
            Return
        End If
        For Each Tunnel As JObject In SakuraTunnels
            If TunnelInt(Tunnel, "local_port") = Port Then
                If MyMsgBox($"已经有一条隧道在映射本地端口 {Port} 了。{vbCrLf}同一个本地端口只能映射一次，确定还要继续创建吗？",
                            "端口重复", "继续", "取消", IsWarn:=True) = 2 Then Return
                Exit For
            End If
        Next
        Dim NodeId As Integer = Val(Item.Tag)
        BtnSakuraCreate.IsEnabled = False
        Dim Thread As New Threading.Thread(
            Sub()
                Try
                    Dim Tunnel As JObject = SakuraFrp.CreateTcpTunnel(Name, NodeId, Port)
                    Dim Nodes As JObject = Nothing
                    Try
                        Nodes = SakuraFrp.GetNodes()
                    Catch ex As Exception
                        Logger.Warn(ex, "创建隧道成功后获取节点信息失败")
                    End Try
                    RunInUi(
                    Sub()
                        Try
                            If Nodes IsNot Nothing Then SakuraNodes = Nodes
                            SakuraTunnels.Add(Tunnel)
                            SakuraTunnels.Sort(
                                Function(A As JObject, B As JObject) TunnelInt(A, "local_port").CompareTo(TunnelInt(B, "local_port")))
                            Dim NewId As Integer = TunnelInt(Tunnel, "id")
                            SakuraFrp.SaveTunnelSettings(NewId, NodeId, Port)
                            RefreshTunnelList()
                            BtnSakuraCreate.IsEnabled = True
                            TextSakuraName.Text = "XingYue-" & RandomInteger(1000, 9999).ToString()
                            StartTunnel(NewId)
                        Catch ex As Exception
                            BtnSakuraCreate.IsEnabled = True
                            RefreshTunnelList()
                            Hint("创建隧道后刷新界面失败：" & ex.Message, HintType.Red, False)
                        End Try
                    End Sub)
                Catch ex As Exception
                    Logger.Warn(ex, "创建樱花映射隧道失败")
                    RunInUi(
                    Sub()
                        BtnSakuraCreate.IsEnabled = True
                        RefreshTunnelList()
                        Hint("创建隧道失败：" & ex.Message, HintType.Red, False)
                    End Sub)
                End Try
            End Sub) With {.IsBackground = True}
        Thread.Start()
    End Sub

    Private Sub UpdateSakuraButtons()
        Dim LoggedIn As Boolean = SakuraFrp.IsLoggedIn
        BtnSakuraCreate.IsEnabled = LoggedIn AndAlso Not IsSakuraLoading
        BtnSakuraRefreshNodes.IsEnabled = LoggedIn AndAlso Not IsSakuraLoading
    End Sub

End Class
