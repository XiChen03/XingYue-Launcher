Imports System.IO
Imports System.Net
Imports System.Runtime.InteropServices
Imports System.Security.Principal
Imports System.Windows.Input

''' <summary>
''' 更多 → 百宝箱。
''' </summary>
Public Class PageOtherTest

#Region "初始化"

    Private IsLoad As Boolean
    Private Shared IsMemoryOptimizeRunning As Boolean
    Private IsCaveEnabled As Boolean

    Public Sub New()
        InitializeComponent()
        IsLoad = False
        IsCaveEnabled = True
        AddHandler MyTextBox.ValidateChanged, Sub() StartButtonRefresh()
        StartButtonRefresh()
    End Sub

    Private Sub MeLoaded(sender As Object, e As RoutedEventArgs) Handles Me.Loaded
        If IsLoad Then Return
        IsLoad = True
        TextDownloadFolder.Text = Settings.Get(Of String)("CacheDownloadFolder")
        TextDownloadFolder.Validate()
        If Not TextDownloadFolder.IsValidated OrElse TextDownloadFolder.Text = "" Then
            TextDownloadFolder.Text = Paths.Base & "XingYue\MyDownload\"
        End If
        TextDownloadFolder.Validate()
        TextDownloadName.Validate()
        StartButtonRefresh()
    End Sub

    Private Sub SaveCacheDownloadFolder()
        If Not TextDownloadFolder.IsValidated OrElse TextDownloadFolder.Text = "" Then Return
        Settings.Set("CacheDownloadFolder", TextDownloadFolder.Text)
        Dim Rule = If(TextDownloadName.ValidateRules.Count > 0, TryCast(TextDownloadName.ValidateRules(0), ValidateFileName), Nothing)
        If Rule IsNot Nothing Then Rule.ParentFolder = TextDownloadFolder.Text
        TextDownloadName.Validate()
    End Sub

    Private Sub StartButtonRefresh()
        BtnDownloadStart.IsEnabled = TextDownloadFolder.IsValidated AndAlso TextDownloadUrl.IsValidated AndAlso TextDownloadName.IsValidated
        BtnDownloadOpen.IsEnabled = TextDownloadFolder.IsValidated
    End Sub

#End Region

#Region "下载自定义文件"

    Private Sub TextDownloadUrl_KeyDown(sender As Object, e As KeyEventArgs) Handles TextDownloadUrl.KeyDown
        If e.Key = Key.Return AndAlso BtnDownloadStart.IsEnabled Then BtnDownloadStart_Click(sender, Nothing)
    End Sub

    Private Sub TextDownloadUrl_ValidatedTextChanged(sender As Object, e As RoutedEventArgs) Handles TextDownloadUrl.ValidatedTextChanged
        Try
            If TextDownloadName.Text = "" AndAlso TextDownloadUrl.Text <> "" Then
                TextDownloadName.Text = PathUtils.GetLastPart(WebUtility.UrlDecode(TextDownloadUrl.Text))
            End If
        Catch ex As Exception
            Logger.Warn(ex, "自动填充下载文件名失败")
        End Try
    End Sub

    Private Sub TextDownloadFolder_ValidatedTextChanged(sender As Object, e As RoutedEventArgs) Handles TextDownloadFolder.ValidatedTextChanged
        SaveCacheDownloadFolder()
    End Sub

    Private Sub BtnDownloadStart_Click(sender As Object, e As MouseButtonEventArgs) Handles BtnDownloadStart.Click
        StartCustomDownload(TextDownloadUrl.Text, TextDownloadName.Text, TextDownloadFolder.Text)
        TextDownloadUrl.Text = ""
        TextDownloadUrl.Validate()
        TextDownloadUrl.ForceShowAsSuccess()
        TextDownloadName.Text = ""
        TextDownloadName.Validate()
        TextDownloadName.ForceShowAsSuccess()
        StartButtonRefresh()
    End Sub

    ''' <summary>
    ''' 开始下载一个自定义文件。
    ''' </summary>
    Public Shared Sub StartCustomDownload(Url As String, FileName As String, Optional Folder As String = Nothing)
        Try
            If String.IsNullOrWhiteSpace(Folder) Then
                Folder = Dialogs.SaveFile("选择文件保存位置", FileName)
                If Folder Is Nothing Then Return
                FileName = PathUtils.GetLastPart(Folder)
                Folder = PathUtils.RemoveLastPart(Folder)
            End If
            Folder = PathUtils.AddSlashSuffix(Folder.Replace("/", "\"))
            Try
                DirectoryUtils.Create(Folder)
                CheckPermissionWithException(Folder)
            Catch ex As Exception
                Logger.Error(ex, $"访问文件夹失败（{Folder}）")
                Return
            End Try
            Logger.Info($"自定义下载文件名：{FileName}")
            Logger.Info($"自定义下载文件目标：{Folder}")
            Dim Uuid As Integer = GetUuid()
            Dim FileLoader As New ModNet.LoaderDownload($"自定义下载文件：{FileName} ", New List(Of ModNet.NetFile) From {
                New ModNet.NetFile({Url}, Folder & FileName, Nothing, True)
            })
            Dim Loader As New ModLoader.LoaderCombo(Of Integer)($"自定义下载 ({Uuid}) ", {CType(FileLoader, ModLoader.LoaderBase)})
            Loader.OnStateChanged = Sub(L) DownloadState(CType(L, ModLoader.LoaderCombo(Of Integer)))
            Loader.Start()
            ModLoader.LoaderTaskbarAdd(Loader)
            ModMain.FrmMain.BtnExtraDownload.ShowRefresh()
            ModMain.FrmMain.BtnExtraDownload.Ribble()
        Catch ex As Exception
            Logger.Error(ex, "开始自定义下载失败")
        End Try
    End Sub

    Private Sub BtnDownloadSelect_Click(sender As Object, e As MouseButtonEventArgs) Handles BtnDownloadSelect.Click
        Dim Folder = Dialogs.SelectFolder("选择目标文件夹", False).FirstOrDefault()
        If Folder IsNot Nothing Then TextDownloadFolder.Text = Folder
    End Sub

    Private Sub BtnDownloadOpen_Click(sender As Object, e As MouseButtonEventArgs) Handles BtnDownloadOpen.Click
        Try
            Dim Folder = TextDownloadFolder.Text
            DirectoryUtils.Create(Folder)
            StartProcess(Folder)
        Catch ex As Exception
            Logger.Warn(ex, "打开下载文件夹失败")
        End Try
    End Sub

    Private Shared Sub DownloadState(Loader As ModLoader.LoaderCombo(Of Integer))
        Try
            Select Case Loader.State
                Case LoadState.Finished
                    Hint(Loader.Name & "完成！", HintType.Green)
                    Interaction.Beep()
                Case LoadState.Failed
                    Logger.Error(Loader.Error, $"{Loader.Name}失败")
                    Interaction.Beep()
                Case LoadState.Canceled
                    Hint(Loader.Name & "已取消！")
                    Interaction.Beep()
            End Select
        Catch ex As Exception
            Logger.Error(ex, "处理自定义下载状态失败")
        End Try
    End Sub

#End Region

#Region "百宝箱"

    Private Sub BtnJrrp_Click(sender As Object, e As MouseButtonEventArgs) Handles BtnJrrp.Click
        Jrrp()
    End Sub

    Private Sub BtnMemory_Click(sender As Object, e As MouseButtonEventArgs) Handles BtnMemory.Click
        RunInThread(Sub() MemoryOptimize(True))
    End Sub

    Private Sub BtnClear_Click(sender As Object, e As MouseButtonEventArgs) Handles BtnClear.Click
        RubbishClear()
    End Sub

    ''' <summary>
    ''' 今日人品。
    ''' </summary>
    Public Shared Sub Jrrp()
        Try
            Dim Seed1 As String = $"asdfgbn{Date.Now.DayOfYear}12#3$45{Date.Now.Year}IUY"
            Dim Seed2 As String = $"QWERTY{Identify}0*8&6{Date.Now.Day}kjhg"
            Dim Num As Integer = CInt(Math.Round(Math.Abs((Seed1.GetStableHashCode() / 3.0 + Seed2.GetStableHashCode() / 3.0) / 527.0) Mod 1001.0))
            Dim Score As Integer = If(Num < 970, CInt(Math.Round(Num / 969.0 * 99.0)), 100)
            Dim Text As String
            Select Case True
                Case Score = 100
                    Text = "！100！100！！！！！" & If(ModSecret.ThemeUnlock(13, False), vbCrLf & "隐藏主题 欧皇彩 已解锁！", "")
                Case Score >= 98
                    Text = "！差点就到 100 了呢……"
                Case Score >= 90
                    Text = "！好评如潮！"
                Case Score >= 65
                    Text = "！今天运气不错呢！"
                Case Score > 50
                    Text = "！还行啦，还行啦。"
                Case Score = 50
                    Text = "！五五开……"
                Case Score >= 40
                    Text = "！勉强还行吧……？"
                Case Score >= 20
                    Text = "！呜……"
                Case Score >= 11
                    Text = "？！不会吧……"
                Case Score >= 1
                    Text = "……（是百分制哦）"
                Case Else
                    Text = "？！"
                    Dim Result = MyMsgBox("在查看结果前，请先同意以下附加使用条款：" & vbCrLf & vbCrLf &
                                          "1. 我知晓并了解 XingYue Launcher 的今日人品功能完全没有出 Bug。" & vbCrLf &
                                          "2. XingYue Launcher 不对使用本软件所间接造成的一切财产损失（如砸电脑等）等负责。",
                                          "今日人品 - 附加使用条款", "同意并查看结果", "再见", "", IsWarn:=True)
                    If Result = 2 Then Return
            End Select
            Dim IsTimeChanged As Boolean = ModMain.FrmMain.IsSystemTimeChanged
            MyMsgBox(If(IsTimeChanged, "在这个时候，你的人品值会是：", "你今天的人品值是：") & Score & Text,
                     If(IsTimeChanged, "今日人品? - ", "今日人品 - ") & Date.Now.ToString("yyyy'/'M'/'d"),
                     "我知道了", "", "", Score < 20)
        Catch ex As Exception
            Logger.Error(ex, "计算今日人品失败")
        End Try
    End Sub

    ''' <summary>
    ''' 清理游戏垃圾。
    ''' </summary>
    Public Shared Sub RubbishClear()
        RunInUi(Sub()
                    If ModMain.FrmOtherTest IsNot Nothing AndAlso ModMain.FrmOtherTest.BtnClear IsNot Nothing AndAlso ModMain.FrmOtherTest.BtnClear.IsLoaded Then
                        ModMain.FrmOtherTest.BtnClear.IsEnabled = False
                    End If
                End Sub)
        RunInNewThread(Sub()
                           Try
                               If ModWatcher.HasRunningMinecraft OrElse ModLaunch.McLaunchLoader.State = LoadState.Loading Then
                                   Hint("请先关闭所有运行中的游戏！")
                               ElseIf HasDownloadingTask() Then
                                   Hint("请在所有下载任务完成后再来清理吧！")
                               Else
                                   If Not ModMinecraft.McFolderList.Any() Then ModMinecraft.McFolderListLoader.Start()
                                   If PathUtils.IsSame(PathTemp, Path.GetTempPath() & "XingYue\") Then
                                       If Settings.Get(Of Integer)("HintClearRubbish") <= 2 Then
                                           If MyMsgBox("即将清理游戏日志、错误报告、缓存等文件。" & vbCrLf &
                                                       "虽然应该没人往这些地方放重要文件，但还是问一下，是否确认继续？" & vbCrLf & vbCrLf &
                                                       "在完成清理后，需要重启 XingYue Launcher。", "清理确认", "确定", "取消") = 2 Then Return
                                           Settings.Set("HintClearRubbish", Settings.Get(Of Integer)("HintClearRubbish") + 1)
                                       End If
                                   ElseIf MyMsgBox("即将清理游戏日志、错误报告、缓存等文件。" & vbCrLf & vbCrLf &
                                                   "你已将缓存文件夹手动修改为：" & PathTemp & vbCrLf &
                                                   "清理过程中，将删除该文件夹中的所有内容，且无法恢复。请确认其中没有除了 XingYue Launcher 缓存以外的重要文件！" & vbCrLf & vbCrLf &
                                                   "在完成清理后，需要重启 XingYue Launcher。", "清理确认", "确定", "取消") = 2 Then
                                       Return
                                   End If
                                   Dim Folders As New List(Of String)
                                   If Not ModMinecraft.McFolderList.Any() Then ModMinecraft.McFolderListLoader.WaitForExit()
                                   For Each Folder In ModMinecraft.McFolderList
                                       Folders.Add(Folder.Location)
                                       Folders.AddRange(DirectoryUtils.EnumerateDirectories(Folder.Location & "versions"))
                                   Next
                                   For Each Folder In Folders
                                       ClearFolder(Path.Combine(Folder, ".mixin.out"))
                                       ClearFolder(Path.Combine(Folder, "crash-reports"))
                                       ClearFolder(Path.Combine(Folder, "debug"))
                                       ClearFolder(Path.Combine(Folder, "logs"))
                                       For Each FilePath In DirectoryUtils.EnumerateFiles(Folder)
                                           Dim Name = PathUtils.GetLastPart(FilePath)
                                           If Name.StartsWithF("hs_err_pid") OrElse PathUtils.GetExtension(Name) = "log" OrElse Name = "WailaErrorOutput.txt" Then
                                               FileUtils.Delete(FilePath)
                                           End If
                                       Next
                                       For Each SubFolder In DirectoryUtils.EnumerateDirectories(Folder)
                                           Dim Name = PathUtils.GetLastPart(SubFolder)
                                           If Name = PathUtils.GetLastPart(Folder) & "-natives" OrElse Name = "natives-windows-x86_64" Then
                                               ClearFolder(SubFolder)
                                           End If
                                       Next
                                   Next
                                   ClearFolder(PathTemp)
                                   ClearFolder(OsDrive & "ProgramData\XingYue\")
                                   If MyMsgBox("垃圾已经清理好啦！", "清理完成", "关闭 XingYue Launcher", "重启 XingYue Launcher", "", IsWarn:=False, HighLight:=True, ForceWait:=True) = 2 Then
                                       StartProcess(New ProcessStartInfo(PathExe, "--wait"))
                                   End If
                                   FormMain.EndProgramForce()
                               End If
                           Catch ex As Exception
                               Logger.Error(ex, "清理垃圾失败")
                           Finally
                               RunInUiWait(Sub()
                                               If ModMain.FrmOtherTest IsNot Nothing AndAlso ModMain.FrmOtherTest.BtnClear IsNot Nothing AndAlso ModMain.FrmOtherTest.BtnClear.IsLoaded Then
                                                   ModMain.FrmOtherTest.BtnClear.IsEnabled = True
                                               End If
                                           End Sub)
                           End Try
                       End Sub, "Rubbish Clear")
    End Sub

    Private Shared Sub ClearFolder(Folder As String)
        Try
            If DirectoryUtils.Exists(Folder) Then DirectoryUtils.Delete(Folder)
        Catch ex As Exception
            Logger.Warn(ex, $"清理文件夹失败（{Folder}）")
        End Try
    End Sub

    ''' <summary>
    ''' 内存优化。如果 ShowHint 为 False，则不显示任何提示。
    ''' </summary>
    Public Shared Sub MemoryOptimize(ShowHint As Boolean)
        If IsMemoryOptimizeRunning Then
            If ShowHint Then Hint("内存优化尚未结束，请稍等！")
            Return
        End If
        IsMemoryOptimizeRunning = True
        If ShowHint Then Hint("正在进行内存优化……")
        Dim Changed As Long
        Try
            If WindowsUtils.HasAdminRole() Then
                Dim Before As Long = CLng(My.Computer.Info.AvailablePhysicalMemory)
                Try
                    MemoryOptimizeInternal(ShowHint)
                Catch ex As Exception
                    Logger.Warn(ex, "内存优化失败", If(ShowHint, LogBehavior.Toast, LogBehavior.ToastIfDebug))
                    Return
                Finally
                    IsMemoryOptimizeRunning = False
                End Try
                Changed = CLng(My.Computer.Info.AvailablePhysicalMemory) - Before
            Else
                Logger.Info("没有管理员权限，将以命令行方式进行内存优化")
                Try
                    Changed = CLng(RunAsAdmin("--memory")) * 1024L
                Catch ex As Exception
                    Logger.Warn(ex, "命令行形式内存优化失败")
                    If ShowHint Then Hint("获取管理员权限失败，请尝试右键 XingYue Launcher，选择 以管理员身份运行！", HintType.Red)
                    Return
                Finally
                    IsMemoryOptimizeRunning = False
                End Try
                If Changed < 0 Then Return
            End If
            Dim Available As String = StringUtils.FormatByteSize(CLng(My.Computer.Info.AvailablePhysicalMemory))
            Logger.Info($"内存优化完成，可用内存改变量：{StringUtils.FormatByteSize(Changed)}，大致剩余内存：{Available}")
            If Changed > 0 Then
                If ShowHint Then Hint($"内存优化完成，可用内存增加了 {StringUtils.FormatByteSize(CLng(Math.Round(Changed * 0.8)))}，目前剩余内存 {Available}！", HintType.Green)
            ElseIf ShowHint Then
                Hint($"内存优化完成，已经优化到了最佳状态，目前剩余内存 {Available}！")
            End If
        Finally
            IsMemoryOptimizeRunning = False
        End Try
    End Sub

    ''' <summary>
    ''' 实际执行内存优化的操作，需要管理员权限。
    ''' </summary>
    Public Shared Sub MemoryOptimizeInternal(ShowHint As Boolean)
        If Not WindowsUtils.HasAdminRole() Then
            Throw New Exception("内存优化功能需要管理员权限！" & vbCrLf &
                                "如果需要自动以管理员身份启动 XingYue Launcher，可以右键 XingYue Launcher，打开 属性 → 兼容性 → 以管理员身份运行此程序。")
        End If
        Logger.Info("获取内存优化权限")
        Using Identify As WindowsIdentity = WindowsIdentity.GetCurrent(TokenAccessLevels.Query Or TokenAccessLevels.AdjustPrivileges)
            Dim NewState As PrivilegeToken
            NewState.PrivilegeCount = 1
            NewState.Luid = 0
            NewState.Attributes = 2
            If Not LookupPrivilegeValue(Nothing, "SeProfileSingleProcessPrivilege", NewState.Luid) Then
                Throw New Exception($"获取内存优化权限失败（错误代码：{Marshal.GetLastWin32Error()}）")
            End If
            Dim PreviousState As IntPtr = IntPtr.Zero
            Dim ReturnLength As Integer = 0
            If Not AdjustTokenPrivileges(Identify.Token, False, NewState, Marshal.SizeOf(NewState), PreviousState, ReturnLength) _
                OrElse Marshal.GetLastWin32Error() <> 0 Then
                Throw New Exception($"获取内存优化权限失败（错误代码：{Marshal.GetLastWin32Error()}）")
            End If
        End Using
        For Num As Integer = 2 To 4
            Logger.Info($"内存优化操作 {Num} 开始")
            Dim Ptr As IntPtr = Marshal.AllocHGlobal(4)
            Try
                Marshal.WriteInt32(Ptr, Num)
                Dim Result As UInteger = NtSetSystemInformation(80, Ptr, 4)
                If Result <> 0UI Then Throw New Exception($"内存优化操作 {Num} 失败（错误代码：{Result}）")
            Finally
                Marshal.FreeHGlobal(Ptr)
            End Try
        Next
    End Sub

    <StructLayout(LayoutKind.Sequential, Pack:=1)>
    Private Structure PrivilegeToken
        Public PrivilegeCount As Integer
        Public Luid As Long
        Public Attributes As Integer
    End Structure

    <DllImport("ntdll.dll", CharSet:=CharSet.Ansi, ExactSpelling:=True, SetLastError:=True)>
    Private Shared Function NtSetSystemInformation(SystemInformationClass As Integer, SystemInformation As IntPtr, SystemInformationLength As Integer) As UInteger
    End Function

    <DllImport("advapi32.dll", EntryPoint:="LookupPrivilegeValueA", CharSet:=CharSet.Ansi, ExactSpelling:=True, SetLastError:=True)>
    Private Shared Function LookupPrivilegeValue(lpSystemName As String, lpName As String, ByRef lpLuid As Long) As Boolean
    End Function

    <DllImport("advapi32.dll", CharSet:=CharSet.Ansi, ExactSpelling:=True, SetLastError:=True)>
    Private Shared Function AdjustTokenPrivileges(TokenHandle As IntPtr, DisableAllPrivileges As Boolean, ByRef NewState As PrivilegeToken, BufferLength As Integer, ByRef PreviousState As IntPtr, ByRef ReturnLength As Integer) As Boolean
    End Function

    ''' <summary>
    ''' 千万别点。
    ''' </summary>
    Private Sub BtnClick_Click(sender As Object, e As MouseButtonEventArgs) Handles BtnClick.Click
        Try
            Dim Tasks As New List(Of Integer) From {RandomInteger(0, 7)}
            If RandomInteger(0, 1) = 1 Then Tasks.Add(RandomInteger(1, 6))
            If Date.Now.Month = 4 AndAlso Date.Now.Day = 1 Then Tasks = New List(Of Integer) From {7}
            If Tasks.Contains(1) Then
                '癫痫警告
                If MyMsgBox("当暴露在点击确定后的场景时，有极小部分人群会引发癲痫。这种情形可能是由于某些未查出的癫病症状引起，即使该人员并没有患癫痫病史也有可能造成此类病症。" &
                            "如果您的家人或任何家庭成员曾有过类似症状，请在点击确定前咨询您的医生或医师。" &
                            "如果您在稍后出现任何症状，包括头晕、目眩、眼部或肌肉抽搐、失去意识、失去方向感、抽搐或出现任何自己无法控制的动作，请立即关闭 XingYue Launcher 并咨询您的医生或医师。" &
                            vbCrLf & "这是最后的警告，是否继续操作？", "警告", "确定", "取消", "", IsWarn:=True) = 2 Then
                    Return
                End If
            Else
                MyMsgBox("XingYue Launcher 作者不会受理由于点击千万别点造成的任何 Bug。" & vbCrLf & "这是最后的警告，是否继续操作？", "警告", "确定", "确定", "确定", IsWarn:=True)
            End If
            For Each Task In Tasks
                Select Case Task
                    Case 0
                        '什么都不做
                    Case 1
                        ModSecret.ThemeDontClick = 2
                        ModSecret.ThemeRefresh()
                    Case 2
                        '镜像翻转
                        If PanBack.LayoutTransform IsNot Nothing Then PanBack.RenderTransformOrigin = New Point(1.25, 1.25)
                        Select Case RandomInteger(0, 2)
                            Case 0
                                PanBack.RenderTransform = New ScaleTransform(1, -1)
                            Case 1
                                PanBack.RenderTransform = New ScaleTransform(-1, -1)
                            Case 2
                                PanBack.RenderTransform = New ScaleTransform(-1, 1)
                        End Select
                    Case 3
                        '放大到全屏
                        ModMain.FrmMain.IsSizeSaveable = False
                        PanBack.LayoutTransform = New ScaleTransform(2.5, 2.5)
                        ModMain.FrmMain.Height = My.Computer.Screen.WorkingArea.Height - 200
                        ModMain.FrmMain.Width = My.Computer.Screen.WorkingArea.Width - 200
                        ModMain.FrmMain.Left = 0
                        ModMain.FrmMain.Top = 0
                        AniStop("Don't Click Scale")
                    Case 4
                        '缩小
                        ModMain.FrmMain.IsSizeSaveable = False
                        ModMain.FrmMain.RenderTransform = New ScaleTransform()
                        AniStart(AaScaleTransform(ModMain.FrmMain, -0.85, 5000, 0, New AniEaseOutFluent(AniEasePower.ExtraStrong)), "Don't Click Scale")
                        AniStop("Don't Click Rotate")
                    Case 5
                        '旋转
                        ModMain.FrmMain.RenderTransform = New RotateTransform()
                        AniStart(AaRotateTransform(ModMain.FrmMain, 1000000 * RandomOne({1, -1}), 10000000), "Don't Click Rotate")
                        AniStop("Don't Click Scale")
                    Case 6
                        '到处乱跑
                        ModMain.FrmMain.IsSizeSaveable = False
                        If RandomInteger(0, 1) <> 0 Then
                            AniStart(AaX(ModMain.FrmMain, 10000000 * RandomOne({1, -1}), 30000000), "Don't Click Move X")
                        Else
                            AniStart(AaY(ModMain.FrmMain, 10000000 * RandomOne({1, -1}), 50000000), "Don't Click Move Y")
                        End If
                        RunInThread(Sub()
                                        Do
                                            RunInUi(Sub()
                                                        If ModMain.FrmMain.Top < -ModMain.FrmMain.Height Then ModMain.FrmMain.Top = My.Computer.Screen.Bounds.Height + 49
                                                        If ModMain.FrmMain.Top > My.Computer.Screen.Bounds.Height + 50 Then ModMain.FrmMain.Top = -ModMain.FrmMain.Height + 1
                                                        If ModMain.FrmMain.Left < -ModMain.FrmMain.Width Then ModMain.FrmMain.Left = My.Computer.Screen.Bounds.Width + 49
                                                        If ModMain.FrmMain.Left > My.Computer.Screen.Bounds.Width + 50 Then ModMain.FrmMain.Left = -ModMain.FrmMain.Width + 1
                                                    End Sub)
                                            Thread.Sleep(10)
                                        Loop
                                    End Sub)
                    Case 7
                        Hint("……好像什么也没发生？", HintType.Blue)
                End Select
            Next
            BtnClick.Visibility = Visibility.Collapsed
        Catch ex As Exception
            Logger.Error(ex, "千万别点失败")
        End Try
    End Sub

#End Region

#Region "回声洞"

    Private Sub CardCave_MouseLeftButtonUp(sender As Object, e As MouseButtonEventArgs) Handles CardCave.MouseLeftButtonUp
        If Not IsCaveEnabled Then Return
        IsCaveEnabled = False
        Dim OldText As String = LabCave.Text
        LabCave.Text = GetRandomCave()
        AniStart({
            AaOpacity(LabCave, -1, 5),
            AaOpacity(LabCave, 1, 5, 30),
            AaOpacity(LabCave, -1, 5, 70),
            AaOpacity(LabCave, 0.9, 5, 85),
            AaOpacity(LabCave, -1, 5, 125),
            AaOpacity(LabCave, 0.8, 5, 145),
            AaOpacity(LabCave, -1, 5, 165),
            AaOpacity(LabCave, 0.7, 5, 195),
            AaOpacity(LabCave, -1, 5, 210),
            AaOpacity(LabCave, 0.5, 5, 235),
            AaOpacity(LabCave, -1, 5, 250),
            AaOpacity(LabCave, 1, 1, 400, Nothing, True),
            AaTextAppear(LabCave, Hide:=False, TimePerText:=True, Time:=60, Delay:=400),
            AaCode(Sub() IsCaveEnabled = True, 0, True)
        }, "Cave")
        LabCave.Text = OldText
    End Sub

    ''' <summary>
    ''' 随机获取一条回声洞留言。
    ''' </summary>
    Public Shared Function GetRandomCave() As String
        Dim Result As String = RandomOne(New String() {
            RandomOne(PageOtherTestData.CaveGroup0),
            RandomOne(PageOtherTestData.CaveGroup1),
            RandomOne(PageOtherTestData.CaveGroup2),
            RandomOne(PageOtherTestData.CaveGroup3),
            RandomOne(PageOtherTestData.CaveGroup4),
            RandomOne(PageOtherTestData.CaveGroup5),
            RandomOne(PageOtherTestData.CaveGroup6),
            RandomOne(New String() {RandomOne(PageOtherTestData.CaveRiddles), GetRandomPresetHint()})
        })
        Return Result.Replace("\n", vbCrLf)
    End Function

    ''' <summary>
    ''' 随机获取一条「你知道吗」提示。优先使用自定义的 hints.txt。
    ''' </summary>
    Public Shared Function GetRandomHint() As String
        Try
            Dim HintFile As String = Paths.Base & "XingYue\hints.txt"
            If FileUtils.Exists(HintFile) Then
                Dim Lines As New List(Of String)
                For Each Line In FileUtils.ReadAsLines(HintFile)
                    If Not String.IsNullOrWhiteSpace(Line) Then Lines.Add(Line)
                Next
                If Lines.Any() Then Return RandomOne(Lines)
            End If
        Catch ex As Exception
            Logger.Error(ex, "获取自定义 你知道吗 提示失败")
        End Try
        Return GetRandomPresetHint()
    End Function

    ''' <summary>
    ''' 随机获取一条预设的「你知道吗」提示。
    ''' </summary>
    Public Shared Function GetRandomPresetHint() As String
        Return RandomOne(PageOtherTestData.PresetHints)
    End Function

#End Region

End Class
