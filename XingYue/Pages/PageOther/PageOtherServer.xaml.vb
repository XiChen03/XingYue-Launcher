Imports System.IO
Imports System.Windows.Forms

Public Class PageOtherServer

    Private _Initialized As Boolean = False
    Private _UptimeTimer As Windows.Threading.DispatcherTimer
    Private _TaskTimer As Windows.Threading.DispatcherTimer

    '当前功能页
    Private Enum ServerTab
        Console = 0
        Settings = 1
        Properties = 2
        Backups = 3
        Plugins = 4
        Files = 5
        Players = 6
        Tasks = 7
        Download = 8
    End Enum
    Private _CurrentTab As ServerTab = ServerTab.Console
    Private _TabButtons As New List(Of MyButton)

    '设置页控件
    Private _TextName As MyTextBox
    Private _TextDir As MyTextBox
    Private _ComboMode As MyComboBox
    Private _ComboJava As MyComboBox
    Private _TextArgs As MyTextBox
    Private _TextCore As MyTextBox
    Private _TextMinMem As MyTextBox
    Private _TextMaxMem As MyTextBox
    Private _TextStartCommand As MyTextBox
    Private _PanSettingsCommand As StackPanel

    '属性页控件
    Private _PropertyControls As New Dictionary(Of String, FrameworkElement)(StringComparer.OrdinalIgnoreCase)

    '文件页
    Private _FileRelative As String = ""
    Private _PluginIsMod As Boolean = False

    '===============================
    '  生命周期
    '===============================

    Private Sub PageEnterHandler() Handles Me.PageEnter
        Try
            ServerManager.ReadConfig()
            ServerManager.OnLog = Sub(Line As String) RunInUi(Sub() AppendLog(Line))
            ServerManager.OnStateChanged = Sub() RunInUi(Sub() RefreshState())
            BuildTabs()
            If Not _Initialized Then
                _Initialized = True
                _UptimeTimer = New Windows.Threading.DispatcherTimer With {.Interval = New TimeSpan(0, 0, 1)}
                AddHandler _UptimeTimer.Tick, Sub() If IsVisible AndAlso ServerManager.IsRunning() Then RefreshOverview()
                _UptimeTimer.Start()
                '定时任务：每分钟检查一次是否到点
                _TaskTimer = New Windows.Threading.DispatcherTimer With {.Interval = New TimeSpan(0, 1, 0)}
                AddHandler _TaskTimer.Tick, Sub() Threading.Tasks.Task.Run(Sub() ServerManager.RunDueTasks())
                _TaskTimer.Start()
            End If
            SwitchTab(_CurrentTab)
            RefreshState()
        Catch ex As Exception
            Logger.Error(ex, "刷新开服器页面失败")
            Hint("刷新开服器页面失败：" & ex.Message, HintType.Red, False)
        End Try
    End Sub

    '===============================
    '  页签
    '===============================

    Private Sub BuildTabs()
        If _TabButtons.Count > 0 Then Return
        Dim Names As String() = {"控制台", "实例设置", "服务器属性", "备份管理", "插件/模组", "文件管理", "玩家管理", "定时任务", "下载核心"}
        For i As Integer = 0 To Names.Length - 1
            Dim Index As Integer = i
            Dim Btn As New MyButton With {.Text = Names(i), .MinWidth = 0, .Padding = New Thickness(6, 7, 6, 7), .HorizontalAlignment = HorizontalAlignment.Stretch}
            Btn.Margin = New Thickness(0, 0, 8, 8)
            AddHandler Btn.Click, Sub() SwitchTab(CType(Index, ServerTab))
            PanTabs.Children.Add(Btn)
            _TabButtons.Add(Btn)
        Next
    End Sub

    ''' <summary>供外部（例如自动化脚本）切换到指定功能页。</summary>
    Public Sub ShowTab(Index As Integer)
        If Index < 0 OrElse Index > 8 Then Return
        SwitchTab(CType(Index, ServerTab))
    End Sub

    Private Sub SwitchTab(Tab As ServerTab)
        _CurrentTab = Tab
        For i As Integer = 0 To _TabButtons.Count - 1
            _TabButtons(i).ColorType = If(i = CInt(Tab), MyButton.ColorState.Highlight, MyButton.ColorState.Normal)
        Next
        PanTabConsole.Visibility = If(Tab = ServerTab.Console, Visibility.Visible, Visibility.Collapsed)
        PanTabSettings.Visibility = If(Tab = ServerTab.Settings, Visibility.Visible, Visibility.Collapsed)
        PanTabProperties.Visibility = If(Tab = ServerTab.Properties, Visibility.Visible, Visibility.Collapsed)
        PanTabBackup.Visibility = If(Tab = ServerTab.Backups, Visibility.Visible, Visibility.Collapsed)
        PanTabPlugins.Visibility = If(Tab = ServerTab.Plugins, Visibility.Visible, Visibility.Collapsed)
        PanTabFiles.Visibility = If(Tab = ServerTab.Files, Visibility.Visible, Visibility.Collapsed)
        PanTabPlayers.Visibility = If(Tab = ServerTab.Players, Visibility.Visible, Visibility.Collapsed)
        PanTabTasks.Visibility = If(Tab = ServerTab.Tasks, Visibility.Visible, Visibility.Collapsed)
        PanTabDownload.Visibility = If(Tab = ServerTab.Download, Visibility.Visible, Visibility.Collapsed)

        '右侧的实例状态 / 实例概览 / 在线玩家只在控制台显示，其它页面让主卡片占满整行
        Dim ShowSide As Boolean = (Tab = ServerTab.Console)
        PanSide.Visibility = If(ShowSide, Visibility.Visible, Visibility.Collapsed)
        ColSide.Width = If(ShowSide, New GridLength(285), New GridLength(0))
        ColSideGap.Width = If(ShowSide, New GridLength(15), New GridLength(0))

        Select Case Tab
            Case ServerTab.Console : CardMain.Title = "服务器控制台"
            Case ServerTab.Settings : CardMain.Title = "实例设置" : BuildSettings()
            Case ServerTab.Properties : CardMain.Title = "服务器属性" : BuildProperties()
            Case ServerTab.Backups : CardMain.Title = "备份管理" : RefreshBackups()
            Case ServerTab.Plugins : CardMain.Title = "插件 / 模组" : RefreshPlugins()
            Case ServerTab.Files : CardMain.Title = "文件管理" : RefreshFiles()
            Case ServerTab.Players : CardMain.Title = "玩家管理" : RefreshPlayersList()
            Case ServerTab.Tasks : CardMain.Title = "定时任务" : RefreshTasks()
            Case ServerTab.Download : CardMain.Title = "下载核心" : BuildDownloadTab()
        End Select

        '切换页签后回到页面顶部，避免停留在上一页的滚动位置
        Try
            PanRoot.ScrollToTop()
        Catch
        End Try
    End Sub

    '===============================
    '  控制台
    '===============================

    Private Sub AppendLog(Line As String)
        If TextLog.LineCount > 3000 Then
            TextLog.Clear()
            TextLog.AppendText("（历史输出过多，已清空更早的内容）" & vbCrLf)
        End If
        TextLog.AppendText(Line & vbCrLf)
        ScrollLogToEnd()
    End Sub

    ''' <summary>把日志滚动到最新一行（日志框外层是 MyScrollViewer，自带启动器样式的滚动条）。</summary>
    Private Sub ScrollLogToEnd()
        Try
            If PanLogScroll Is Nothing Then Return
            TextLog.UpdateLayout()
            PanLogScroll.ScrollToVerticalOffset(PanLogScroll.ScrollableHeight)
        Catch
        End Try
    End Sub

    Private Sub BtnSend_Click(sender As Object, e As MouseButtonEventArgs)
        SendCurrentCommand()
    End Sub

    Private Sub TextCommand_KeyDown(sender As Object, e As Windows.Input.KeyEventArgs)
        If e.Key = Windows.Input.Key.Enter Then
            SendCurrentCommand()
            e.Handled = True
        End If
    End Sub

    Private Sub SendCurrentCommand()
        If Not ServerManager.IsRunning() Then
            Hint("服务器还没有运行。", HintType.Red, False)
            Return
        End If
        Dim Cmd As String = TextCommand.Text.Trim()
        If Cmd = "" Then Return
        TextCommand.Text = ""
        ServerManager.SendCommand(Cmd)
    End Sub

    '===============================
    '  状态 / 概览 / 玩家
    '===============================

    Private Sub RefreshState()
        Try
            Dim Running As Boolean = ServerManager.IsRunning()
            LabStatus.Text = If(Running, "运行中", "已停止")
            LabStatusIcon.Foreground = If(Running, New Media.SolidColorBrush(Media.Color.FromRgb(51, 177, 51)), FindResource("ColorBrushGray4"))
            BtnStart.IsEnabled = Not Running
            BtnStop.IsEnabled = Running
            BtnRestart.IsEnabled = Running
            BtnSend.IsEnabled = Running
            BtnSettingsSave.IsEnabled = Not Running
            RefreshOverview()
            RefreshPlayers()
        Catch ex As Exception
            Logger.Warn(ex, "刷新开服器状态失败")
        End Try
    End Sub

    Private Sub RefreshOverview()
        Try
            AniControlEnabled += 1
            PanOverview.Children.Clear()
            If ServerManager.Dir = "" Then
                AddOverviewRow("状态", "尚未选择服务端")
                Return
            End If
            AddOverviewRow("实例名称", If(ServerManager.Name = "", Path.GetFileName(ServerManager.Dir), ServerManager.Name))
            AddOverviewRow("服务端核心", If(ServerManager.Core = "", "未找到", If(ServerManager.Core.StartsWith("@"), "NeoForge/Forge 启动文件", ServerManager.Core)))
            AddOverviewRow("内存限制", $"{ServerManager.MinMemory} - {ServerManager.MaxMemory} MB")
            Dim Properties As Dictionary(Of String, String) = ServerManager.ReadProperties()
            AddOverviewRow("运行端口", GetProperty(Properties, "server-port", "25565"))
            AddOverviewRow("游戏难度", Translate(GetProperty(Properties, "difficulty", "-"), "peaceful", "简单", "easy", "简单", "normal", "普通", "hard", "困难"))
            AddOverviewRow("游戏模式", Translate(GetProperty(Properties, "gamemode", "-"), "survival", "生存", "creative", "创造", "adventure", "冒险", "spectator", "旁观"))
            AddOverviewRow("游戏地图", GetProperty(Properties, "level-name", "world"))
            AddOverviewRow("正版验证", If(GetProperty(Properties, "online-mode", "true") = "true", "开启", "关闭"))
            AddOverviewRow("运行时长", If(ServerManager.IsRunning(), FormatUptime(ServerManager.Uptime), "—"))
        Finally
            AniControlEnabled -= 1
        End Try
    End Sub

    ''' <summary>把字节数格式化成 KB / MB 文本。</summary>
    Private Shared Function FormatFileSize(Bytes As Long) As String
        If Bytes < 1024 Then Return Bytes & " B"
        If Bytes < 1024L * 1024 Then Return (Bytes / 1024.0).ToString("F1") & " KB"
        If Bytes < 1024L * 1024 * 1024 Then Return (Bytes / 1024.0 / 1024.0).ToString("F1") & " MB"
        Return (Bytes / 1024.0 / 1024.0 / 1024.0).ToString("F2") & " GB"
    End Function

    Private Shared Function FormatUptime(Span As TimeSpan) As String
        If Span.TotalDays >= 1 Then Return $"{Int(Span.TotalDays)} 天 {Span.Hours:00}:{Span.Minutes:00}:{Span.Seconds:00}"
        Return $"{Span.Hours:00}:{Span.Minutes:00}:{Span.Seconds:00}"
    End Function

    Private Shared Function GetProperty(Properties As Dictionary(Of String, String), Key As String, DefaultValue As String) As String
        Dim Value As String = Nothing
        If Properties.TryGetValue(Key, Value) AndAlso Value <> "" Then Return Value
        Return DefaultValue
    End Function

    ''' <summary>把值按键值对翻译成中文（Pairs 为 原值, 译文 交替排列）。</summary>
    Private Shared Function Translate(Value As String, ParamArray Pairs As String()) As String
        Dim i As Integer = 0
        While i + 1 < Pairs.Length
            If String.Equals(Value, Pairs(i), StringComparison.OrdinalIgnoreCase) Then Return Pairs(i + 1)
            i += 2
        End While
        Return Value
    End Function

    Private Sub AddOverviewRow(Label As String, Value As String)
        Dim Grid As New Grid With {.Margin = New Thickness(0, 0, 0, 8)}
        Grid.ColumnDefinitions.Add(New ColumnDefinition With {.Width = New GridLength(76)})
        Grid.ColumnDefinitions.Add(New ColumnDefinition With {.Width = New GridLength(1, GridUnitType.Star)})
        Dim Lab As New TextBlock With {.Text = Label, .FontSize = 12.5, .VerticalAlignment = VerticalAlignment.Center}
        Lab.SetResourceReference(TextBlock.ForegroundProperty, "ColorBrushGray3")
        Dim Val As New TextBlock With {.Text = Value, .FontSize = 12.5, .HorizontalAlignment = HorizontalAlignment.Right, .TextTrimming = TextTrimming.CharacterEllipsis}
        Val.SetResourceReference(TextBlock.ForegroundProperty, "ColorBrush1")
        Grid.SetColumn(Val, 1)
        Grid.Children.Add(Lab)
        Grid.Children.Add(Val)
        PanOverview.Children.Add(Grid)
    End Sub

    Private Sub RefreshPlayers()
        Try
            AniControlEnabled += 1
            PanPlayers.Children.Clear()
            Dim Names As List(Of String) = ServerManager.Players
            If Names Is Nothing OrElse Names.Count = 0 Then
                Dim Empty As New TextBlock With {.Text = "当前无人在线", .FontSize = 12.5}
                Empty.SetResourceReference(TextBlock.ForegroundProperty, "ColorBrushGray4")
                PanPlayers.Children.Add(Empty)
                Return
            End If
            For Each PlayerName As String In Names
                PanPlayers.Children.Add(BuildPlayerRow(PlayerName))
            Next
        Finally
            AniControlEnabled -= 1
        End Try
    End Sub

    ''' <summary>在线玩家一行，右键可以给 OP / 撤销 OP / 踢出 / 封禁。</summary>
    Private Function BuildPlayerRow(PlayerName As String) As FrameworkElement
        Dim Row As New Border With {
            .Padding = New Thickness(6, 4, 6, 4),
            .Margin = New Thickness(0, 0, 0, 2),
            .CornerRadius = New CornerRadius(4),
            .Background = Windows.Media.Brushes.Transparent
        }
        Dim Lab As New TextBlock With {
            .Text = PlayerName,
            .FontSize = 13,
            .VerticalAlignment = VerticalAlignment.Center
        }
        Lab.SetResourceReference(TextBlock.ForegroundProperty, "ColorBrush1")
        Row.Child = Lab

        Dim Menu As New System.Windows.Controls.ContextMenu()
        Menu.Items.Add(NewContextMenuItem("给予 OP", Sub() PlayerAdminAction("op", PlayerName)))
        Menu.Items.Add(NewContextMenuItem("撤销 OP", Sub() PlayerAdminAction("deop", PlayerName)))
        Menu.Items.Add(NewContextMenuItem("踢出此人", Sub() PlayerAdminAction("kick", PlayerName)))
        Menu.Items.Add(NewContextMenuItem("封禁此人", Sub() PlayerAdminAction("ban", PlayerName)))
        Row.ContextMenu = Menu

        '鼠标移上去时给个底色，提示这行可以右键
        AddHandler Row.MouseEnter, Sub() Row.SetResourceReference(Border.BackgroundProperty, "ColorBrush6")
        AddHandler Row.MouseLeave, Sub() Row.SetResourceReference(Border.BackgroundProperty, "ColorBrushTransparent")
        Return Row
    End Function

    Private Function NewContextMenuItem(Header As String, Action As Action) As MyMenuItem
        Dim Item As New MyMenuItem With {.Header = Header, .Padding = New Thickness(0, 2, 0, 2)}
        Item.AddHandler(System.Windows.Controls.MenuItem.ClickEvent, New RoutedEventHandler(Sub(sender, e) Action()))
        Return Item
    End Function

    ''' <summary>对在线玩家执行管理指令。</summary>
    Private Sub PlayerAdminAction(Command As String, PlayerName As String)
        If Not ServerManager.IsRunning() Then
            Hint("服务端没有运行，没办法执行指令", HintType.Red, False)
            Return
        End If
        Try
            ServerManager.SendCommand(Command & " " & PlayerName)
            Select Case Command
                Case "op" : Hint($"已经给 {PlayerName} 管理员权限", HintType.Green, False)
                Case "deop" : Hint($"已经取消 {PlayerName} 的管理员权限", HintType.Green, False)
                Case "kick" : Hint($"已经把 {PlayerName} 踢出服务器", HintType.Green, False)
                Case "ban" : Hint($"已经封禁 {PlayerName}", HintType.Green, False)
            End Select
        Catch ex As Exception
            Hint("执行失败：" & ex.Message, HintType.Red, False)
        End Try
    End Sub

    '===============================
    '  实例设置
    '===============================

    Private Sub BuildSettings()
        If PanSettingsHost.Children.Count > 0 Then
            '已构建过，只刷新内容
            _TextName.Text = ServerManager.Name
            _TextDir.Text = ServerManager.Dir
            _TextArgs.Text = ServerManager.JvmArgs
            _TextCore.Text = ServerManager.Core
            _TextMinMem.Text = ServerManager.MinMemory.ToString()
            _TextMaxMem.Text = ServerManager.MaxMemory.ToString()
            _TextStartCommand.Text = ServerManager.CustomCommand
            RefreshSettingsState()
            Return
        End If

        _TextName = AddSettingsText("服务器名称", "面板里显示的别名，可以是中文")
        _TextDir = AddSettingsText("实例路径", "服务端文件夹的完整路径", AddressOf BrowseDir)
        _ComboMode = AddSettingsCombo("启动方式", {"使用 Java 启动（Minecraft 服务端）", "自定义启动命令"}, AddressOf ModeChanged)
        _ComboJava = AddSettingsCombo("Java 版本", {"自动检测"}, Nothing)
        _TextArgs = AddSettingsText("启动参数（JVM Args）", "例如 -XX:+UseG1GC，无特殊需求请留空")
        _TextCore = AddSettingsText("服务端核心文件", "jar 文件名，或 NeoForge/Forge 的 @libraries/... 启动文件", AddressOf BrowseCore)
        _TextMinMem = AddSettingsText("最小内存（MB）", "例如 1024")
        _TextMaxMem = AddSettingsText("最大内存（MB）", "例如 4096")
        _TextStartCommand = AddSettingsText("自定义启动命令", "例如 bedrock_server.exe，工作目录为实例路径")

        _PanSettingsCommand = New StackPanel
        Dim Hint2 As New TextBlock With {.Text = "提示：更改路径或核心后记得点下面的「保存设置」。", .FontSize = 12.5, .Margin = New Thickness(0, 6, 0, 0), .TextWrapping = TextWrapping.Wrap}
        Hint2.SetResourceReference(TextBlock.ForegroundProperty, "ColorBrushGray4")
        PanSettingsHost.Children.Add(Hint2)

        _TextName.Text = ServerManager.Name
        _TextDir.Text = ServerManager.Dir
        _TextArgs.Text = ServerManager.JvmArgs
        _TextCore.Text = ServerManager.Core
        _TextMinMem.Text = ServerManager.MinMemory.ToString()
        _TextMaxMem.Text = ServerManager.MaxMemory.ToString()
        _TextStartCommand.Text = ServerManager.CustomCommand
        RefreshJavaList()
        RefreshSettingsState()
    End Sub

    Private Sub RefreshSettingsState()
        Dim IsCustom As Boolean = _ComboMode.SelectedIndex = 1
        _ComboJava.IsEnabled = Not IsCustom
        _TextArgs.IsEnabled = Not IsCustom
        _TextCore.IsEnabled = Not IsCustom
        _TextMinMem.IsEnabled = Not IsCustom
        _TextMaxMem.IsEnabled = Not IsCustom
        _TextStartCommand.IsEnabled = IsCustom
    End Sub

    Private Sub ModeChanged()
        RefreshSettingsState()
    End Sub

    Private Sub RefreshJavaList()
        Try
            _ComboJava.Items.Clear()
            Dim Auto_ As New MyComboBoxItem With {.Content = "自动检测（推荐）", .Tag = ""}
            _ComboJava.Items.Add(Auto_)
            For Each Path As String In ServerManager.ListJava()
                _ComboJava.Items.Add(New MyComboBoxItem With {.Content = Path, .Tag = Path})
            Next
            Dim Current As String = ServerManager.JavaPath
            For i As Integer = 0 To _ComboJava.Items.Count - 1
                Dim Item As MyComboBoxItem = CType(_ComboJava.Items(i), MyComboBoxItem)
                If CStr(Item.Tag) = Current Then
                    _ComboJava.SelectedIndex = i
                    Exit For
                End If
            Next
            If _ComboJava.SelectedIndex < 0 Then _ComboJava.SelectedIndex = 0
        Catch ex As Exception
            Logger.Warn(ex, "刷新 Java 列表失败")
        End Try
    End Sub

    Private Function AddSettingsText(Label As String, Description As String, Optional OnBrowse As Action = Nothing) As MyTextBox
        Dim Panel As New StackPanel With {.Margin = New Thickness(0, 0, 0, 14)}
        Dim Lab As New TextBlock With {.Text = Label, .FontSize = 13}
        Lab.SetResourceReference(TextBlock.ForegroundProperty, "ColorBrush1")
        Dim Desc As New TextBlock With {.Text = Description, .FontSize = 12, .Margin = New Thickness(0, 2, 0, 6), .TextWrapping = TextWrapping.Wrap}
        Desc.SetResourceReference(TextBlock.ForegroundProperty, "ColorBrushGray4")
        Panel.Children.Add(Lab)
        Panel.Children.Add(Desc)
        Dim Row As New Grid
        Row.ColumnDefinitions.Add(New ColumnDefinition With {.Width = New GridLength(1, GridUnitType.Star)})
        Dim Box As New MyTextBox With {.Height = 30, .VerticalContentAlignment = VerticalAlignment.Center}
        Row.Children.Add(Box)
        If OnBrowse IsNot Nothing Then
            Row.ColumnDefinitions.Add(New ColumnDefinition With {.Width = GridLength.Auto})
            Dim Btn As New MyButton With {.Text = "浏览", .Margin = New Thickness(10, 0, 0, 0), .MinWidth = 76, .Padding = New Thickness(14, 5, 14, 5)}
            AddHandler Btn.Click, Sub() OnBrowse.Invoke()
            Grid.SetColumn(Btn, 1)
            Row.Children.Add(Btn)
        End If
        Panel.Children.Add(Row)
        PanSettingsHost.Children.Add(Panel)
        Return Box
    End Function

    Private Function AddSettingsCombo(Label As String, Items As String(), OnChange As Action) As MyComboBox
        Dim Panel As New StackPanel With {.Margin = New Thickness(0, 0, 0, 14)}
        Dim Lab As New TextBlock With {.Text = Label, .FontSize = 13, .Margin = New Thickness(0, 0, 0, 6)}
        Lab.SetResourceReference(TextBlock.ForegroundProperty, "ColorBrush1")
        Panel.Children.Add(Lab)
        Dim Combo As New MyComboBox With {.Height = 30}
        For i As Integer = 0 To Items.Length - 1
            Combo.Items.Add(New MyComboBoxItem With {.Content = Items(i), .Tag = i})
        Next
        Combo.SelectedIndex = If(ServerManager.StartMode = "custom", 1, 0)
        If OnChange IsNot Nothing Then AddHandler Combo.SelectionChanged, Sub() OnChange.Invoke()
        Panel.Children.Add(Combo)
        PanSettingsHost.Children.Add(Panel)
        Return Combo
    End Function

    Private Sub BrowseDir()
        Dim Dialog As New FolderBrowserDialog With {.Description = "选择服务端文件夹", .ShowNewFolderButton = True}
        If ServerManager.Dir <> "" AndAlso Directory.Exists(ServerManager.Dir) Then Dialog.SelectedPath = ServerManager.Dir
        If Dialog.ShowDialog() = DialogResult.OK Then
            _TextDir.Text = Dialog.SelectedPath
            If _TextName.Text = "" Then _TextName.Text = Path.GetFileName(Dialog.SelectedPath)
            '自动填核心
            Dim Old As String = ServerManager.Dir
            ServerManager.Dir = Dialog.SelectedPath
            Dim Detected As String = ServerManager.DetectJar()
            ServerManager.Dir = Old
            If Detected <> "" AndAlso _TextCore.Text = "" Then _TextCore.Text = Detected
        End If
    End Sub

    Private Sub BrowseCore()
        Dim Dialog As New OpenFileDialog With {.Title = "选择服务端核心", .Filter = "服务端核心|*.jar;*.bat;*.txt|所有文件|*.*"}
        If _TextDir.Text <> "" AndAlso Directory.Exists(_TextDir.Text) Then Dialog.InitialDirectory = _TextDir.Text
        If Dialog.ShowDialog() = DialogResult.OK Then
            Dim Base As String = If(_TextDir.Text, "")
            If Base <> "" AndAlso Dialog.FileName.StartsWith(Base, StringComparison.OrdinalIgnoreCase) Then
                _TextCore.Text = Dialog.FileName.Substring(Base.Length).TrimStart("\"c)
            Else
                _TextCore.Text = Dialog.FileName
            End If
        End If
    End Sub

    Private Sub BtnSettingsSave_Click(sender As Object, e As MouseButtonEventArgs)
        Try
            Dim Dir As String = _TextDir.Text.Trim()
            If Dir <> "" AndAlso Not Directory.Exists(Dir) Then
                Hint("实例路径不存在，请重新选择。", HintType.Red, False)
                Return
            End If
            ServerManager.Name = _TextName.Text
            ServerManager.Dir = Dir
            ServerManager.JavaPath = CStr(CType(_ComboJava.SelectedItem, MyComboBoxItem).Tag)
            ServerManager.JvmArgs = _TextArgs.Text
            ServerManager.Core = _TextCore.Text.Trim()
            Dim MinValue As Integer
            Dim MaxValue As Integer
            If Integer.TryParse(_TextMinMem.Text.Trim(), MinValue) Then ServerManager.MinMemory = MinValue
            If Integer.TryParse(_TextMaxMem.Text.Trim(), MaxValue) Then ServerManager.MaxMemory = MaxValue
            ServerManager.StartMode = If(_ComboMode.SelectedIndex = 1, "custom", "java")
            ServerManager.CustomCommand = _TextStartCommand.Text
            ServerManager.WriteConfig()
            RefreshOverview()
            Hint("实例设置已保存。")
        Catch ex As Exception
            Logger.Error(ex, "保存实例设置失败")
            Hint("保存失败：" & ex.Message, HintType.Red, False)
        End Try
    End Sub

    Private Sub BtnSettingsReset_Click(sender As Object, e As MouseButtonEventArgs)
        ServerManager.ReadConfig()
        PanSettingsHost.Children.Clear()
        BuildSettings()
        Hint("已恢复为已保存的设置。")
    End Sub

    '===============================
    '  服务器属性
    '===============================

    Private Sub BuildProperties()
        PanPropertiesHost.Children.Clear()
        _PropertyControls.Clear()
        Dim Props As Dictionary(Of String, String) = ServerManager.ReadProperties()

        AddPropertyText("motd", "服务器标语", Props)
        AddPropertyCombo("difficulty", "游戏难度", {"peaceful", "easy", "normal", "hard"}, Props)
        AddPropertyCombo("gamemode", "默认游戏模式", {"survival", "creative", "adventure", "spectator"}, Props)
        AddPropertyText("level-name", "地图名称", Props)
        AddPropertyText("server-port", "服务器端口", Props)
        AddPropertyCombo("online-mode", "正版验证", {"true", "false"}, Props)
        AddPropertyText("max-players", "最大玩家数", Props)
        AddPropertyText("view-distance", "视距（区块）", Props)
        AddPropertyCombo("pvp", "允许 PVP", {"true", "false"}, Props)
        AddPropertyCombo("white-list", "白名单", {"false", "true"}, Props)
        AddPropertyText("spawn-protection", "出生点保护半径", Props)
        AddPropertyText("level-seed", "地图种子", Props)

        Dim Tip As New TextBlock With {
            .Text = "这里只列出常用项，其它设置可以点「编辑原始文件」用记事本改完再刷新。修改一般需要重启服务器才会生效。",
            .FontSize = 12.5, .Margin = New Thickness(0, 4, 0, 0), .TextWrapping = TextWrapping.Wrap}
        Tip.SetResourceReference(TextBlock.ForegroundProperty, "ColorBrushGray4")
        PanPropertiesHost.Children.Add(Tip)
    End Sub

    Private Function AddPropertyRow(Key As String, Label As String, Control As FrameworkElement) As StackPanel
        Dim Panel As New StackPanel With {.Margin = New Thickness(0, 0, 0, 12)}
        Dim Row As New Grid
        Row.ColumnDefinitions.Add(New ColumnDefinition With {.Width = New GridLength(170)})
        Row.ColumnDefinitions.Add(New ColumnDefinition With {.Width = New GridLength(1, GridUnitType.Star)})
        Dim Lab As New TextBlock With {.Text = Label, .FontSize = 12.5, .VerticalAlignment = VerticalAlignment.Center, .TextWrapping = TextWrapping.Wrap}
        Lab.SetResourceReference(TextBlock.ForegroundProperty, "ColorBrushGray3")
        Dim Key2 As New TextBlock With {.Text = Key, .FontSize = 10.5, .Margin = New Thickness(0, 1, 0, 0)}
        Key2.SetResourceReference(TextBlock.ForegroundProperty, "ColorBrushGray5")
        Dim Left As New StackPanel
        Left.Children.Add(Lab)
        Left.Children.Add(Key2)
        Row.Children.Add(Left)
        Grid.SetColumn(Control, 1)
        Row.Children.Add(Control)
        Panel.Children.Add(Row)
        PanPropertiesHost.Children.Add(Panel)
        _PropertyControls(Key) = Control
        Return Panel
    End Function

    Private Sub AddPropertyText(Key As String, Label As String, Props As Dictionary(Of String, String))
        Dim Box As New MyTextBox With {.Text = GetProperty(Props, Key, ""), .Height = 28, .VerticalContentAlignment = VerticalAlignment.Center}
        AddPropertyRow(Key, Label, Box)
    End Sub

    Private Sub AddPropertyCombo(Key As String, Label As String, Items As String(), Props As Dictionary(Of String, String))
        Dim Combo As New MyComboBox With {.Height = 28}
        For Each Item As String In Items
            Combo.Items.Add(New MyComboBoxItem With {.Content = Item, .Tag = Item})
        Next
        Dim Current As String = GetProperty(Props, Key, Items(0))
        For i As Integer = 0 To Combo.Items.Count - 1
            If CStr(CType(Combo.Items(i), MyComboBoxItem).Tag) = Current Then
                Combo.SelectedIndex = i
                Exit For
            End If
        Next
        If Combo.SelectedIndex < 0 Then Combo.SelectedIndex = 0
        AddPropertyRow(Key, Label, Combo)
    End Sub

    Private Sub BtnPropertiesSave_Click(sender As Object, e As MouseButtonEventArgs)
        Try
            If ServerManager.Dir = "" Then
                Hint("请先在实例设置里选择服务端文件夹。", HintType.Red, False)
                Return
            End If
            Dim Values As New Dictionary(Of String, String)(StringComparer.OrdinalIgnoreCase)
            For Each Pair As KeyValuePair(Of String, FrameworkElement) In _PropertyControls
                If TypeOf Pair.Value Is MyTextBox Then
                    Values(Pair.Key) = CType(Pair.Value, MyTextBox).Text.Trim()
                ElseIf TypeOf Pair.Value Is MyComboBox Then
                    Dim Item As MyComboBoxItem = TryCast(CType(Pair.Value, MyComboBox).SelectedItem, MyComboBoxItem)
                    If Item IsNot Nothing Then Values(Pair.Key) = CStr(Item.Tag)
                End If
            Next
            ServerManager.WriteProperties(Values)
            RefreshOverview()
            Hint("服务器属性已保存，重启服务器后生效。")
        Catch ex As Exception
            Logger.Error(ex, "保存服务器属性失败")
            Hint("保存失败：" & ex.Message, HintType.Red, False)
        End Try
    End Sub

    Private Sub BtnPropertiesRaw_Click(sender As Object, e As MouseButtonEventArgs)
        Try
            If ServerManager.Dir = "" Then Return
            Dim File As String = Path.Combine(ServerManager.Dir, "server.properties")
            If Not IO.File.Exists(File) Then
                Hint("还没有 server.properties，启动一次服务器后会自动生成。", HintType.Red, False)
                Return
            End If
            Process.Start("notepad.exe", """" & File & """")
            Hint("用记事本改完保存后，回到这里重新点一次「服务器属性」即可刷新。")
        Catch ex As Exception
            Logger.Error(ex, "打开 server.properties 失败")
        End Try
    End Sub

    '===============================
    '  备份
    '===============================

    Private Sub RefreshBackups()
        Try
            AniControlEnabled += 1
            PanBackups.Children.Clear()
            If ServerManager.Dir = "" Then
                AddEmptyHint(PanBackups, "请先在实例设置里选择服务端文件夹。")
                Return
            End If
            Dim List As List(Of FileInfo) = ServerManager.ListBackups()
            If List.Count = 0 Then
                AddEmptyHint(PanBackups, "还没有备份。点「创建备份」会把服务端目录打包成 zip。")
                Return
            End If
            For Each Backup As FileInfo In List
                AddBackupRow(Backup)
            Next
        Finally
            AniControlEnabled -= 1
        End Try
    End Sub

    Private Sub AddBackupRow(Backup As FileInfo)
        Dim Grid As New Grid With {.Margin = New Thickness(0, 0, 0, 10)}
        Grid.ColumnDefinitions.Add(New ColumnDefinition With {.Width = New GridLength(1, GridUnitType.Star)})
        Grid.ColumnDefinitions.Add(New ColumnDefinition With {.Width = GridLength.Auto})
        Dim Info As New StackPanel
        Dim Name As New TextBlock With {.Text = Backup.Name, .FontSize = 13}
        Name.SetResourceReference(TextBlock.ForegroundProperty, "ColorBrush1")
        Dim Detail As New TextBlock With {
            .Text = $"{Backup.CreationTime:yyyy-MM-dd HH:mm} · {FormatFileSize(Backup.Length)}", .FontSize = 12, .Margin = New Thickness(0, 2, 0, 0)}
        Detail.SetResourceReference(TextBlock.ForegroundProperty, "ColorBrushGray4")
        Info.Children.Add(Name)
        Info.Children.Add(Detail)
        Grid.Children.Add(Info)

        Dim Buttons As New StackPanel With {.Orientation = Orientation.Horizontal}
        Dim BtnRestore As New MyButton With {.Text = "还原", .MinWidth = 66, .Padding = New Thickness(12, 5, 12, 5)}
        AddHandler BtnRestore.Click, Sub() RestoreBackup(Backup)
        Dim BtnDelete As New MyButton With {.Text = "删除", .MinWidth = 66, .Padding = New Thickness(12, 5, 12, 5), .Margin = New Thickness(8, 0, 0, 0), .ColorType = MyButton.ColorState.Red}
        AddHandler BtnDelete.Click, Sub() DeleteBackup(Backup)
        Buttons.Children.Add(BtnRestore)
        Buttons.Children.Add(BtnDelete)
        Grid.SetColumn(Buttons, 1)
        Grid.Children.Add(Buttons)
        PanBackups.Children.Add(Grid)
    End Sub

    Private Sub AddEmptyHint(Host As System.Windows.Controls.Panel, Text As String)
        Dim Tip As New TextBlock With {.Text = Text, .FontSize = 12.5, .TextWrapping = TextWrapping.Wrap}
        Tip.SetResourceReference(TextBlock.ForegroundProperty, "ColorBrushGray4")
        Host.Children.Add(Tip)
    End Sub

    Private Sub BtnBackupCreate_Click(sender As Object, e As MouseButtonEventArgs)
        If ServerManager.Dir = "" Then
            Hint("请先在实例设置里选择服务端文件夹。", HintType.Red, False)
            Return
        End If
        BtnBackupCreate.IsEnabled = False
        BtnBackupCreate.Text = "正在备份……"
        RunInNewThread(
            Sub()
                Try
                    ServerManager.CreateBackup(Sub(Message As String) RunInUi(Sub() Hint(Message, Log:=False)))
                    RunInUi(Sub() Hint("备份完成。"))
                Catch ex As Exception
                    Logger.Error(ex, "创建备份失败")
                    RunInUi(Sub() Hint("备份失败：" & ex.Message, HintType.Red, False))
                Finally
                    RunInUi(
                        Sub()
                            BtnBackupCreate.IsEnabled = True
                            BtnBackupCreate.Text = "创建备份"
                            RefreshBackups()
                        End Sub)
                End Try
            End Sub, "Server Backup")
    End Sub

    Private Sub BtnBackupRefresh_Click(sender As Object, e As MouseButtonEventArgs)
        RefreshBackups()
    End Sub

    Private Sub BtnBackupFolder_Click(sender As Object, e As MouseButtonEventArgs)
        Try
            If ServerManager.Dir = "" Then Return
            Directory.CreateDirectory(ServerManager.BackupDir)
            Process.Start("explorer.exe", """" & ServerManager.BackupDir & """")
        Catch ex As Exception
            Logger.Error(ex, "打开备份目录失败")
        End Try
    End Sub

    Private Sub RestoreBackup(Backup As FileInfo)
        If MyMsgBox($"确定要还原备份 {Backup.Name} 吗？同名文件会被覆盖，建议先停止服务器。", "还原备份", "还原", "取消") <> 1 Then Return
        RunInNewThread(
            Sub()
                Try
                    ServerManager.RestoreBackup(Backup.FullName, Sub(Message As String) RunInUi(Sub() Hint(Message, Log:=False)))
                    RunInUi(Sub() Hint("备份已还原。"))
                Catch ex As Exception
                    Logger.Error(ex, "还原备份失败")
                    RunInUi(Sub() Hint("还原失败：" & ex.Message, HintType.Red, False))
                End Try
            End Sub, "Server Restore")
    End Sub

    Private Sub DeleteBackup(Backup As FileInfo)
        If MyMsgBox($"确定要删除备份 {Backup.Name} 吗？", "删除备份", "删除", "取消", IsWarn:=True) <> 1 Then Return
        Try
            ServerManager.DeleteBackup(Backup.FullName)
            RefreshBackups()
        Catch ex As Exception
            Hint("删除失败：" & ex.Message, HintType.Red, False)
        End Try
    End Sub

    '===============================
    '  插件 / 模组
    '===============================

    Private Sub BtnPluginKind_Click(sender As Object, e As MouseButtonEventArgs)
        _PluginIsMod = Not _PluginIsMod
        BtnPluginKind.Text = If(_PluginIsMod, "模组", "插件")
        InitModDownloadUi()
        PanModResults.Children.Clear()
        RefreshPlugins()
    End Sub

    Private Sub RefreshPlugins()
        Try
            AniControlEnabled += 1
            PanPlugins.Children.Clear()
            BtnPluginKind.Text = If(_PluginIsMod, "模组", "插件")
            InitModDownloadUi()
            If ServerManager.Dir = "" Then
                AddEmptyHint(PanPlugins, "请先在实例设置里选择服务端文件夹。")
                Return
            End If
            Dim Folder As String = ServerManager.PluginDir(_PluginIsMod)
            If Folder = "" Then
                AddEmptyHint(PanPlugins, $"实例目录下没有 {If(_PluginIsMod, "mods", "plugins")} 文件夹，启动一次对应类型的服务端后会自动生成。")
                Return
            End If
            Dim List As List(Of FileInfo) = ServerManager.ListPlugins(_PluginIsMod)
            If List.Count = 0 Then
                AddEmptyHint(PanPlugins, "这个文件夹里还没有 jar 文件。点「导入 jar」把插件复制进来。")
                Return
            End If
            For Each Item As FileInfo In List
                AddPluginRow(Item)
            Next
        Finally
            AniControlEnabled -= 1
        End Try
    End Sub

    Private Sub AddPluginRow(Item As FileInfo)
        Dim Enabled As Boolean = ServerManager.IsPluginEnabled(Item)
        Dim Grid As New Grid With {.Margin = New Thickness(0, 0, 0, 10)}
        Grid.ColumnDefinitions.Add(New ColumnDefinition With {.Width = New GridLength(1, GridUnitType.Star)})
        Grid.ColumnDefinitions.Add(New ColumnDefinition With {.Width = GridLength.Auto})
        Dim Info As New StackPanel
        Dim Name As New TextBlock With {.Text = Item.Name, .FontSize = 13, .TextTrimming = TextTrimming.CharacterEllipsis}
        Name.SetResourceReference(TextBlock.ForegroundProperty, "ColorBrush1")
        Dim Detail As New TextBlock With {
            .Text = $"{If(Enabled, "已启用", "已禁用")} · {Item.Length / 1024.0:F0} KB · {Item.LastWriteTime:yyyy-MM-dd}", .FontSize = 12, .Margin = New Thickness(0, 2, 0, 0)}
        Detail.SetResourceReference(TextBlock.ForegroundProperty, If(Enabled, "ColorBrush3", "ColorBrushGray4"))
        Info.Children.Add(Name)
        Info.Children.Add(Detail)
        Grid.Children.Add(Info)

        Dim Buttons As New StackPanel With {.Orientation = Orientation.Horizontal}
        Dim BtnToggle As New MyButton With {.Text = If(Enabled, "禁用", "启用"), .MinWidth = 66, .Padding = New Thickness(12, 5, 12, 5)}
        AddHandler BtnToggle.Click, Sub() TogglePlugin(Item, Not Enabled)
        Dim BtnDelete As New MyButton With {.Text = "删除", .MinWidth = 66, .Padding = New Thickness(12, 5, 12, 5), .Margin = New Thickness(8, 0, 0, 0), .ColorType = MyButton.ColorState.Red}
        AddHandler BtnDelete.Click, Sub() DeletePlugin(Item)
        Buttons.Children.Add(BtnToggle)
        Buttons.Children.Add(BtnDelete)
        Grid.SetColumn(Buttons, 1)
        Grid.Children.Add(Buttons)
        PanPlugins.Children.Add(Grid)
    End Sub

    Private Sub TogglePlugin(Item As FileInfo, Enabled As Boolean)
        Try
            If ServerManager.IsRunning() Then Hint("建议先停止服务器再修改插件状态。", HintType.Red, False)
            ServerManager.SetPluginEnabled(Item, Enabled)
            RefreshPlugins()
        Catch ex As Exception
            Hint("操作失败：" & ex.Message, HintType.Red, False)
        End Try
    End Sub

    Private Sub DeletePlugin(Item As FileInfo)
        If MyMsgBox($"确定要删除 {Item.Name} 吗？", "删除文件", "删除", "取消", IsWarn:=True) <> 1 Then Return
        Try
            IO.File.Delete(Item.FullName)
            RefreshPlugins()
        Catch ex As Exception
            Hint("删除失败：" & ex.Message, HintType.Red, False)
        End Try
    End Sub

    Private Sub BtnPluginImport_Click(sender As Object, e As MouseButtonEventArgs)
        If ServerManager.Dir = "" Then
            Hint("请先在实例设置里选择服务端文件夹。", HintType.Red, False)
            Return
        End If
        Dim Folder As String = Path.Combine(ServerManager.Dir, If(_PluginIsMod, "mods", "plugins"))
        Try
            Directory.CreateDirectory(Folder)
            Dim Dialog As New OpenFileDialog With {.Title = "选择要导入的 jar", .Filter = "插件/模组|*.jar|所有文件|*.*", .Multiselect = True}
            If Dialog.ShowDialog() <> DialogResult.OK Then Return
            For Each File As String In Dialog.FileNames
                IO.File.Copy(File, Path.Combine(Folder, Path.GetFileName(File)), True)
            Next
            RefreshPlugins()
            Hint($"已导入 {Dialog.FileNames.Length} 个文件。")
        Catch ex As Exception
            Logger.Error(ex, "导入插件失败")
            Hint("导入失败：" & ex.Message, HintType.Red, False)
        End Try
    End Sub

    Private Sub BtnPluginFolder_Click(sender As Object, e As MouseButtonEventArgs)
        Try
            If ServerManager.Dir = "" Then Return
            Dim Folder As String = Path.Combine(ServerManager.Dir, If(_PluginIsMod, "mods", "plugins"))
            Directory.CreateDirectory(Folder)
            Process.Start("explorer.exe", """" & Folder & """")
        Catch ex As Exception
            Logger.Error(ex, "打开插件目录失败")
        End Try
    End Sub

    Private Sub BtnPluginRefresh_Click(sender As Object, e As MouseButtonEventArgs)
        RefreshPlugins()
    End Sub

    '===============================
    '  在线下载模组 / 插件（复用启动器「下载 → Mod」的资源库与列表样式）
    '===============================

    Private _ResourceStorage As New ResourceSearcher.SearchResult
    Private _ResourceLoader As LoaderTask(Of ResourceSearcher.SearchRequest, Integer) = Nothing
    Private _ResourceRenderVersion As Integer = -1

    ''' <summary>刷新「在线下载」区域的标题、版本/加载器下拉框。</summary>
    Private Sub InitModDownloadUi()
        LabModDownloadTitle.Text = If(_PluginIsMod, "在线下载模组（来源：CurseForge / Modrinth）", "在线下载插件（来源：CurseForge / Modrinth）")
        TextModQuery.HintText = If(_PluginIsMod, "搜索模组名称", "搜索插件名称")
        If String.IsNullOrEmpty(ComboModVersion.Text) Then
            ComboModVersion.Text = ServerContent.GuessGameVersion(ServerManager.Core)
        End If

        Dim Loaders As List(Of String) = If(_PluginIsMod,
            ServerContent.DetectModLoaders(ServerManager.Dir),
            ServerContent.DetectPluginLoaders(ServerManager.Dir, ServerManager.Core))
        If Loaders.Count = 0 Then
            If _PluginIsMod Then
                Loaders.AddRange({"fabric", "forge", "neoforge", "quilt"})
            Else
                Loaders.AddRange({"paper", "spigot", "bukkit"})
            End If
        End If

        '只在选项变化时重建，避免刷新时丢掉当前选择
        Dim Same As Boolean = ComboModLoader.Items.Count = Loaders.Count
        If Same Then
            For i As Integer = 0 To Loaders.Count - 1
                Dim Existing As MyComboBoxItem = TryCast(ComboModLoader.Items(i), MyComboBoxItem)
                If Existing Is Nothing OrElse Existing.Tag.ToString() <> Loaders(i) Then Same = False : Exit For
            Next
        End If
        If Not Same Then
            ComboModLoader.Items.Clear()
            For Each L As String In Loaders
                ComboModLoader.Items.Add(New MyComboBoxItem With {.Content = LoaderText(L), .Tag = L})
            Next
            If ComboModLoader.SelectedIndex < 0 Then ComboModLoader.SelectedIndex = 0
        End If
        EnsureVersionOptions()
    End Sub

    ''' <summary>游戏版本下拉框的候选，第一次进入时从 Mojang 版本清单拉取。</summary>
    Private Shared _VersionOptions As List(Of String)
    Private Shared _VersionOptionsLoaded As Boolean

    Private Sub EnsureVersionOptions()
        If _VersionOptionsLoaded Then
            ApplyVersionOptions()
            Return
        End If
        RunInNewThread(
            Sub()
                Dim Versions As New List(Of String)
                Try
                    Versions = ServerCoreDownloader.GetVersions(ServerCoreDownloader.CoreSource.Vanilla)
                Catch ex As Exception
                    Logger.Warn(ex, "获取游戏版本列表失败")
                End Try
                If Versions.Count > 0 Then
                    _VersionOptions = Versions
                    _VersionOptionsLoaded = True
                End If
                RunInUi(Sub() ApplyVersionOptions())
            End Sub, "Load Game Versions")
    End Sub

    Private Sub ApplyVersionOptions()
        If _VersionOptions Is Nothing OrElse _VersionOptions.Count = 0 Then Return
        Dim Current As String = If(ComboModVersion.Text, "").Trim()
        If ComboModVersion.Items.Count = _VersionOptions.Count Then Return '已经填过了
        ComboModVersion.Items.Clear()
        For Each Version As String In _VersionOptions
            ComboModVersion.Items.Add(New MyComboBoxItem With {.Content = Version, .Tag = Version})
        Next
        If Current <> "" AndAlso _VersionOptions.Contains(Current) Then
            ComboModVersion.Text = Current
        ElseIf ComboModVersion.SelectedIndex < 0 Then
            ComboModVersion.Text = ""
        End If
    End Sub

    Private Shared Function LoaderText(Loader As String) As String
        Select Case If(Loader, "").ToLowerInvariant()
            Case "fabric" : Return "Fabric"
            Case "forge" : Return "Forge"
            Case "neoforge" : Return "NeoForge"
            Case "quilt" : Return "Quilt"
            Case "paper" : Return "Paper"
            Case "purpur" : Return "Purpur"
            Case "spigot" : Return "Spigot"
            Case "bukkit" : Return "Bukkit"
            Case Else : Return Loader
        End Select
    End Function

    Private Function SelectedLoader() As String
        Dim Item As MyComboBoxItem = TryCast(ComboModLoader.SelectedItem, MyComboBoxItem)
        If Item Is Nothing OrElse Item.Tag Is Nothing Then Return ""
        Return Item.Tag.ToString()
    End Function

    ''' <summary>把下拉框选中的加载器换成资源库使用的 ModLoaders 标记。</summary>
    Private Function SelectedModLoaders() As ModLoaders
        If Not _PluginIsMod Then Return ModLoaders.None '服务端插件按类别搜索，不筛加载器
        Select Case SelectedLoader().ToLowerInvariant()
            Case "fabric" : Return ModLoaders.Fabric
            Case "forge" : Return ModLoaders.Forge
            Case "neoforge" : Return ModLoaders.NeoForge
            Case Else : Return ModLoaders.None
        End Select
    End Function

    Private Sub InitResourceLoader()
        If _ResourceLoader IsNot Nothing Then Return
        _ResourceLoader = ResourceSearcher.GetLoader(Function() BuildResourceRequest())
        AddHandler _ResourceLoader.OnStateChangedUi,
            Sub(L As LoaderBase, NewState As LoadState, OldState As LoadState)
                Dim L2 As LoaderBase = L
                RunInUi(
                    Sub()
                        Try
                            BtnModSearch.IsEnabled = (L.State <> LoadState.Loading)
                            Select Case L.State
                                Case LoadState.Finished
                                    RenderResourceResults()
                                Case LoadState.Failed
                                    PanModResults.Children.Clear()
                                    AddEmptyHint(PanModResults, "搜索失败：" & If(L.Error Is Nothing, "未知错误", L.Error.Message))
                            End Select
                        Catch ex As Exception
                            Logger.Error(ex, "刷新资源列表失败")
                        End Try
                    End Sub)
            End Sub
    End Sub

    Private Function BuildResourceRequest() As ResourceSearcher.SearchRequest
        Dim Version As String = If(ComboModVersion.Text, "").Trim()
        Dim Request As New ResourceSearcher.SearchRequest(
            If(_PluginIsMod, ResourceTypes.Mod, ResourceTypes.Mod), _ResourceStorage, 40)
        Request.SearchText = If(TextModQuery.Text, "").Trim()
        Request.GameVersion = If(Version = "", Nothing, Version)
        Request.Tag = ""
        Request.ModLoaders = SelectedModLoaders()
        '服务端插件在 CurseForge 里大多是模组加载器版本，Bukkit / Paper 构建只在 Modrinth 上有
        Request.Sources = If(_PluginIsMod, ResourcePlatforms.Any, ResourcePlatforms.Modrinth)
        Return Request
    End Function

    Private Sub BtnModSearch_Click(sender As Object, e As MouseButtonEventArgs)
        InitResourceLoader()
        _ResourceStorage = New ResourceSearcher.SearchResult
        _ResourceLoader.LastFinishedTime = 0 '相同关键词也强制重新搜索
        PanModResults.Children.Clear()
        AddEmptyHint(PanModResults, "正在搜索 ……")
        BtnModSearch.IsEnabled = False
        Try
            _ResourceLoader.Start(Nothing, True)
        Catch ex As Exception
            BtnModSearch.IsEnabled = True
            Logger.Error(ex, "启动资源搜索失败")
        End Try
    End Sub

    ''' <summary>把搜索结果画成和「下载 → Mod」完全一致的列表项。</summary>
    Private Sub RenderResourceResults()
        PanModResults.Children.Clear()
        If _ResourceStorage.ErrorMessage IsNot Nothing Then
            AddEmptyHint(PanModResults, _ResourceStorage.ErrorMessage)
        End If
        If _ResourceStorage.Results.Count = 0 Then
            If _ResourceStorage.ErrorMessage Is Nothing Then AddEmptyHint(PanModResults, "没有找到匹配的内容，换个关键词或版本试试。")
            Return
        End If
        AniControlEnabled += 1
        Try
            For Each Project As ResourceProject In _ResourceStorage.Results
                Dim Item As MyResourceItem = Project.ToResourceItem(ShowMcVersionDesc:=True, ShowLoaderDesc:=True)
                If Item Is Nothing Then Continue For
                Item.ShowDetailOnClick = False '点击时安装到服务端，而不是打开详情页
                AddHandler Item.Click, Sub(sender, e) InstallResourceProject(Project, Item)
                PanModResults.Children.Add(Item)
            Next
        Finally
            AniControlEnabled -= 1
        End Try
    End Sub

    ''' <summary>检查文件是否可以装进当前服务端（jar 且游戏版本匹配）。</summary>
    Private Function IsUsableJar(Candidate As ResourceVersion, GameVersion As String) As Boolean
        If String.IsNullOrEmpty(Candidate.FileName) Then Return False
        If Not Candidate.FileName.ToLowerInvariant().EndsWith(".jar") Then Return False
        If Candidate.GameVersions IsNot Nothing AndAlso GameVersion <> "" AndAlso
           Not Candidate.GameVersions.Contains(GameVersion) Then Return False
        Return True
    End Function

    ''' <summary>服务端插件打分：Bukkit / Paper / Spigot 优先，Forge 系靠后。</summary>
    Private Function PluginBuildScore(FileName As String) As Integer
        Dim Name As String = If(FileName, "").ToLowerInvariant()
        If Name.Contains("bukkit") OrElse Name.Contains("paper") OrElse Name.Contains("spigot") OrElse Name.Contains("purpur") Then Return 0
        If Name.Contains("neoforge") OrElse Name.Contains("forge") OrElse Name.Contains("fabric") OrElse Name.Contains("quilt") Then Return 2
        Return 1
    End Function

    ''' <summary>下载选中工程的兼容文件到 mods / plugins 目录，多个候选时弹窗选择版本。</summary>
    Private Sub InstallResourceProject(Project As ResourceProject, Item As MyResourceItem)
        If ServerManager.Dir = "" Then
            Hint("请先在实例设置里选择服务端文件夹", HintType.Red, False)
            Return
        End If
        Dim TargetFolder As String = IO.Path.Combine(ServerManager.Dir, If(_PluginIsMod, "mods", "plugins"))
        Dim GameVersion As String = If(ComboModVersion.Text, "").Trim()
        Dim Loaders As ModLoaders = SelectedModLoaders()
        Dim IsMod As Boolean = _PluginIsMod
        If Item IsNot Nothing Then Item.IsEnabled = False

        RunInNewThread(
            Sub()
                Try
                    Dim Files As List(Of ResourceVersion) = ResourceVersion.FromProjectId(Project.Id, Project.Platform)
                    Dim Candidates As New List(Of ResourceVersion)
                    If Not IsMod Then
                        '插件优先 Bukkit / Paper / Spigot 的构建，其它构建排后面
                        Dim Usable As List(Of ResourceVersion) = Files.Where(Function(F) IsUsableJar(F, GameVersion)).ToList()
                        Dim BukkitBuilds As List(Of ResourceVersion) = Usable.
                            Where(Function(F) PluginBuildScore(F.FileName) = 0).ToList()
                        '只有服务端插件（Bukkit 系）才列出，避免把 Velocity / Bungee / Forge 构建混进来
                        If BukkitBuilds.Count > 0 Then
                            Candidates = BukkitBuilds.OrderByDescending(Function(F) F.ReleaseDate).ToList()
                        Else
                            Candidates = Usable.
                                Where(Function(F) PluginBuildScore(F.FileName) <= 1).
                                OrderBy(Function(F) PluginBuildScore(F.FileName)).
                                ThenByDescending(Function(F) F.ReleaseDate).ToList()
                        End If
                    Else
                        Candidates = Files.Where(Function(F) IsUsableJar(F, GameVersion)).
                            Where(Function(F) Loaders = ModLoaders.None OrElse F.ModLoaders = ModLoaders.None OrElse F.ModLoaders.HasFlagF(Loaders)).
                            OrderByDescending(Function(F) F.ReleaseDate).ToList()
                    End If
                    If Candidates.Count = 0 Then
                        RunInUi(
                            Sub()
                                If Item IsNot Nothing Then Item.IsEnabled = True
                                Hint($"「{Project.TranslatedName}」没有适配 {GameVersion} 的文件", HintType.Red, False)
                            End Sub)
                        Return
                    End If

                    '多个候选文件时让用户自己挑版本
                    Dim File As ResourceVersion = Candidates(0)
                    If Candidates.Count > 1 Then
                        Dim Choice As Integer? = Nothing
                        RunInUiWait(
                            Sub()
                                Dim Radios As New List(Of IMyRadio)
                                For Each Candidate As ResourceVersion In Candidates
                                    Radios.Add(New MyRadioBox With {
                                        .Text = $"{Candidate.FileName}（{Candidate.ReleaseDate:yyyy-MM-dd}）",
                                        .Checked = (Candidate Is Candidates(0))})
                                Next
                                Choice = MyMsgBoxSelect(Radios, $"选择要安装的 {Project.TranslatedName} 版本")
                            End Sub)
                        If Choice Is Nothing Then
                            RunInUi(Sub() If Item IsNot Nothing Then Item.IsEnabled = True)
                            Return
                        End If
                        File = Candidates(Choice.Value)
                    End If

                    IO.Directory.CreateDirectory(TargetFolder)
                    Dim Dest As String = IO.Path.Combine(TargetFolder, File.FileName)
                    Dim Done As Boolean = False
                    '先走带镜像 / DoH 兜底的下载器，失败再用启动器自带的多线程下载
                    For Each Url As String In File.DownloadUrls
                        Try
                            DownloadHelper.DownloadFile(Url, Dest, Nothing)
                            Done = True
                            Exit For
                        Catch ex As Exception
                            Logger.Warn(ex, "资源下载失败：" & Url)
                        End Try
                    Next
                    If Not Done Then
                        For Each Url As String In File.DownloadUrls
                            Try
                                NetDownloadByClient(Url, Dest, SimulateBrowserHeaders:=True)
                                Done = True
                                Exit For
                            Catch ex As Exception
                                Logger.Warn(ex, "资源下载失败：" & Url)
                            End Try
                        Next
                    End If
                    If Not Done Then Throw New Exception("所有下载地址都失败了")

                    RunInUi(
                        Sub()
                            If Item IsNot Nothing Then Item.IsEnabled = True
                            RefreshPlugins()
                            Hint($"已安装 {Project.TranslatedName} → {File.FileName}")
                        End Sub)
                Catch ex As Exception
                    Logger.Error(ex, "安装资源失败")
                    RunInUi(
                        Sub()
                            If Item IsNot Nothing Then Item.IsEnabled = True
                            Hint("安装失败：" & ex.Message, HintType.Red, False)
                        End Sub)
                End Try
            End Sub, "Install Resource")
    End Sub

    '===============================
    '  文件管理
    '===============================

    Private Sub RefreshFiles()
        Try
            AniControlEnabled += 1
            PanFiles.Children.Clear()
            LabFilePath.Text = "/" & _FileRelative.Replace("\"c, "/"c)
            If ServerManager.Dir = "" Then
                AddEmptyHint(PanFiles, "请先在实例设置里选择服务端文件夹。")
                Return
            End If
            Dim List As List(Of FileSystemInfo) = ServerManager.ListFiles(_FileRelative)
            If List.Count = 0 Then
                AddEmptyHint(PanFiles, "这个文件夹是空的。")
                Return
            End If
            For Each Item As FileSystemInfo In List
                AddFileRow(Item)
            Next
        Finally
            AniControlEnabled -= 1
        End Try
    End Sub

    Private Sub AddFileRow(Item As FileSystemInfo)
        Dim IsFolder As Boolean = TypeOf Item Is DirectoryInfo
        Dim Relative As String = _FileRelative
        Dim Name As String = Item.Name
        If Relative <> "" Then Relative &= "\"
        Relative &= Name

        Dim Grid As New Grid With {.Margin = New Thickness(0, 0, 0, 8)}
        Grid.ColumnDefinitions.Add(New ColumnDefinition With {.Width = New GridLength(1, GridUnitType.Star)})
        Grid.ColumnDefinitions.Add(New ColumnDefinition With {.Width = GridLength.Auto})
        Dim Info As New StackPanel
        Dim NameBlock As New TextBlock With {.Text = If(IsFolder, "📁 " & Name, Name), .FontSize = 13, .TextTrimming = TextTrimming.CharacterEllipsis}
        NameBlock.SetResourceReference(TextBlock.ForegroundProperty, "ColorBrush1")
        Dim Detail As New TextBlock With {
            .Text = If(IsFolder, "文件夹", $"{CDir(Item) / 1024.0:F0} KB · {Item.LastWriteTime:yyyy-MM-dd HH:mm}"), .FontSize = 12, .Margin = New Thickness(0, 2, 0, 0)}
        Detail.SetResourceReference(TextBlock.ForegroundProperty, "ColorBrushGray4")
        Info.Children.Add(NameBlock)
        Info.Children.Add(Detail)
        Grid.Children.Add(Info)

        Dim Buttons As New StackPanel With {.Orientation = Orientation.Horizontal}
        If IsFolder Then
            Dim BtnOpen As New MyButton With {.Text = "打开", .MinWidth = 66, .Padding = New Thickness(12, 5, 12, 5)}
            AddHandler BtnOpen.Click, Sub()
                                           _FileRelative = Relative
                                           RefreshFiles()
                                       End Sub
            Buttons.Children.Add(BtnOpen)
        Else
            Dim BtnOpen As New MyButton With {.Text = "打开", .MinWidth = 66, .Padding = New Thickness(12, 5, 12, 5)}
            AddHandler BtnOpen.Click, Sub() OpenFileWithShell(Item.FullName)
            Dim BtnEdit As New MyButton With {.Text = "编辑", .MinWidth = 66, .Padding = New Thickness(12, 5, 12, 5), .Margin = New Thickness(8, 0, 0, 0)}
            AddHandler BtnEdit.Click, Sub() TextEditFile(Relative)
            Buttons.Children.Add(BtnOpen)
            Buttons.Children.Add(BtnEdit)
        End If
        Dim BtnDelete As New MyButton With {.Text = "删除", .MinWidth = 66, .Padding = New Thickness(12, 5, 12, 5), .Margin = New Thickness(8, 0, 0, 0), .ColorType = MyButton.ColorState.Red}
        AddHandler BtnDelete.Click, Sub() DeleteFileEntry(Relative)
        Buttons.Children.Add(BtnDelete)
        Grid.SetColumn(Buttons, 1)
        Grid.Children.Add(Buttons)
        PanFiles.Children.Add(Grid)
    End Sub

    Private Function CDir(Info As FileSystemInfo) As Long
        Dim File As FileInfo = TryCast(Info, FileInfo)
        Return If(File Is Nothing, 0, File.Length)
    End Function

    Private Sub OpenFileWithShell(FullPath As String)
        Try
            Process.Start("explorer.exe", """" & FullPath & """")
        Catch ex As Exception
            Logger.Error(ex, "打开文件失败")
        End Try
    End Sub

    Private Sub TextEditFile(Relative As String)
        Try
            Dim Content As String = ServerManager.ReadTextFile(Relative)
            Dim Edited As String = MyMsgBoxInput(Content, "编辑 " & Path.GetFileName(Relative))
            If Edited Is Nothing Then Return
            If Edited = Content Then Return
            ServerManager.WriteTextFile(Relative, Edited)
            Hint("已保存。")
        Catch ex As Exception
            Hint("打开失败：" & ex.Message, HintType.Red, False)
        End Try
    End Sub

    Private Sub DeleteFileEntry(Relative As String)
        If MyMsgBox($"确定要删除 {Relative} 吗？文件夹会被整个删除。", "删除文件", "删除", "取消", IsWarn:=True) <> 1 Then Return
        Try
            ServerManager.DeleteEntry(Relative)
            RefreshFiles()
        Catch ex As Exception
            Hint("删除失败：" & ex.Message, HintType.Red, False)
        End Try
    End Sub

    Private Sub BtnFileUp_Click(sender As Object, e As EventArgs)
        If _FileRelative = "" Then Return
        Dim Parent As String = Path.GetDirectoryName(_FileRelative)
        _FileRelative = If(Parent, "")
        RefreshFiles()
    End Sub

    Private Sub BtnFileRefresh_Click(sender As Object, e As MouseButtonEventArgs)
        RefreshFiles()
    End Sub

    Private Sub BtnFileMkdir_Click(sender As Object, e As MouseButtonEventArgs)
        Try
            Dim Input As String = MyMsgBoxInput("", "新建文件夹", "请输入文件夹名称")
            If String.IsNullOrEmpty(Input) Then Return
            Dim Relative As String = If(_FileRelative = "", Input, _FileRelative & "\" & Input)
            ServerManager.CreateFolder(Relative)
            RefreshFiles()
        Catch ex As Exception
            Hint("创建失败：" & ex.Message, HintType.Red, False)
        End Try
    End Sub

    Private Sub BtnFileOpenDir_Click(sender As Object, e As MouseButtonEventArgs)
        Try
            Dim Target As String = If(_FileRelative = "", ServerManager.Dir, Path.Combine(ServerManager.Dir, _FileRelative))
            If Directory.Exists(Target) Then Process.Start("explorer.exe", """" & Target & """")
        Catch ex As Exception
            Logger.Error(ex, "打开文件夹失败")
        End Try
    End Sub

    '===============================
    '  侧栏按钮
    '===============================

    Private Sub BtnChooseDir_Click(sender As Object, e As MouseButtonEventArgs)
        Dim Dialog As New FolderBrowserDialog With {.Description = "选择服务端文件夹", .ShowNewFolderButton = True}
        If ServerManager.Dir <> "" AndAlso Directory.Exists(ServerManager.Dir) Then Dialog.SelectedPath = ServerManager.Dir
        If Dialog.ShowDialog() <> DialogResult.OK Then Return
        ServerManager.Dir = Dialog.SelectedPath
        If ServerManager.Name = "" Then ServerManager.Name = Path.GetFileName(Dialog.SelectedPath)
        If ServerManager.Core = "" Then ServerManager.Core = ServerManager.DetectJar()
        ServerManager.WriteConfig()
        PanSettingsHost.Children.Clear()
        BuildSettings()
        RefreshState()
        Hint("已选择服务端：" & Dialog.SelectedPath)
    End Sub

    Private Sub BtnOpenFolder_Click(sender As Object, e As MouseButtonEventArgs)
        Try
            If ServerManager.Dir = "" OrElse Not Directory.Exists(ServerManager.Dir) Then Return
            Process.Start("explorer.exe", """" & ServerManager.Dir & """")
        Catch ex As Exception
            Logger.Error(ex, "打开服务端目录失败")
        End Try
    End Sub

    Private Sub BtnStart_Click(sender As Object, e As MouseButtonEventArgs)
        RunInNewThread(
            Sub()
                Try
                    ServerManager.StartServer()
                Catch ex As ServerEulaException
                    RunInUi(
                        Sub()
                            If MyMsgBox("启动前需要先同意 Minecraft 最终用户许可协议（EULA），是否同意并继续？", "同意 EULA", "同意并启动", "取消") = 1 Then
                                ServerManager.AcceptEula()
                                RunInNewThread(Sub()
                                                   Try
                                                       ServerManager.StartServer()
                                                   Catch ex2 As Exception
                                                       Logger.Error(ex2, "启动服务器失败")
                                                       RunInUi(Sub() Hint("启动失败：" & ex2.Message, HintType.Red, False))
                                                   End Try
                                               End Sub, "Server Start")
                            End If
                        End Sub)
                Catch ex As Exception
                    Logger.Error(ex, "启动服务器失败")
                    RunInUi(Sub() Hint("启动失败：" & ex.Message, HintType.Red, False))
                End Try
            End Sub, "Server Start")
    End Sub

    Private Sub BtnStop_Click(sender As Object, e As MouseButtonEventArgs)
        If MyMsgBox("确定要停止服务器吗？", "停止服务器", "停止", "取消", IsWarn:=True) <> 1 Then Return
        RunInNewThread(Sub() ServerManager.StopServer(), "Server Stop")
    End Sub

    Private Sub BtnRestart_Click(sender As Object, e As MouseButtonEventArgs)
        If MyMsgBox("确定要重启服务器吗？", "重启服务器", "重启", "取消", IsWarn:=True) <> 1 Then Return
        RunInNewThread(
            Sub()
                Try
                    ServerManager.StopServer()
                    Threading.Thread.Sleep(1500)
                    ServerManager.StartServer()
                Catch ex As Exception
                    Logger.Error(ex, "重启服务器失败")
                    RunInUi(Sub() Hint("重启失败：" & ex.Message, HintType.Red, False))
                End Try
            End Sub, "Server Restart")
    End Sub

    '===============================
    '  玩家管理
    '===============================

    Private Sub RefreshPlayersList()
        PanPlayerLists.Children.Clear()
        AddPlayerSection("白名单", "开启白名单后，只有名单里的玩家才能进入服务器。", ServerManager.PlayerListType.Whitelist)
        AddPlayerSection("管理员（OP）", "管理员可以使用大部分管理指令。", ServerManager.PlayerListType.Ops)
        AddPlayerSection("封禁名单", "名单里的玩家无法进入服务器。", ServerManager.PlayerListType.Banned)
    End Sub

    Private Sub AddPlayerSection(TitleText As String, Description As String, Type As ServerManager.PlayerListType)
        Dim Panel As New StackPanel With {.Margin = New Thickness(0, 0, 0, 18)}

        Panel.Children.Add(New TextBlock With {.Text = TitleText, .FontSize = 14, .FontWeight = FontWeights.SemiBold})
        Panel.Children.Add(New TextBlock With {
            .Text = Description, .FontSize = 12, .Margin = New Thickness(0, 2, 0, 8),
            .TextWrapping = TextWrapping.Wrap, .Foreground = New Media.SolidColorBrush(Media.Color.FromRgb(120, 120, 120))})

        Dim Editor As New WrapPanel With {.Margin = New Thickness(0, 0, 0, 8)}
        Dim Input As New MyTextBox With {.MaxHeight = 28, .Width = 180, .HintText = "输入玩家名"}
        Dim BtnAdd As New MyButton With {.Text = "添加", .Padding = New Thickness(14, 6, 14, 6), .Margin = New Thickness(8, 0, 0, 0)}
        AddHandler BtnAdd.Click,
            Sub()
                Dim Name As String = If(Input.Text, "").Trim()
                If Name = "" Then
                    Hint("请输入玩家名", HintType.Red, False)
                    Return
                End If
                Try
                    ServerManager.PlayerListSet(Type, Name, True)
                    RunInUi(Sub() RefreshPlayersList())
                Catch ex As Exception
                    Hint("添加失败：" & ex.Message, HintType.Red, False)
                End Try
            End Sub
        Editor.Children.Add(Input)
        Editor.Children.Add(BtnAdd)
        Panel.Children.Add(Editor)

        Dim Names As List(Of String) = ServerManager.ReadPlayerList(Type)
        If Names.Count = 0 Then
            Panel.Children.Add(New TextBlock With {
                .Text = "（空）", .FontSize = 12.5, .Foreground = New Media.SolidColorBrush(Media.Color.FromRgb(150, 150, 150))})
        Else
            For Each PlayerName As String In Names
                Dim Row As New Grid With {.Margin = New Thickness(0, 0, 0, 6)}
                Row.ColumnDefinitions.Add(New ColumnDefinition With {.Width = New GridLength(1, GridUnitType.Star)})
                Row.ColumnDefinitions.Add(New ColumnDefinition With {.Width = GridLength.Auto})
                Dim Lab As New TextBlock With {.Text = PlayerName, .FontSize = 13, .VerticalAlignment = VerticalAlignment.Center}
                Grid.SetColumn(Lab, 0)
                Dim BtnDel As New MyButton With {.Text = "移除", .Padding = New Thickness(12, 4, 12, 4), .ColorType = MyButton.ColorState.Red}
                AddHandler BtnDel.Click,
                    Sub()
                        Try
                            ServerManager.PlayerListSet(Type, PlayerName, False)
                            RefreshPlayersList()
                        Catch ex As Exception
                            Hint("移除失败：" & ex.Message, HintType.Red, False)
                        End Try
                    End Sub
                Grid.SetColumn(BtnDel, 1)
                Row.Children.Add(Lab)
                Row.Children.Add(BtnDel)
                Panel.Children.Add(Row)
            Next
        End If

        PanPlayerLists.Children.Add(Panel)
    End Sub

    Private Sub BtnPlayersRefresh_Click(sender As Object, e As MouseButtonEventArgs)
        RefreshPlayersList()
    End Sub

    Private Sub BtnWhitelistOn_Click(sender As Object, e As MouseButtonEventArgs)
        Try
            ServerManager.SetProperty("white-list", "true")
            ServerManager.SetProperty("enforce-whitelist", "true")
            Hint("已开启白名单（服务器重启后生效）")
        Catch ex As Exception
            Hint("设置失败：" & ex.Message, HintType.Red, False)
        End Try
    End Sub

    Private Sub BtnWhitelistOff_Click(sender As Object, e As MouseButtonEventArgs)
        Try
            ServerManager.SetProperty("white-list", "false")
            Hint("已关闭白名单")
        Catch ex As Exception
            Hint("设置失败：" & ex.Message, HintType.Red, False)
        End Try
    End Sub

    '===============================
    '  定时任务
    '===============================

    Private Function TaskActionText(Action As String) As String
        Select Case Action.ToLowerInvariant()
            Case "backup" : Return "创建备份"
            Case "restart" : Return "重启服务器"
            Case "stop" : Return "停止服务器"
            Case "start" : Return "启动服务器"
            Case "command" : Return "发送指令"
            Case Else : Return Action
        End Select
    End Function

    Private Sub RefreshTasks()
        ServerManager.LoadTasks()
        PanTasks.Children.Clear()

        If ServerManager.Tasks.Count = 0 Then
            PanTasks.Children.Add(New TextBlock With {
                .Text = "还没有定时任务。上面选好时间和动作，点「添加任务」就行。",
                .FontSize = 12.5, .TextWrapping = TextWrapping.Wrap,
                .Foreground = New Media.SolidColorBrush(Media.Color.FromRgb(150, 150, 150))})
            Return
        End If

        For Each Task As ServerManager.ScheduledTask In ServerManager.Tasks
            Dim Row As New Grid With {.Margin = New Thickness(0, 0, 0, 8)}
            Row.ColumnDefinitions.Add(New ColumnDefinition With {.Width = New GridLength(1, GridUnitType.Star)})
            Row.ColumnDefinitions.Add(New ColumnDefinition With {.Width = GridLength.Auto})

            Dim Info As New StackPanel()
            Dim Desc As String = TaskActionText(Task.Action)
            If Task.Action.ToLowerInvariant() = "command" AndAlso Not String.IsNullOrEmpty(Task.Command) Then Desc &= "：" & Task.Command
            Info.Children.Add(New TextBlock With {.Text = "每天 " & Task.Time & " · " & Desc, .FontSize = 13})
            Info.Children.Add(New TextBlock With {
                .Text = If(Task.Enabled, "已启用", "已停用") & If(String.IsNullOrEmpty(Task.LastRun), "", " · 上次执行 " & Task.LastRun),
                .FontSize = 11.5, .Margin = New Thickness(0, 2, 0, 0),
                .Foreground = New Media.SolidColorBrush(Media.Color.FromRgb(150, 150, 150))})

            Dim BtnToggle As New MyButton With {
                .Text = If(Task.Enabled, "停用", "启用"), .Padding = New Thickness(12, 4, 12, 4),
                .Margin = New Thickness(0, 0, 8, 0)}
            AddHandler BtnToggle.Click,
                Sub()
                    Task.Enabled = Not Task.Enabled
                    ServerManager.SaveTasks()
                    RefreshTasks()
                End Sub
            Dim BtnDelete As New MyButton With {.Text = "删除", .Padding = New Thickness(12, 4, 12, 4), .ColorType = MyButton.ColorState.Red}
            AddHandler BtnDelete.Click,
                Sub()
                    ServerManager.Tasks.Remove(Task)
                    ServerManager.SaveTasks()
                    RefreshTasks()
                End Sub

            Dim Buttons As New StackPanel With {.Orientation = Orientation.Horizontal, .VerticalAlignment = VerticalAlignment.Center}
            Buttons.Children.Add(BtnToggle)
            Buttons.Children.Add(BtnDelete)
            Grid.SetColumn(Buttons, 1)

            Row.Children.Add(Info)
            Row.Children.Add(Buttons)
            PanTasks.Children.Add(Row)
        Next
    End Sub

    Private Sub BtnTaskAdd_Click(sender As Object, e As MouseButtonEventArgs)
        Dim TimeText As String = If(TextTaskTime.Text, "").Trim()
        Dim Match As Text.RegularExpressions.Match = Text.RegularExpressions.Regex.Match(TimeText, "^([01]?\d|2[0-3]):([0-5]\d)$")
        If Not Match.Success Then
            Hint("时间格式应为 04:00 这种", HintType.Red, False)
            Return
        End If
        TimeText = Match.Groups(1).Value.PadLeft(2, "0"c) & ":" & Match.Groups(2).Value

        Dim Action As String = "backup"
        Dim SelectItem As MyComboBoxItem = TryCast(ComboTaskAction.SelectedItem, MyComboBoxItem)
        If SelectItem IsNot Nothing AndAlso SelectItem.Tag IsNot Nothing Then Action = SelectItem.Tag.ToString()

        Dim Task As New ServerManager.ScheduledTask With {
            .Time = TimeText,
            .Action = Action,
            .Command = If(TextTaskCommand.Text, "").Trim(),
            .Enabled = True}
        If Action = "command" AndAlso Task.Command = "" Then
            Hint("发送指令需要填写指令内容", HintType.Red, False)
            Return
        End If

        ServerManager.Tasks.Add(Task)
        ServerManager.SaveTasks()
        RefreshTasks()
        Hint($"已添加每天 {TimeText} 的{TaskActionText(Action)}任务")
    End Sub

    '===============================
    '  下载核心
    '===============================

    Private _CoreUiBusy As Boolean = False

    Private Sub BuildDownloadTab()
        If ComboCoreSource.Items.Count = 0 Then
            _CoreUiBusy = True
            Try
                For Each Name As String In ServerCoreDownloader.SourceNames
                    ComboCoreSource.Items.Add(New MyComboBoxItem With {.Content = Name})
                Next
                ComboCoreSource.SelectedIndex = 0
            Finally
                _CoreUiBusy = False
            End Try
            LoadCoreGameVersions()
        End If
    End Sub

    Private Function SelectedSource() As ServerCoreDownloader.CoreSource
        Return ServerCoreDownloader.SourceFromIndex(ComboCoreSource.SelectedIndex)
    End Function

    Private Function ComboText(Combo As MyComboBox) As String
        Dim Item As MyComboBoxItem = TryCast(Combo.SelectedItem, MyComboBoxItem)
        If Item Is Nothing Then Return ""
        Return If(Item.Content?.ToString(), "").Trim()
    End Function

    Private Sub ComboCoreSource_SelectionChanged(sender As Object, e As SelectionChangedEventArgs)
        If _CoreUiBusy Then Return
        LoadCoreGameVersions()
    End Sub

    ''' <summary>来源变了：重新拉游戏版本列表，再按第一个版本拉加载器列表。</summary>
    Private Sub LoadCoreGameVersions()
        Dim Source As ServerCoreDownloader.CoreSource = SelectedSource()
        LabCoreStatus.Text = "正在获取游戏版本列表……"
        RunInNewThread(
            Sub()
                Try
                    Dim Versions As List(Of String) = ServerCoreDownloader.GetGameVersions(Source)
                    RunInUi(
                        Sub()
                            _CoreUiBusy = True
                            Try
                                ComboCoreGameVersion.Items.Clear()
                                For Each Version As String In Versions
                                    ComboCoreGameVersion.Items.Add(New MyComboBoxItem With {.Content = Version})
                                Next
                                If Versions.Count > 0 Then ComboCoreGameVersion.SelectedIndex = 0
                            Finally
                                _CoreUiBusy = False
                            End Try
                            LoadCoreLoaderVersions()
                        End Sub)
                Catch ex As Exception
                    Logger.Error(ex, "获取服务端版本列表失败")
                    RunInUi(Sub()
                                LabCoreStatus.Text = "获取游戏版本失败：" & ex.Message
                                Hint("获取游戏版本失败：" & ex.Message, HintType.Red, False)
                            End Sub)
                End Try
            End Sub, "Load Core Game Versions")
    End Sub

    Private Sub ComboCoreGameVersion_SelectionChanged(sender As Object, e As SelectionChangedEventArgs)
        If _CoreUiBusy Then Return
        LoadCoreLoaderVersions()
    End Sub

    ''' <summary>游戏版本变了：重新拉加载器版本；不需要加载器的来源直接禁用下拉。</summary>
    Private Sub LoadCoreLoaderVersions()
        Dim Source As ServerCoreDownloader.CoreSource = SelectedSource()
        Dim GameVersion As String = ComboText(ComboCoreGameVersion)
        If Not ServerCoreDownloader.NeedsLoader(Source) Then
            _CoreUiBusy = True
            Try
                ComboCoreLoaderVersion.Items.Clear()
                '这个来源不需要加载器版本；MyComboBox 不是可编辑的，只能塞一个占位项而不是改 Text
                ComboCoreLoaderVersion.Items.Add(New MyComboBoxItem With {.Content = "无需加载器", .Tag = ""})
                ComboCoreLoaderVersion.SelectedIndex = 0
                ComboCoreLoaderVersion.IsEnabled = False
                LabCoreLoaderTitle.Text = "加载器版本（这个来源不需要）"
            Finally
                _CoreUiBusy = False
            End Try
            LabCoreStatus.Text = $"共 {ComboCoreGameVersion.Items.Count} 个游戏版本，选好后点「下载到实例」。"
            Return
        End If

        ComboCoreLoaderVersion.IsEnabled = True
        LabCoreLoaderTitle.Text = "加载器版本"
        LabCoreStatus.Text = "正在获取加载器版本……"
        RunInNewThread(
            Sub()
                Try
                    Dim Versions As List(Of String) = ServerCoreDownloader.GetLoaderVersions(Source, GameVersion)
                    RunInUi(
                        Sub()
                            _CoreUiBusy = True
                            Try
                                ComboCoreLoaderVersion.Items.Clear()
                                For Each Version As String In Versions
                                    Dim Item As New MyComboBoxItem With {.Content = Version, .Tag = Version}
                                    'Forge 的版本号是 1.20.1-47.2.0，显示时去掉游戏版本前缀
                                    If Source = ServerCoreDownloader.CoreSource.Forge AndAlso Version.StartsWith(GameVersion & "-") Then
                                        Item.Content = Version.Substring(GameVersion.Length + 1)
                                    End If
                                    ComboCoreLoaderVersion.Items.Add(Item)
                                Next
                                If Versions.Count > 0 Then ComboCoreLoaderVersion.SelectedIndex = 0
                            Finally
                                _CoreUiBusy = False
                            End Try
                            ComboCoreLoaderVersion_SelectionChanged(Nothing, Nothing)
                        End Sub)
                Catch ex As Exception
                    Logger.Error(ex, "获取加载器版本列表失败")
                    RunInUi(Sub()
                                LabCoreStatus.Text = "获取加载器版本失败：" & ex.Message
                            End Sub)
                End Try
            End Sub, "Load Core Loader Versions")
    End Sub

    Private Sub ComboCoreLoaderVersion_SelectionChanged(sender As Object, e As SelectionChangedEventArgs)
        If _CoreUiBusy Then Return
        Dim Source As ServerCoreDownloader.CoreSource = SelectedSource()
        If Not ServerCoreDownloader.NeedsLoader(Source) Then Return
        LabCoreStatus.Text = $"游戏版本 {ComboText(ComboCoreGameVersion)} · 加载器版本 {ComboText(ComboCoreLoaderVersion)}，点「下载到实例」开始。"
    End Sub

    Private Sub BtnCoreDownload_Click(sender As Object, e As MouseButtonEventArgs)
        Dim Source As ServerCoreDownloader.CoreSource = SelectedSource()
        Dim GameVersion As String = ComboText(ComboCoreGameVersion)
        Dim LoaderVersion As String = ""
        If ServerCoreDownloader.NeedsLoader(Source) Then
            Dim Item As MyComboBoxItem = TryCast(ComboCoreLoaderVersion.SelectedItem, MyComboBoxItem)
            If Item Is Nothing Then
                Hint("请先选一个加载器版本", HintType.Red, False)
                Return
            End If
            LoaderVersion = If(Item.Tag?.ToString(), Item.Content?.ToString()).Trim()
        End If
        If GameVersion = "" Then
            Hint("请先选一个游戏版本", HintType.Red, False)
            Return
        End If
        Dim TargetDir As String = ServerManager.Dir
        If TargetDir = "" Then
            '没有选择过服务端文件夹时，核心默认下载到 启动器目录\XingYue\Server
            Try
                Directory.CreateDirectory(ServerManager.DataDir)
                TargetDir = ServerManager.DataDir.TrimEnd("\"c)
                ServerManager.Dir = TargetDir
            Catch ex As Exception
                Hint("无法创建默认服务端目录：" & ex.Message, HintType.Red, False)
                Return
            End Try
        End If
        BtnCoreDownload.IsEnabled = False
        LabCoreStatus.Text = "开始下载 " & GameVersion & " " & LoaderVersion & " ……"
        RunInNewThread(
            Sub()
                Try
                    ServerCoreDownloader.Download(Source, GameVersion, LoaderVersion, TargetDir,
                        Sub(Message As String) RunInUi(Sub() LabCoreStatus.Text = Message))
                    RunInUi(
                        Sub()
                            BtnCoreDownload.IsEnabled = True
                            RefreshSettingsState()
                            Hint($"已下载 {GameVersion} 的服务端核心：{ServerManager.Core}")
                        End Sub)
                Catch ex As Exception
                    Logger.Error(ex, "下载服务端核心失败")
                    RunInUi(
                        Sub()
                            BtnCoreDownload.IsEnabled = True
                            LabCoreStatus.Text = "下载失败：" & ex.Message
                            Hint("下载失败：" & ex.Message, HintType.Red, False)
                        End Sub)
                End Try
            End Sub, "Download Server Core")
    End Sub

End Class
