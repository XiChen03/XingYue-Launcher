Imports System.Windows.Threading

Public Class PageSetupSystem


    Private Sub PageSetupSystem_Loaded(sender As Object, e As RoutedEventArgs) Handles Me.Loaded

        '重复加载部分
        PanBack.ScrollToHome()

        '非重复加载部分
        Static IsLoaded As Boolean = False
        If IsLoaded Then Return
        IsLoaded = True

        AniControlEnabled += 1
        Reload()
        SliderLoad()
        AniControlEnabled -= 1

    End Sub
    Public Sub Reload()
        SettingService.RefreshSettings(Me)
    End Sub
    Public Sub Reset()
        Try
            SettingService.ResetSettings(Me)
            Logger.Info("已初始化启动器页设置")
            Hint("已初始化启动器页设置！", HintType.Green, False)
        Catch ex As Exception
            Logger.Error(ex, "初始化启动器页设置失败", LogBehavior.Alert)
        End Try
        Reload()
    End Sub

    '滑动条
    Private Sub SliderLoad()
        SliderDownloadThread.GetHintText = Function(v) v + 1
        SliderDownloadSpeed.GetHintText =
        Function(v)
            Select Case v
                Case Is <= 14
                    Return (v + 1) * 0.1 & " M/s"
                Case Is <= 31
                    Return (v - 11) * 0.5 & " M/s"
                Case Is <= 41
                    Return (v - 21) & " M/s"
                Case Else
                    Return "无限制"
            End Select
        End Function
        SliderDebugAnim.GetHintText = Function(v) If(v > 29, "关闭", (v / 10 + 0.1) & "x")
    End Sub
    Private Sub SliderDownloadThread_PreviewChange(sender As Object, e As RouteEventArgs) Handles SliderDownloadThread.PreviewChange
        If SliderDownloadThread.Value < 100 Then Return
        If Not Settings.Get(Of Boolean)("HintDownloadThread") Then
            Settings.Set("HintDownloadThread", True)
            MyMsgBox("如果设置过多的下载线程，可能会导致下载时出现非常严重的卡顿。" & vbCrLf &
                     "一般设置 64 线程即可满足大多数下载需求，除非你知道你在干什么，否则不建议设置更多的线程数！", "警告", "我知道了", IsWarn:=True)
        End If
    End Sub

    '调试模式
    Private Sub CheckDebugMode_Change() Handles CheckDebugMode.Change
        If AniControlEnabled = 0 Then Hint("部分调试信息将在刷新或启动器重启后切换显示！",, False)
    End Sub

