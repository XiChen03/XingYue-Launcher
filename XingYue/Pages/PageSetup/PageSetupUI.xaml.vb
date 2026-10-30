Public Class PageSetupUI

    Private Sub PageSetupUI_Loaded(sender As Object, e As RoutedEventArgs) Handles Me.Loaded
        '重复加载部分
        PanBack.ScrollToHome()
        ThemeCheckAll(True)
        If ThemeDontClick <> 0 Then
            Dim NewText As String = Nothing
            Select Case ThemeDontClick
                Case 1
                    NewText = "眼瞎白"
                Case 2
                    NewText = "真·滑稽彩"
            End Select
            For Each Control In PanLauncherTheme.Children
                If (TypeOf Control Is MyRadioBox) AndAlso CType(Control, MyRadioBox).IsEnabled Then CType(Control, MyRadioBox).Text = NewText
            Next
        End If

        AniControlEnabled += 1
        Refresh() '#4826
        AniControlEnabled -= 1

        '非重复加载部分
        Static Reloaded As Boolean = False
        If Reloaded Then Return
        Reloaded = True

        SliderLoad()

    End Sub
    Public Sub Refresh()
        Try
            SettingService.RefreshSettings(Me)
            BackgroundRefresh(False, False)

            '标题栏
            CheckLogoLeft.Visibility = If(RadioLogoType0.Checked, Visibility.Visible, Visibility.Collapsed)
            PanLogoText.Visibility = If(RadioLogoType2.Checked, Visibility.Visible, Visibility.Collapsed)
            PanLogoChange.Visibility = If(RadioLogoType3.Checked, Visibility.Visible, Visibility.Collapsed)

            '背景音乐
            MusicRefreshUI()

            '主页
            OnMainPageTypeChanged()

            '主页小站与指令
            RefreshHomepageEditor()
        Catch ex As NullReferenceException
            Logger.Error(ex, "个性化设置项存在异常，已被自动重置", LogBehavior.Alert)
            Reset()
        Catch ex As Exception
            Logger.Error(ex, "重载个性化设置时出错")
        End Try
    End Sub
    Public Sub Reset()
        Try
            SettingService.ResetSettings(Me)
            Logger.Info("已初始化个性化设置！")
            Hint("已初始化个性化设置", HintType.Green, False)
        Catch ex As Exception
            Logger.Error(ex, "初始化个性化设置失败", LogBehavior.Alert)
        End Try
        Refresh()
    End Sub

    '背景图片
    Private Sub BtnUIBgOpen_Click(sender As Object, e As EventArgs) Handles BtnBackgroundOpen.Click
        OpenExplorer(Paths.Base & "XingYue\Pictures\")
    End Sub
    Private Sub BtnBackgroundRefresh_Click(sender As Object, e As EventArgs) Handles BtnBackgroundRefresh.Click
        BackgroundRefresh(True, True)
    End Sub
    Public Sub BackgroundRefreshUI(Show As Boolean, Count As Integer)
        If IsNothing(PanBackgroundOpacity) Then Return
        If Show Then
            PanBackgroundOpacity.Visibility = Visibility.Visible
            PanBackgroundBlur.Visibility = Visibility.Visible
            PanBackgroundSuit.Visibility = Visibility.Visible
            BtnBackgroundClear.Visibility = Visibility.Visible
            CardBackground.Title = "背景图片（" & Count & " 张）"
        Else
            PanBackgroundOpacity.Visibility = Visibility.Collapsed
            PanBackgroundBlur.Visibility = Visibility.Collapsed
            PanBackgroundSuit.Visibility = Visibility.Collapsed
            BtnBackgroundClear.Visibility = Visibility.Collapsed
            CardBackground.Title = "背景图片"
        End If
        CardBackground.TriggerForceResize()
    End Sub
    Private Sub BtnBackgroundClear_Click(sender As Object, e As EventArgs) Handles BtnBackgroundClear.Click
        If MyMsgBox("即将删除背景图片文件夹中的所有文件。" & vbCrLf & "此操作不可撤销，是否确定？", "警告",, "取消", IsWarn:=True) = 1 Then
            DirectoryUtils.Delete(Paths.Base & "XingYue\Pictures")
            BackgroundRefresh(False, True)
            Hint("背景图片已清空！", HintType.Green)
        End If
    End Sub
    ''' <summary>
    ''' 刷新背景图片及设置页 UI。
    ''' </summary>
    ''' <param name="IsHint">是否显示刷新提示。</param>
    ''' <param name="Refresh">是否刷新图片显示。</param>
    Public Shared Sub BackgroundRefresh(IsHint As Boolean, Refresh As Boolean)
        Try

            '获取可用的图片文件
            DirectoryUtils.Create(Paths.Base & "XingYue\Pictures\")
            Dim Pic As New List(Of String)
            For Each File In DirectoryUtils.EnumerateFiles(Paths.Base & "XingYue\Pictures\", True)
                Dim Extension As String = PathUtils.GetExtension(File)
                If Extension <> "ini" AndAlso Extension <> "db" Then Pic.Add(File) '文件夹可能会被加入 .ini 和 thumbs.db
            Next
            '加载
            If Not Pic.Any() Then
                If Refresh Then
                    If FrmMain.ImgBack.Visibility = Visibility.Collapsed Then
                        If IsHint Then Hint("未检测到可用背景图片！", HintType.Red)
                    Else
                        FrmMain.ImgBack.Visibility = Visibility.Collapsed
                        If IsHint Then Hint("背景图片已清除！", HintType.Green)
                    End If
                End If
                If Not IsNothing(FrmSetupUI) Then FrmSetupUI.BackgroundRefreshUI(False, 0)
            Else
                If Refresh Then
                    Dim Address As String = RandomOne(Pic)
                    Try
                        Logger.Info($"加载背景图片：{Address}")
                        FrmMain.ImgBack.Background = New MyBitmap(Address)
                        FrmMain.ImgBack.Visibility = Visibility.Visible
                        FrmMain.UpdateBackgroundAndTitleBar()
                        If IsHint Then Hint("背景图片已刷新：" & PathUtils.GetLastPart(Address), HintType.Green, False)
                    Catch ex As Exception
                        If ex.Message.Contains("参数无效") Then
                            Logger.Error($"刷新背景图片失败，该图片文件可能并非标准格式。{vbCrLf}你可以尝试使用画图打开该文件并重新保存，这会让图片变为标准格式。{vbCrLf}文件：{Address}", LogBehavior.Alert)
                        Else
                            Logger.Error(ex, $"刷新背景图片失败（{Address}）", LogBehavior.Alert)
                        End If
                    End Try
                End If
                If Not IsNothing(FrmSetupUI) Then FrmSetupUI.BackgroundRefreshUI(True, Pic.Count)
            End If

        Catch ex As Exception
            Logger.Error(ex, "刷新背景图片时出现未知错误")
        End Try
    End Sub

    '顶部栏
    Private Sub BtnLogoChange_Click(sender As Object, e As EventArgs) Handles BtnLogoChange.Click
        Dim FileName As String = Dialogs.SelectFile("选择图片", False, filter:={({"png", "jpg", "jpeg", "gif", "webp"}, "常用图片文件")}).FirstOrDefault()
        If String.IsNullOrEmpty(FileName) Then Return
        Dim TargetPath As String = Paths.Base & "XingYue\Logo.png"
        Try
            '复制文件
            FileUtils.Copy(FileName, TargetPath)
            '设置当前显示
            FrmMain.ImageTitleLogo.Source = Nothing '防止因为 Source 属性前后的值相同而不更新 (#5628)
            FrmMain.ImageTitleLogo.Source = TargetPath
        Catch ex As Exception
            If ex.Message.Contains("参数无效") Then
                Logger.Error($"改变标题栏图片失败，该图片文件可能并非标准格式。{vbCrLf}你可以尝试使用画图打开该文件并重新保存，这会让图片变为标准格式。{vbCrLf}文件：{TargetPath}", LogBehavior.Alert)
            Else
                Logger.Error(ex, "设置标题栏图片失败", LogBehavior.Alert)
            End If
            FrmMain.ImageTitleLogo.Source = Nothing
        End Try
    End Sub
    Private Sub RadioLogoType3_Check(sender As Object, e As RouteEventArgs) Handles RadioLogoType3.PreviewCheck
        If Not (AniControlEnabled = 0 AndAlso e.RaiseByMouse) Then Return
Refresh:
        '已有图片则不再选择
        Dim TargetPath As String = Paths.Base & "XingYue\Logo.png"
        If FileUtils.Exists(TargetPath) Then
            Try
                FrmMain.ImageTitleLogo.Source = Nothing '防止因为 Source 属性前后的值相同而不更新 (#5628)
                FrmMain.ImageTitleLogo.Source = TargetPath
            Catch ex As Exception
                If ex.Message.Contains("参数无效") Then
                    Logger.Error($"改变标题栏图片失败，该图片文件可能并非标准格式。{vbCrLf}你可以尝试使用画图打开该文件并重新保存，这会让图片变为标准格式。{vbCrLf}文件：{TargetPath}", LogBehavior.Alert)
                Else
                    Logger.Error(ex, "调整标题栏图片失败", LogBehavior.Alert)
                End If
                FrmMain.ImageTitleLogo.Source = Nothing
                e.Handled = True
                Try
                    FileUtils.Delete(TargetPath)
                Catch exx As Exception
                    Logger.Error(exx, "清理错误的标题栏图片失败", LogBehavior.Alert)
                End Try
            End Try
            Return
        End If
        '没有图片则要求选择
        Dim FileName As String = Dialogs.SelectFile("选择图片", False, filter:={({"png", "jpeg", "jpg", "gif", "webp"}, "常用图片文件")}).FirstOrDefault()
        If String.IsNullOrEmpty(FileName) Then
            FrmMain.ImageTitleLogo.Source = Nothing
            e.Handled = True
        Else
            Try
                FileUtils.Copy(FileName, TargetPath)
                GoTo Refresh
            Catch ex As Exception
                Logger.Error(ex, "复制标题栏图片失败", LogBehavior.Alert)
            End Try
        End If
    End Sub
    Private Sub BtnLogoDelete_Click(sender As Object, e As EventArgs) Handles BtnLogoDelete.Click
        Try
            FileUtils.Delete(Paths.Base & "XingYue\Logo.png")
            RadioLogoType1.SetChecked(True, True)
            Hint("标题栏图片已清空！", HintType.Green)
        Catch ex As Exception
            Logger.Error(ex, "清空标题栏图片失败", LogBehavior.Alert)
        End Try
    End Sub

    '背景音乐
    Private Sub BtnMusicOpen_Click(sender As Object, e As EventArgs) Handles BtnMusicOpen.Click
        OpenExplorer(Paths.Base & "XingYue\Musics\")
    End Sub
    Private Sub BtnMusicRefresh_Click(sender As Object, e As EventArgs) Handles BtnMusicRefresh.Click
        MusicRefreshPlay(True)
    End Sub
    Public Sub MusicRefreshUI()
        If PanBackgroundOpacity Is Nothing Then Return
        If MusicAllList.Any Then
            PanMusicVolume.Visibility = Visibility.Visible
            PanMusicDetail.Visibility = Visibility.Visible
            BtnMusicClear.Visibility = Visibility.Visible
            CardMusic.Title = "背景音乐（" &
                DirectoryUtils.EnumerateFiles(Paths.Base & "XingYue\Musics\", True).Count(Function(f) Not {"ini", "jpg", "txt", "cfg", "lrc", "db", "png"}.Contains(PathUtils.GetExtension(f))) &
                " 首）"
        Else
            PanMusicVolume.Visibility = Visibility.Collapsed
            PanMusicDetail.Visibility = Visibility.Collapsed
            BtnMusicClear.Visibility = Visibility.Collapsed
            CardMusic.Title = "背景音乐"
        End If
        CardMusic.TriggerForceResize()
    End Sub
    Private Sub BtnMusicClear_Click(sender As Object, e As EventArgs) Handles BtnMusicClear.Click
        If MyMsgBox("即将删除背景音乐文件夹中的所有文件。" & vbCrLf & "此操作不可撤销，是否确定？", "警告",, "取消", IsWarn:=True) = 1 Then
            RunInThread(
            Sub()
                Hint("正在删除背景音乐……")
                '停止播放音乐
                MusicNAudio = Nothing
                MusicWaitingList = New List(Of String)
                MusicAllList = New List(Of String)
                Thread.Sleep(200)
                '删除文件
                Try
                    DirectoryUtils.Delete(Paths.Base & "XingYue\Musics")
                    Hint("背景音乐已删除！", HintType.Green)
                Catch ex As Exception
                    Logger.Error(ex, "删除背景音乐失败", LogBehavior.Alert)
                End Try
                Try
                    DirectoryUtils.Create(Paths.Base & "XingYue\Musics\")
                    RunInUi(Sub() MusicRefreshPlay(False))
                Catch ex As Exception
                    Logger.Error(ex, "重建背景音乐文件夹失败", LogBehavior.Alert)
                End Try
            End Sub)
        End If
    End Sub
    Private Sub CheckMusicStart_Change() Handles CheckMusicStart.Change
        If AniControlEnabled <> 0 Then Return
        If CheckMusicStart.Checked Then CheckMusicStop.Checked = False
    End Sub
    Private Sub CheckMusicStop_Change() Handles CheckMusicStop.Change
        If AniControlEnabled <> 0 Then Return
        If CheckMusicStop.Checked Then CheckMusicStart.Checked = False
    End Sub

    '主页
    Private Sub BtnCustomFile_Click(sender As Object, e As EventArgs) Handles BtnCustomFile.Click
        Try
            If FileUtils.Exists(Paths.Base & "XingYue\Custom.xaml") Then
                If MyMsgBox("当前已存在布局文件，继续生成教学文件将会覆盖现有布局文件！", "覆盖确认", "继续", "取消", IsWarn:=True) = 2 Then Return
            End If
            ExtractResources(Paths.Base & "XingYue\Custom.xaml", "Custom")
            Hint("教学文件已生成！", HintType.Green)
            OpenExplorer(Paths.Base & "XingYue\Custom.xaml")
        Catch ex As Exception
            Logger.Error(ex, "生成教学文件失败")
        End Try
    End Sub
    Private Sub BtnCustomRefresh_Click() Handles BtnCustomRefresh.Click
        FrmLaunchRight.ForceRefresh()
        Hint("已刷新主页！", HintType.Green)
    End Sub
    Private Sub BtnCustomTutorial_Click(sender As Object, e As EventArgs) Handles BtnCustomTutorial.Click
        MyMsgBox("1. 点击 生成教学文件 按钮，这会在XingYue Launcher文件夹下生成 Custom.xaml 布局文件。" & vbCrLf &
                 "2. 使用记事本等工具打开这个文件并进行修改，修改完记得保存。" & vbCrLf &
                 "3. 点击 刷新主页 按钮，查看主页现在长啥样了。" & vbCrLf &
                 vbCrLf &
                 "你可以在生成教学文件后直接刷新主页，对照着进行修改，更有助于理解。" & vbCrLf &
                 "直接将主页文件拖进XingYue Launcher窗口也可以快捷加载。", "主页自定义教程")
    End Sub
    Public Shared Sub OnMainPageTypeChanged()
        If FrmSetupUI Is Nothing Then Return
        Select Case CInt(Settings.Get(Of Integer)("UiCustomType"))
            Case 0 '无
                FrmSetupUI.PanCustomPreset.Visibility = Visibility.Collapsed
                FrmSetupUI.PanCustomLocal.Visibility = Visibility.Collapsed
                FrmSetupUI.PanCustomNet.Visibility = Visibility.Collapsed
                FrmSetupUI.HintCustom.Visibility = Visibility.Collapsed
                FrmSetupUI.HintCustomWarn.Visibility = Visibility.Collapsed
            Case 1 '本地
                FrmSetupUI.PanCustomPreset.Visibility = Visibility.Collapsed
                FrmSetupUI.PanCustomLocal.Visibility = Visibility.Visible
                FrmSetupUI.PanCustomNet.Visibility = Visibility.Collapsed
                FrmSetupUI.HintCustom.Visibility = Visibility.Visible
                FrmSetupUI.HintCustomWarn.Visibility = If(Settings.Get(Of Boolean)("HintCustomWarn"), Visibility.Collapsed, Visibility.Visible)
                FrmSetupUI.HintCustom.Text = $"从XingYue Launcher文件夹下的 Custom.xaml 读取主页内容。{vbCrLf}你可以手动编辑该文件，向主页添加文本、图片、常用网站、快捷启动等功能。"
                CustomEventService.SetEventType(FrmSetupUI.HintCustom, CustomEvent.EventType.None)
            Case 2 '联网
                FrmSetupUI.PanCustomPreset.Visibility = Visibility.Collapsed
                FrmSetupUI.PanCustomLocal.Visibility = Visibility.Collapsed
                FrmSetupUI.PanCustomNet.Visibility = Visibility.Visible
                FrmSetupUI.HintCustom.Visibility = Visibility.Visible
                FrmSetupUI.HintCustomWarn.Visibility = If(Settings.Get(Of Boolean)("HintCustomWarn"), Visibility.Collapsed, Visibility.Visible)
                FrmSetupUI.HintCustom.Text = $"从指定网址联网获取主页内容。服主也可以用于动态更新服务器公告。"
                CustomEventService.SetEventType(FrmSetupUI.HintCustom, CustomEvent.EventType.None)
            Case 3 '预设
                FrmSetupUI.PanCustomPreset.Visibility = Visibility.Visible
                FrmSetupUI.PanCustomLocal.Visibility = Visibility.Collapsed
                FrmSetupUI.PanCustomNet.Visibility = Visibility.Collapsed
                FrmSetupUI.HintCustom.Visibility = Visibility.Collapsed
                FrmSetupUI.HintCustomWarn.Visibility = Visibility.Collapsed
        End Select
        FrmSetupUI.CardCustom.TriggerForceResize()
    End Sub
    Private Sub BtnCustomFolder_Click(sender As Object, e As MouseButtonEventArgs) Handles BtnCustomFolder.Click
        Dim CustomPath As String = Paths.Base & "XingYue\Custom.xaml"
        If FileUtils.Exists(CustomPath) Then
            OpenExplorer(CustomPath)
        Else
            OpenExplorer(Paths.Base & "XingYue\")
        End If
    End Sub

    '主题自定义
    Private Sub RadioLauncherTheme14_Change(sender As Object, e As RouteEventArgs) Handles RadioLauncherTheme14.Changed
        If RadioLauncherTheme14.Checked Then
            If LabLauncherHue.Visibility = Visibility.Visible Then Return
            LabLauncherHue.Visibility = Visibility.Visible
            SliderLauncherHue.Visibility = Visibility.Visible
            LabLauncherSat.Visibility = Visibility.Visible
            SliderLauncherSat.Visibility = Visibility.Visible
            LabLauncherDelta.Visibility = Visibility.Visible
            SliderLauncherDelta.Visibility = Visibility.Visible
            LabLauncherLight.Visibility = Visibility.Visible
            SliderLauncherLight.Visibility = Visibility.Visible
        Else
            If LabLauncherHue.Visibility = Visibility.Collapsed Then Return
            LabLauncherHue.Visibility = Visibility.Collapsed
            SliderLauncherHue.Visibility = Visibility.Collapsed
            LabLauncherSat.Visibility = Visibility.Collapsed
            SliderLauncherSat.Visibility = Visibility.Collapsed
            LabLauncherDelta.Visibility = Visibility.Collapsed
            SliderLauncherDelta.Visibility = Visibility.Collapsed
            LabLauncherLight.Visibility = Visibility.Collapsed
            SliderLauncherLight.Visibility = Visibility.Collapsed
        End If
        CardLauncher.TriggerForceResize()
    End Sub
    Private Sub HSL_Change() Handles SliderLauncherHue.Change, SliderLauncherLight.Change, SliderLauncherSat.Change, SliderLauncherDelta.Change
        If AniControlEnabled <> 0 OrElse SliderLauncherSat Is Nothing OrElse Not SliderLauncherSat.IsLoaded Then Return
        ThemeRefresh()
    End Sub

#Region "功能隐藏"

    Private Shared _HiddenForceShow As Boolean = False
    ''' <summary>
    ''' 是否强制显示被禁用的功能。
    ''' </summary>
    Public Shared Property HiddenForceShow As Boolean
        Get
            Return _HiddenForceShow
        End Get
        Set(value As Boolean)
            _HiddenForceShow = value
            HiddenRefresh()
        End Set
    End Property

    ''' <summary>
    ''' 更新功能隐藏带来的显示变化。
    ''' </summary>
    Public Shared Sub HiddenRefresh() Handles Me.Loaded
        If FrmMain.PanTitleSelect Is Nothing OrElse Not FrmMain.PanTitleSelect.IsLoaded Then Return
        Try
            '顶部栏
            If Not HiddenForceShow AndAlso Settings.Get(Of Boolean)("UiHiddenPageDownload") AndAlso Settings.Get(Of Boolean)("UiHiddenPageLink") AndAlso Settings.Get(Of Boolean)("UiHiddenPageSetup") AndAlso Settings.Get(Of Boolean)("UiHiddenPageOther") Then
                '顶部栏已被全部隐藏
                FrmMain.PanTitleSelect.Visibility = Visibility.Collapsed
            Else
                '顶部栏未被全部隐藏
                FrmMain.PanTitleSelect.Visibility = Visibility.Visible
                FrmMain.BtnTitleSelect1.Visibility = If(Not HiddenForceShow AndAlso Settings.Get(Of Boolean)("UiHiddenPageDownload"), Visibility.Collapsed, Visibility.Visible)
                FrmMain.BtnTitleSelect2.Visibility = If(Not HiddenForceShow AndAlso Settings.Get(Of Boolean)("UiHiddenPageLink"), Visibility.Collapsed, Visibility.Visible)
                FrmMain.BtnTitleSelect3.Visibility = If(Not HiddenForceShow AndAlso Settings.Get(Of Boolean)("UiHiddenPageSetup"), Visibility.Collapsed, Visibility.Visible)
                FrmMain.BtnTitleSelect4.Visibility = If(Not HiddenForceShow AndAlso Settings.Get(Of Boolean)("UiHiddenPageOther"), Visibility.Collapsed, Visibility.Visible)
            End If
            '功能
            FrmLaunchLeft.RefreshButtonsUI()
            If FrmSetupUI IsNot Nothing Then
                FrmSetupUI.CardSwitch.Visibility = If(Not HiddenForceShow AndAlso Settings.Get(Of Boolean)("UiHiddenFunctionHidden"), Visibility.Collapsed, Visibility.Visible)
            End If
            '版本设置子页面
            If FrmInstanceLeft IsNot Nothing Then
                FrmInstanceLeft.ItemModify.Visibility = If(Not HiddenForceShow AndAlso Settings.Get(Of Boolean)("UiHiddenFunctionInstanceModify"), Visibility.Collapsed, Visibility.Visible)
            End If
            '设置子页面
            If FrmSetupLeft IsNot Nothing Then
                FrmSetupLeft.ItemLaunch.Visibility = If(Not HiddenForceShow AndAlso Settings.Get(Of Boolean)("UiHiddenSetupLaunch"), Visibility.Collapsed, Visibility.Visible)
                FrmSetupLeft.ItemUI.Visibility = If(Not HiddenForceShow AndAlso Settings.Get(Of Boolean)("UiHiddenSetupUi"), Visibility.Collapsed, Visibility.Visible)
                FrmSetupLeft.ItemLink.Visibility = If(Not HiddenForceShow AndAlso Settings.Get(Of Boolean)("UiHiddenSetupLink"), Visibility.Collapsed, Visibility.Visible)
                FrmSetupLeft.ItemSystem.Visibility = If(Not HiddenForceShow AndAlso Settings.Get(Of Boolean)("UiHiddenSetupSystem"), Visibility.Collapsed, Visibility.Visible)
                '隐藏左边选择卡
                Dim AvaliableCount As Integer = 0
                If Not Settings.Get(Of Boolean)("UiHiddenSetupLaunch") Then AvaliableCount += 1
                If Not Settings.Get(Of Boolean)("UiHiddenSetupUi") Then AvaliableCount += 1
                If Not Settings.Get(Of Boolean)("UiHiddenSetupLink") Then AvaliableCount += 1
                If Not Settings.Get(Of Boolean)("UiHiddenSetupSystem") Then AvaliableCount += 1
                FrmSetupLeft.PanItem.Visibility = If(AvaliableCount < 2 AndAlso Not HiddenForceShow, Visibility.Collapsed, Visibility.Visible)
            End If
            '更多子页面
            Dim OtherAvaliableCount As Integer = 0
            If Not Settings.Get(Of Boolean)("UiHiddenOtherServer") Then OtherAvaliableCount += 1
            If Not Settings.Get(Of Boolean)("UiHiddenOtherTest") Then OtherAvaliableCount += 1
            If Not Settings.Get(Of Boolean)("UiHiddenOtherHelp") Then OtherAvaliableCount += 1
            If FrmOtherLeft IsNot Nothing Then
                FrmOtherLeft.ItemServer.Visibility = If(Not HiddenForceShow AndAlso Settings.Get(Of Boolean)("UiHiddenOtherServer"), Visibility.Collapsed, Visibility.Visible)
                FrmOtherLeft.ItemTest.Visibility = If(Not HiddenForceShow AndAlso Settings.Get(Of Boolean)("UiHiddenOtherTest"), Visibility.Collapsed, Visibility.Visible)
                FrmOtherLeft.ItemHelp.Visibility = If(Not HiddenForceShow AndAlso Settings.Get(Of Boolean)("UiHiddenOtherHelp"), Visibility.Collapsed, Visibility.Visible)
                '隐藏左边选择卡
                FrmOtherLeft.PanItem.Visibility = If(OtherAvaliableCount < 2 AndAlso Not HiddenForceShow, Visibility.Collapsed, Visibility.Visible)
            End If
            If OtherAvaliableCount = 1 AndAlso Not HiddenForceShow Then
                If Not Settings.Get(Of Boolean)("UiHiddenOtherServer") Then
                    FrmMain.BtnTitleSelect4.Text = "开服器"
                ElseIf Not Settings.Get(Of Boolean)("UiHiddenOtherTest") Then
                    FrmMain.BtnTitleSelect4.Text = "百宝箱"
                Else
                    FrmMain.BtnTitleSelect4.Text = "帮助"
                End If
            Else
                FrmMain.BtnTitleSelect4.Text = "更多"
            End If
            '各个页面的入口
            If FrmMain.PageCurrent = FormMain.PageType.InstanceSelect Then FrmSelectRight.BtnEmptyDownload_Loaded()
            If FrmMain.PageCurrent = FormMain.PageType.Launch Then FrmLaunchLeft.RefreshButtonsUI()
            If FrmMain.PageCurrent = FormMain.PageType.InstanceSetup AndAlso FrmInstanceModDisabled IsNot Nothing Then FrmInstanceModDisabled.BtnDownload_Loaded()
            '备注
            If FrmSetupUI IsNot Nothing Then FrmSetupUI.CardSwitch.Title = If(HiddenForceShow, "功能隐藏（已暂时关闭，按 F12 以重新启用）", "功能隐藏")
        Catch ex As Exception
            Logger.Error(ex, "刷新功能隐藏项目失败")
        End Try
    End Sub

    'UI 协同改变
    Private Sub HiddenSetupMain() Handles CheckHiddenPageSetup.Change
        '设置主页面
        If CheckHiddenPageSetup.Checked Then
            '开启
            CheckHiddenSetupLaunch.Checked = True
            CheckHiddenSetupSystem.Checked = True
            CheckHiddenSetupLink.Checked = True
            CheckHiddenSetupUI.Checked = True
        Else
            '关闭
            If Settings.Get(Of Boolean)("UiHiddenSetupLaunch") AndAlso Settings.Get(Of Boolean)("UiHiddenSetupUi") AndAlso Settings.Get(Of Boolean)("UiHiddenSetupSystem") AndAlso Settings.Get(Of Boolean)("UiHiddenSetupLink") Then
                CheckHiddenSetupLaunch.Checked = False
                CheckHiddenSetupSystem.Checked = False
                CheckHiddenSetupLink.Checked = False
                CheckHiddenSetupUI.Checked = False
            End If
        End If
    End Sub
    Private Sub HiddenSetupSub() Handles CheckHiddenSetupLaunch.Change, CheckHiddenSetupSystem.Change, CheckHiddenSetupLink.Change, CheckHiddenSetupUI.Change
        '设置子页面
        If Settings.Get(Of Boolean)("UiHiddenSetupLaunch") AndAlso Settings.Get(Of Boolean)("UiHiddenSetupUi") AndAlso Settings.Get(Of Boolean)("UiHiddenSetupSystem") AndAlso Settings.Get(Of Boolean)("UiHiddenSetupLink") Then
            '已被全部隐藏
            CheckHiddenPageSetup.Checked = True
        Else
            '未被全部隐藏
            CheckHiddenPageSetup.Checked = False
        End If
    End Sub
    Private Sub HiddenOtherMain() Handles CheckHiddenPageOther.Change
        '更多主页面
        If CheckHiddenPageOther.Checked Then
            '开启
            CheckHiddenOtherServer.Checked = True
            CheckHiddenOtherTest.Checked = True
            CheckHiddenOtherHelp.Checked = True
        Else
            '关闭
            If Settings.Get(Of Boolean)("UiHiddenOtherServer") AndAlso Settings.Get(Of Boolean)("UiHiddenOtherTest") AndAlso Settings.Get(Of Boolean)("UiHiddenOtherHelp") Then
                CheckHiddenOtherServer.Checked = False
                CheckHiddenOtherTest.Checked = False
                CheckHiddenOtherHelp.Checked = False
            End If
        End If
    End Sub
    Private Sub HiddenOtherSub() Handles CheckHiddenOtherServer.Change, CheckHiddenOtherTest.Change, CheckHiddenOtherHelp.Change
        '更多子页面
        If Settings.Get(Of Boolean)("UiHiddenOtherServer") AndAlso Settings.Get(Of Boolean)("UiHiddenOtherTest") AndAlso Settings.Get(Of Boolean)("UiHiddenOtherHelp") Then
            '已被全部隐藏
            CheckHiddenPageOther.Checked = True
        Else
            '未被全部隐藏
            CheckHiddenPageOther.Checked = False
        End If
    End Sub

    '警告提示
    Private Sub HiddenHint(sender As Object, user As Boolean) Handles CheckHiddenFunctionHidden.Change, CheckHiddenPageSetup.Change, CheckHiddenSetupUI.Change
        If AniControlEnabled = 0 AndAlso sender.Checked Then Hint("按 F12 即可暂时关闭功能隐藏设置。千万别忘了，要不然设置就改不回来了……")
    End Sub

#End Region

    '滑动条
    Private Sub SliderLoad()
        SliderMusicVolume.GetHintText = Function(v) Math.Ceiling(v * 0.1) & "%"
        SliderLauncherTransparent.GetHintText = Function(v) Math.Round(40 + v * 0.1) & "%"
        SliderLauncherHue.GetHintText = Function(v) v & "°"
        SliderLauncherSat.GetHintText = Function(v) v & "%"
        SliderLauncherDelta.GetHintText =
        Function(Value As Integer) As String
            If Value > 90 Then
                Return "+" & (Value - 90)
            ElseIf Value = 90 Then
                Return 0
            Else
                Return Value - 90
            End If
        End Function
        SliderLauncherLight.GetHintText =
        Function(Value As Integer) As String
            If Value > 20 Then
                Return "+" & (Value - 20)
            ElseIf Value = 20 Then
                Return 0
            Else
                Return Value - 20
            End If
        End Function
        SliderBackgroundOpacity.GetHintText = Function(v) Math.Round(v * 0.1) & "%"
        SliderBackgroundBlur.GetHintText = Function(v) v & " 像素"
    End Sub

#Region "主页小站与指令"

    ''' <summary>正在编辑的主页小站 / 指令配置。</summary>
    Private _Homepage As HomepageEdit

    ''' <summary>重建「主页小站与指令」卡片的界面。</summary>
    Public Sub RefreshHomepageEditor()
        If IsNothing(PanEditSites) Then Return
        If _Homepage Is Nothing Then _Homepage = HomepageEdit.Load()

        If ComboCmdType.Items.Count = 0 Then
            For Each Name As String In {"复制文本", "执行命令", "打开网页"}
                ComboCmdType.Items.Add(New MyComboBoxItem With {.Content = Name})
            Next
        End If
        If ComboCmdType.SelectedIndex < 0 Then ComboCmdType.SelectedIndex = 0

        PanEditSites.Children.Clear()
        If _Homepage.Sites.Count = 0 Then
            PanEditSites.Children.Add(New TextBlock With {
                .Text = "还没有添加任何小站。", .Margin = New Thickness(0, 2, 0, 2), .FontSize = 12})
        Else
            For Each Entry As HomepageEdit.SiteEntry In _Homepage.Sites
                PanEditSites.Children.Add(BuildSiteRow(Entry))
            Next
        End If

        PanEditCommands.Children.Clear()
        If _Homepage.Commands.Count = 0 Then
            PanEditCommands.Children.Add(New TextBlock With {
                .Text = "还没有添加任何指令。", .Margin = New Thickness(0, 2, 0, 2), .FontSize = 12})
        Else
            For Each Entry As HomepageEdit.CommandEntry In _Homepage.Commands
                PanEditCommands.Children.Add(BuildCommandRow(Entry))
            Next
        End If
    End Sub

    Private Function BuildSiteRow(Entry As HomepageEdit.SiteEntry) As FrameworkElement
        Dim Row As New Grid With {.Margin = New Thickness(0, 0, 0, 6)}
        Row.ColumnDefinitions.Add(New ColumnDefinition With {.Width = New GridLength(1.1, GridUnitType.Star)})
        Row.ColumnDefinitions.Add(New ColumnDefinition With {.Width = New GridLength(8)})
        Row.ColumnDefinitions.Add(New ColumnDefinition With {.Width = New GridLength(1, GridUnitType.Star)})
        Row.ColumnDefinitions.Add(New ColumnDefinition With {.Width = New GridLength(8)})
        Row.ColumnDefinitions.Add(New ColumnDefinition With {.Width = New GridLength(1.6, GridUnitType.Star)})
        Row.ColumnDefinitions.Add(New ColumnDefinition With {.Width = New GridLength(8)})
        Row.ColumnDefinitions.Add(New ColumnDefinition With {.Width = GridLength.Auto})

        Dim Title As New TextBlock With {.Text = Entry.Title, .FontSize = 12.5, .TextTrimming = TextTrimming.CharacterEllipsis, .VerticalAlignment = VerticalAlignment.Center}
        Dim Info As New TextBlock With {.Text = Entry.Info, .FontSize = 12, .TextTrimming = TextTrimming.CharacterEllipsis, .VerticalAlignment = VerticalAlignment.Center}
        Dim Url As New TextBlock With {.Text = Entry.Url, .FontSize = 12, .TextTrimming = TextTrimming.CharacterEllipsis, .VerticalAlignment = VerticalAlignment.Center}
        Grid.SetColumn(Title, 0) : Grid.SetColumn(Info, 2) : Grid.SetColumn(Url, 4)
        Row.Children.Add(Title) : Row.Children.Add(Info) : Row.Children.Add(Url)

        Dim Btn As New MyButton With {.Text = "删除", .MinWidth = 62, .Padding = New Thickness(12, 4, 12, 4),
                                      .ColorType = MyButton.ColorState.Red, .VerticalAlignment = VerticalAlignment.Center}
        AddHandler Btn.Click, Sub(sender As Object, e As MouseButtonEventArgs)
                                  _Homepage.Sites.Remove(Entry)
                                  RefreshHomepageEditor()
                              End Sub
        Grid.SetColumn(Btn, 6)
        Row.Children.Add(Btn)
        Return Row
    End Function

    Private Function BuildCommandRow(Entry As HomepageEdit.CommandEntry) As FrameworkElement
        Dim Row As New Grid With {.Margin = New Thickness(0, 0, 0, 6)}
        Row.ColumnDefinitions.Add(New ColumnDefinition With {.Width = New GridLength(1, GridUnitType.Star)})
        Row.ColumnDefinitions.Add(New ColumnDefinition With {.Width = New GridLength(8)})
        Row.ColumnDefinitions.Add(New ColumnDefinition With {.Width = New GridLength(76)})
        Row.ColumnDefinitions.Add(New ColumnDefinition With {.Width = New GridLength(8)})
        Row.ColumnDefinitions.Add(New ColumnDefinition With {.Width = New GridLength(1.6, GridUnitType.Star)})
        Row.ColumnDefinitions.Add(New ColumnDefinition With {.Width = New GridLength(8)})
        Row.ColumnDefinitions.Add(New ColumnDefinition With {.Width = GridLength.Auto})

        Dim Title As New TextBlock With {.Text = Entry.Text, .FontSize = 12.5, .TextTrimming = TextTrimming.CharacterEllipsis, .VerticalAlignment = VerticalAlignment.Center}
        Dim Kind As New TextBlock With {.Text = Entry.Type, .FontSize = 12, .VerticalAlignment = VerticalAlignment.Center}
        Dim Data As New TextBlock With {.Text = Entry.Data, .FontSize = 12, .TextTrimming = TextTrimming.CharacterEllipsis, .VerticalAlignment = VerticalAlignment.Center}
        Grid.SetColumn(Title, 0) : Grid.SetColumn(Kind, 2) : Grid.SetColumn(Data, 4)
        Row.Children.Add(Title) : Row.Children.Add(Kind) : Row.Children.Add(Data)

        Dim Btn As New MyButton With {.Text = "删除", .MinWidth = 62, .Padding = New Thickness(12, 4, 12, 4),
                                      .ColorType = MyButton.ColorState.Red, .VerticalAlignment = VerticalAlignment.Center}
        AddHandler Btn.Click, Sub(sender As Object, e As MouseButtonEventArgs)
                                  _Homepage.Commands.Remove(Entry)
                                  RefreshHomepageEditor()
                              End Sub
        Grid.SetColumn(Btn, 6)
        Row.Children.Add(Btn)
        Return Row
    End Function

    Private Sub BtnSiteAdd_Click(sender As Object, e As MouseButtonEventArgs) Handles BtnSiteAdd.Click
        Dim Title As String = TextSiteTitle.Text.Trim()
        Dim Url As String = TextSiteUrl.Text.Trim()
        If Title = "" Then
            Hint("请先填小站名称", HintType.Red, False)
            Return
        End If
        If Url = "" Then
            Hint("请先填小站网址", HintType.Red, False)
            Return
        End If
        If Not Url.Contains("://") Then Url = "https://" & Url
        _Homepage.Sites.Add(New HomepageEdit.SiteEntry With {
            .Title = Title, .Info = TextSiteInfo.Text.Trim(), .Url = Url})
        TextSiteTitle.Text = "" : TextSiteInfo.Text = "" : TextSiteUrl.Text = ""
        RefreshHomepageEditor()
        Hint("已添加小站，记得点「保存修改」", HintType.Green, False)
    End Sub

    Private Sub BtnCmdAdd_Click(sender As Object, e As MouseButtonEventArgs) Handles BtnCmdAdd.Click
        Dim Title As String = TextCmdTitle.Text.Trim()
        Dim Data As String = TextCmdData.Text.Trim()
        If Title = "" Then
            Hint("请先填按钮文字", HintType.Red, False)
            Return
        End If
        If Data = "" Then
            Hint("请先填指令内容", HintType.Red, False)
            Return
        End If
        Dim Kind As String = "复制文本"
        Dim KindItem As MyComboBoxItem = TryCast(ComboCmdType.SelectedItem, MyComboBoxItem)
        If KindItem IsNot Nothing Then Kind = KindItem.Content.ToString()
        _Homepage.Commands.Add(New HomepageEdit.CommandEntry With {
            .Text = Title, .Type = Kind, .Data = Data, .Tip = TextCmdTip.Text.Trim()})
        TextCmdTitle.Text = "" : TextCmdData.Text = "" : TextCmdTip.Text = ""
        RefreshHomepageEditor()
        Hint("已添加指令，记得点「保存修改」", HintType.Green, False)
    End Sub

    Private Sub BtnHomepageSave_Click(sender As Object, e As MouseButtonEventArgs) Handles BtnHomepageSave.Click
        Try
            _Homepage.Save()
            RefreshHomepageEditor()
            If FrmLaunchRight IsNot Nothing Then FrmLaunchRight.ForceRefresh()
            Hint("已保存，主页已刷新", HintType.Green, False)
        Catch ex As Exception
            Logger.Error(ex, "保存主页小站 / 指令失败")
            Hint("保存失败：" & ex.Message, HintType.Red, False)
        End Try
    End Sub

    Private Sub BtnHomepageReset_Click(sender As Object, e As MouseButtonEventArgs) Handles BtnHomepageReset.Click
        If MyMsgBox("要把「常去小站」和「常用指令」恢复成默认内容吗？", "恢复默认", "恢复", "取消") <> 1 Then Return
        _Homepage = HomepageEdit.Defaults()
        Try
            _Homepage.Save()
            RefreshHomepageEditor()
            If FrmLaunchRight IsNot Nothing Then FrmLaunchRight.ForceRefresh()
            Hint("已恢复默认内容", HintType.Green, False)
        Catch ex As Exception
            Logger.Error(ex, "恢复默认主页内容失败")
        End Try
    End Sub

#End Region

End Class
