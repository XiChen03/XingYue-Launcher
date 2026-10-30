Public Class PageLinkLeft

    ''' <summary>
    ''' 当前页面的编号。从上往下从 0 开始计算。
    ''' </summary>
    Public PageID As FormMain.PageSubType = FormMain.PageSubType.LinkMain

    ''' <summary>
    ''' 勾选事件改变页面。
    ''' </summary>
    Private Sub PageCheck(sender As MyListItem, e As EventArgs) Handles ItemEasyTier.Check, ItemSakura.Check
        '尚未初始化控件属性时，sender.Tag 为 Nothing，会跳过切换
        If sender.Tag Is Nothing Then Return
        Dim ID As FormMain.PageSubType = Val(sender.Tag)
        If ID = FormMain.PageSubType.LinkSakura AndAlso Not SakuraFrp.IsLoggedIn Then
            '没登录就先弹提示，并把选中项退回 EasyTier
            ItemEasyTier.SetChecked(True, True, False)
            If MyMsgBox($"使用樱花映射需要先登录。{vbCrLf}登录入口在 设置 → 联机 → 樱花映射。", "需要登录", "前往设置", "取消") = 1 Then
                GoToSakuraSetup()
            End If
            Return
        End If
        PageChange(ID)
    End Sub

    ''' <summary>
    ''' 跳转到 设置 → 联机，并定位到樱花映射卡片。
    ''' </summary>
    Private Sub GoToSakuraSetup()
        PageSetupLink.LocateSakuraOnLoad = True
        FrmMain.PageChange(New FormMain.PageStackData With {.Page = FormMain.PageType.Setup}, FormMain.PageSubType.SetupLink)
        '若设置页早就加载好了，上面的标志不会被消费，这里再主动定位一次
        If FrmSetupLink IsNot Nothing Then FrmSetupLink.LocateSakura()
    End Sub

    ''' <summary>
    ''' 获取当前导航指定的右页面。
    ''' </summary>
    Public Function PageGet(Optional ID As FormMain.PageSubType = -1) As MyPageRight
        If ID = -1 Then ID = PageID
        Select Case ID
            Case FormMain.PageSubType.LinkSakura
                If FrmLinkSakura Is Nothing Then FrmLinkSakura = New PageLinkSakura
                Return FrmLinkSakura
            Case FormMain.PageSubType.LinkMain
                If FrmLinkMain Is Nothing Then FrmLinkMain = New PageLinkMain
                Return FrmLinkMain
            Case Else
                Throw New Exception("未知的联机子页面种类：" & ID)
        End Select
    End Function

    ''' <summary>
    ''' 切换现有页面。
    ''' </summary>
    Public Sub PageChange(ID As FormMain.PageSubType)
        If PageID = ID Then Return
        AniControlEnabled += 1
        Try
            Select Case ID
                Case FormMain.PageSubType.LinkSakura
                    PageChangeRun(PageGet(FormMain.PageSubType.LinkSakura))
                Case FormMain.PageSubType.LinkMain
                    PageChangeRun(PageGet(FormMain.PageSubType.LinkMain))
                Case Else
                    Throw New Exception("未知的联机子页面种类：" & ID)
            End Select
            PageID = ID
        Catch ex As Exception
            Logger.Error(ex, $"切换联机分页面失败（ID {ID}）")
        Finally
            AniControlEnabled -= 1
        End Try
    End Sub

    Private Shared Sub PageChangeRun(Target As MyPageRight)
        AniStop("FrmMain PageChangeRight") '停止主页面的右页面切换动画，防止它与本动画一起触发多次 PageOnEnter
        If Target.Parent IsNot Nothing Then Target.SetValue(ContentPresenter.ContentProperty, Nothing)
        FrmMain.PageRight = Target
        CType(FrmMain.PanMainRight.Child, MyPageRight).PageOnExit()
        AniStart({
            AaCode(
            Sub()
                CType(FrmMain.PanMainRight.Child, MyPageRight).PageOnForceExit()
                FrmMain.PanMainRight.Child = FrmMain.PageRight
                FrmMain.PageRight.Opacity = 0
            End Sub, 130),
            AaCode(
            Sub()
                '延迟触发页面通用动画，以使得在 Loaded 事件中加载的控件得以处理
                FrmMain.PageRight.Opacity = 1
                FrmMain.PageRight.PageOnEnter()
            End Sub, 30, True)
        }, "PageLeft PageChange")
    End Sub

End Class