#Region "社区资源下载源"

    '切换到官方源时要求用户自行填写 CurseForge API Key
    Private LastDownloadModIndex As Integer = -1
    Private IsCheckingDownloadModSource As Boolean = False

    Private Sub ComboDownloadMod_Loaded(sender As Object, e As RoutedEventArgs) Handles ComboDownloadMod.Loaded
        LastDownloadModIndex = ComboDownloadMod.SelectedIndex
    End Sub

    Private Sub ComboDownloadMod_DropDownOpened(sender As Object, e As EventArgs) Handles ComboDownloadMod.DropDownOpened
        If Not IsCheckingDownloadModSource Then LastDownloadModIndex = ComboDownloadMod.SelectedIndex
    End Sub

    Private Sub ComboDownloadMod_SelectionChanged(sender As Object, e As SelectionChangedEventArgs) Handles ComboDownloadMod.SelectionChanged
        If AniControlEnabled <> 0 OrElse Not ComboDownloadMod.IsLoaded Then Return
        If IsCheckingDownloadModSource Then Return
        '等 MyComboBox 内部的保存逻辑先跑完，再决定要不要要求填写 Key
        Dispatcher.BeginInvoke(DispatcherPriority.Background, New Action(AddressOf CheckDownloadModSource))
    End Sub

    Private Sub CheckDownloadModSource()
        If IsCheckingDownloadModSource Then Return
        If ComboDownloadMod.SelectedIndex <> 2 Then
            LastDownloadModIndex = ComboDownloadMod.SelectedIndex
            Return
        End If
        '用户自己配置了 Key，直接允许
        If Not String.IsNullOrWhiteSpace(ModSecret.CurseForgeUserAPIKey) Then
            LastDownloadModIndex = 2
            Return
        End If

        IsCheckingDownloadModSource = True
        Try
            Dim EnableOfficial As Boolean = False
            Do
                Dim Reason As String = "使用官方源需要你自己的 CurseForge API Key。"

                Dim Choice As Integer = MyMsgBox(
                    Reason & vbCrLf & vbCrLf &
                    "你可以这样申请自己的 Key：" & vbCrLf &
                    "1. 打开 CurseForge 的申请说明页，按要求填写表单（需要说明用途，人工审核）" & vbCrLf &
                    "2. 审核通过后会通过邮件收到你的 Key" & vbCrLf & vbCrLf &
                    "如果不方便申请，直接使用镜像源即可：镜像源不需要 Key，加载失败时也会自动回退到官方源。",
                    "需要 CurseForge API Key", "去填写我自己的 Key", "取消")
                If Choice <> 1 Then Exit Do

                Dim InputKey As String = MyMsgBoxInput("填写 CurseForge API Key",
                    "把申请到的 Key 粘贴到下面。点击确定后会立即联网验证，验证通过才会切换为官方源。" & vbCrLf &
                    "Key 只会保存在本机注册表中，不会上传到任何地方。",
                    HintText:="例如 $2a$10$xxxxxxxxxxxxxxxxxxxxxxxxxxxxxx")
                If InputKey Is Nothing Then Exit Do
                InputKey = InputKey.Trim()
                If InputKey.Length = 0 Then
                    Hint("没有填写 API Key，下载源没有更换", HintType.Red, False)
                    Exit Do
                End If

                Select Case RunInBackground(Function() CurseForgeCheck.VerifyKey(InputKey), 15000, -1)
                    Case 1 '有效
                        Settings.Set("CacheCurseForgeKey", InputKey)
                        CurseForgeCheck.ResetCache()
                        EnableOfficial = True
                        Exit Do
                    Case 0 '无效
                        If MyMsgBox("这个 Key 没有通过官方接口验证，可能填错了或者已经被吊销。" & vbCrLf & "要重新填写一次吗？",
                                    "验证失败", "重新填写", "取消", IsWarn:=True) <> 1 Then Exit Do
                    Case Else '无法验证
                        If MyMsgBox("暂时无法联网验证这个 Key（可能是网络问题）。" & vbCrLf & "要保存它并切换为官方源吗？",
                                    "无法验证", "保存并切换", "取消", IsWarn:=True) <> 1 Then Exit Do
                        Settings.Set("CacheCurseForgeKey", InputKey)
                        CurseForgeCheck.ResetCache()
                        EnableOfficial = True
                        Exit Do
                End Select
            Loop

            If EnableOfficial Then
                Settings.Set("ToolDownloadMod", 2)
                LastDownloadModIndex = 2
                Hint("已切换为官方下载源", HintType.Green, False)
            Else
                '取消或没有填写，回退到之前的选项，不更换下载源
                Dim Fallback As Integer = If(LastDownloadModIndex >= 0 AndAlso LastDownloadModIndex <= 1, LastDownloadModIndex, 0)
                Settings.Set("ToolDownloadMod", Fallback)
                If ComboDownloadMod.SelectedIndex <> Fallback Then ComboDownloadMod.SelectedIndex = Fallback
                LastDownloadModIndex = Fallback
            End If
        Catch ex As Exception
            Logger.Error(ex, "检查社区资源下载源失败", LogBehavior.Toast)
        Finally
            IsCheckingDownloadModSource = False
        End Try
    End Sub

    ''' <summary>
    ''' 在后台线程执行操作，等待期间保持界面响应（用于在 UI 线程里发起网络请求）。
    ''' 超时未完成时返回 Fallback。
    ''' </summary>
    Private Function RunInBackground(Of T)(Work As Func(Of T), TimeoutMs As Integer, Fallback As T) As T
        Dim Result As T = Fallback
        Dim Worker As New Threading.Thread(Sub() Result = Work()) With {.IsBackground = True}
        Worker.Start()
        Dim Frame As New DispatcherFrame()
        Dim PollTimer As Threading.Timer = Nothing, TimeoutTimer As Threading.Timer = Nothing
        Try
            PollTimer = New Threading.Timer(Sub(state)
                                                If Not Worker.IsAlive Then Dispatcher.BeginInvoke(New Action(Sub() Frame.Continue = False))
                                            End Sub, Nothing, 100, 100)
            TimeoutTimer = New Threading.Timer(Sub(state) Dispatcher.BeginInvoke(New Action(Sub() Frame.Continue = False)),
                                               Nothing, TimeoutMs, Threading.Timeout.Infinite)
            Dispatcher.PushFrame(Frame)
        Finally
            PollTimer?.Dispose()
            TimeoutTimer?.Dispose()
        End Try
        Return Result
    End Function

    ''' <summary>
    ''' 把「下载源」下拉框同步为当前设置值（用于其它页面自动切换下载源后刷新界面）。
    ''' </summary>
    Public Sub SyncDownloadModCombo()
        Try
            Dim Index As Integer = Settings.Get(Of Integer)("ToolDownloadMod")
            If Index < 0 OrElse Index > 2 Then Index = 0
            IsCheckingDownloadModSource = True
            Try
                If ComboDownloadMod.SelectedIndex <> Index Then ComboDownloadMod.SelectedIndex = Index
            Finally
                IsCheckingDownloadModSource = False
            End Try
            LastDownloadModIndex = Index
        Catch ex As Exception
            Logger.Error(ex, "同步下载源下拉框失败")
        End Try
    End Sub

    ''' <summary>
    ''' 让「下载源」下拉框获得焦点，方便用户直接修改（不会触发联网检测）。
    ''' </summary>
    Public Sub FocusDownloadModCombo()
        Try
            ComboDownloadMod.Focus()
        Catch ex As Exception
        End Try
    End Sub

#End Region

#Region "导出 / 导入设置"

    Private Sub BtnSystemSettingExp_Click(sender As Object, e As MouseButtonEventArgs) Handles BtnSystemSettingExp.Click
        Hint("该功能尚在开发中！")
    End Sub
    Private Sub BtnSystemSettingImp_Click(sender As Object, e As MouseButtonEventArgs) Handles BtnSystemSettingImp.Click
        Hint("该功能尚在开发中！")
    End Sub

#End Region

End Class
