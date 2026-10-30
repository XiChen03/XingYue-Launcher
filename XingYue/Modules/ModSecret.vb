'由于包含加解密等安全信息，本文件中的部分代码已被删除

Friend Module ModSecret

    '标注 XingYue Launcher 的不同分支，仅用于替换标记
    Public Const VersionBranchMain As String = "OpenSource"
    '在开源版的注册表与常规版的注册表隔离，以防数据冲突
    Public Const RegFolder As String = "XingYue"
    '用于微软登录的 ClientId
    '微软 Xbox 应用的公共 ClientId，已通过 Mojang 授权（Minecraft 服务白名单）
    Public Const OAuthClientIdPublic As String = "00000000402b5328"
    '若通过环境变量 XINGYUE_MS_CLIENT_ID 提供自建 ClientId（需自行完成 Mojang 审批），则自动改用 v2.0 端点
    Public OAuthClientId As String = If(Environment.GetEnvironmentVariable("XINGYUE_MS_CLIENT_ID"), OAuthClientIdPublic)
    '是否走 live.com 旧版设备码端点（仅配合上面的公共 ClientId 使用）
    Public ReadOnly UseLegacyMsaEndpoint As Boolean = (OAuthClientId = OAuthClientIdPublic)
    'CurseForge API Key
    '开源版不内置任何官方 Key（避免 Key 被公开滥用或被官方吊销），只使用用户自己配置的 Key：
    '优先级：环境变量 XINGYUE_CURSEFORGE_API_KEY > 设置项 CacheCurseForgeKey（设置 → 其他 → 下载源）
    '两者都没有时，CurseForge 相关请求会自动回退到镜像源，镜像源不需要 Key

    ''' <summary>
    ''' 用户自己提供的 Key（环境变量或设置项）。为空则说明用户没有配置。
    ''' </summary>
    Public ReadOnly Property CurseForgeUserAPIKey As String
        Get
            Dim Key As String = Environment.GetEnvironmentVariable("XINGYUE_CURSEFORGE_API_KEY")
            If Not String.IsNullOrWhiteSpace(Key) Then Return Key.Trim()
            Try
                Return If(Settings.Get(Of String)("CacheCurseForgeKey"), "").Trim()
            Catch ex As Exception
                Return ""
            End Try
        End Get
    End Property

    ''' <summary>
    ''' 当前请求实际使用的 Key。为空字符串时说明没有可用的 Key，调用方应改用镜像源。
    ''' </summary>
    Public ReadOnly Property CurseForgeAPIKey As String
        Get
            Return CurseForgeUserAPIKey
        End Get
    End Property
#Region "网络鉴权"

    Public Const RestrictMcDownload As Boolean = False
    Friend Function SecretCdnSign(UrlWithMark As String) As String
        If Not UrlWithMark.EndsWithF("{CDN}") Then Return UrlWithMark
        Return UrlWithMark.Replace("{CDN}", "").Replace(" ", "%20")
    End Function
    ''' <summary>
    ''' 设置 Headers 的 UA、Referer。
    ''' </summary>
    Friend Sub SecretHeadersSign(Url As String, ByRef Req As HttpRequestMessage, Optional SimulateBrowserHeaders As Boolean = False)
        If Not Req.Headers.UserAgent.Any Then
            If Url.Contains("baidupcs.com") OrElse Url.Contains("baidu.com") Then
                Req.Headers.Add("User-Agent", "LogStatistic")  '#4951
            ElseIf SimulateBrowserHeaders Then
                Req.Headers.Add("User-Agent", $"XingYue/{VersionBaseName}.{CInt(BuildType)} Mozilla/5.0 AppleWebKit/537.36 Chrome/63.0.3239.132 Safari/537.36")
            Else
                Req.Headers.Add("User-Agent", $"XingYue/{VersionBaseName}.{CInt(BuildType)}")
            End If
        End If
        If Not SimulateBrowserHeaders Then Req.Headers.Add("Referer", $"http://{VersionCode}.open.xingyue.server/")
        If Url.Contains("api.curseforge.com") OrElse Url.Contains("forgecdn.net") Then
            Dim CurseForgeKey As String = CurseForgeAPIKey
            '调用方可能已经通过 Headers 显式指定了 Key（例如校验用户输入的 Key），此时不能再加一遍，
            '否则请求头会变成 "key, key" 而被官方判定为无效
            If CurseForgeKey <> "" AndAlso Not Req.Headers.Contains("x-api-key") Then Req.Headers.Add("x-api-key", CurseForgeKey)
        End If
    End Sub

#End Region

#Region "主题"

    Public Color1 As New MyColor(52, 61, 74)
    Public Color2 As New MyColor(11, 91, 203)
    Public Color3 As New MyColor(19, 112, 243)
    Public Color4 As New MyColor(72, 144, 245)
    Public Color5 As New MyColor(150, 192, 249)
    Public Color6 As New MyColor(213, 230, 253)
    Public Color7 As New MyColor(222, 236, 253)
    Public Color8 As New MyColor(234, 242, 254)
    Public ColorBg0 As New MyColor(150, 192, 249)
    Public ColorBg1 As New MyColor(190, Color7)
    Public ColorGray1 As New MyColor(64, 64, 64)
    Public ColorGray2 As New MyColor(115, 115, 115)
    Public ColorGray3 As New MyColor(140, 140, 140)
    Public ColorGray4 As New MyColor(166, 166, 166)
    Public ColorGray5 As New MyColor(204, 204, 204)
    Public ColorGray6 As New MyColor(235, 235, 235)
    Public ColorGray7 As New MyColor(240, 240, 240)
    Public ColorGray8 As New MyColor(245, 245, 245)
    Public ColorSemiTransparent As New MyColor(1, Color8)

    Public ThemeNow As Integer = -1
    Public ColorHue As Integer = 210, ColorSat As Integer = 85, ColorLightAdjust As Integer = 0, ColorHueTopbarDelta As OneOf(Of Integer, Integer()) = 0
    Public ThemeDontClick As Integer = 0

    ''' <summary>
    ''' 各主题对应的颜色参数（色相、饱和度、亮度调整、顶栏色相偏移）。
    ''' 原版中该对照表属于保密内容，开源版需要自行补全，否则切换主题不会有任何效果。
    ''' </summary>
    Private Sub ThemeApplyPreset(Id As Integer)
        Select Case Id
            Case 0 '天空蓝
                ColorHue = 210 : ColorSat = 85 : ColorLightAdjust = 0 : ColorHueTopbarDelta = 0
            Case 1 '甜柠青
                ColorHue = 165 : ColorSat = 60 : ColorLightAdjust = 0 : ColorHueTopbarDelta = 0
            Case 2 '小草绿
                ColorHue = 110 : ColorSat = 55 : ColorLightAdjust = 0 : ColorHueTopbarDelta = 0
            Case 3 '菠萝黄
                ColorHue = 45 : ColorSat = 85 : ColorLightAdjust = 0 : ColorHueTopbarDelta = 0
            Case 4 '橡木棕
                ColorHue = 28 : ColorSat = 55 : ColorLightAdjust = 0 : ColorHueTopbarDelta = 0
            Case 5 '玄素黑
                ColorHue = 210 : ColorSat = 0 : ColorLightAdjust = 0 : ColorHueTopbarDelta = 0
            Case 6 '铁杆粉
                ColorHue = 340 : ColorSat = 60 : ColorLightAdjust = 8 : ColorHueTopbarDelta = 0
            Case 7 '神秘紫
                ColorHue = 275 : ColorSat = 55 : ColorLightAdjust = 0 : ColorHueTopbarDelta = 0
            Case 8 '秋仪金
                ColorHue = 40 : ColorSat = 75 : ColorLightAdjust = 0 : ColorHueTopbarDelta = 0
            Case 9 '活跃橙
                ColorHue = 20 : ColorSat = 80 : ColorLightAdjust = 0 : ColorHueTopbarDelta = 0
            Case 10 '跳票红
                ColorHue = 0 : ColorSat = 75 : ColorLightAdjust = 0 : ColorHueTopbarDelta = 0
            Case 11 '极客蓝
                ColorHue = 205 : ColorSat = 55 : ColorLightAdjust = 0 : ColorHueTopbarDelta = 0
            Case 12 '滑稽彩
                ColorHue = 0 : ColorSat = 70 : ColorLightAdjust = 0 : ColorHueTopbarDelta = New Integer() {-40, 0, 40}
            Case 13 '欧皇彩
                ColorHue = 40 : ColorSat = 90 : ColorLightAdjust = 0 : ColorHueTopbarDelta = New Integer() {-60, 0, 60}
            Case 14 '自定义
                ColorHue = Math.Max(0, Math.Min(360, Settings.Get(Of Integer)("UiLauncherHue")))
                ColorSat = Math.Max(0, Math.Min(100, Settings.Get(Of Integer)("UiLauncherSat")))
                ColorLightAdjust = Math.Max(-20, Math.Min(20, Settings.Get(Of Integer)("UiLauncherLight")))
                ColorHueTopbarDelta = Math.Max(0, Math.Min(180, Settings.Get(Of Integer)("UiLauncherDelta")))
            Case Else
                ColorHue = 210 : ColorSat = 85 : ColorLightAdjust = 0 : ColorHueTopbarDelta = 0
        End Select
    End Sub

    Public Sub ThemeRefresh(Optional NewTheme As Integer = -1)
        Try
            If NewTheme >= 0 Then
                If ThemeNow = NewTheme Then Return
                ThemeNow = NewTheme
                ThemeApplyPreset(NewTheme)
            ElseIf ThemeNow = 12 Then
                '滑稽彩：由主时钟每 250ms 调用一次，让色相缓缓循环
                ColorHue = (ColorHue + 4) Mod 360
            End If

            Color1 = New MyColor().FromHSL2(ColorHue, ColorSat * 0.2, 25 + ColorLightAdjust * 0.3)
            Color2 = New MyColor().FromHSL2(ColorHue, ColorSat, 45 + ColorLightAdjust)
            Color3 = New MyColor().FromHSL2(ColorHue, ColorSat, 55 + ColorLightAdjust)
            Color4 = New MyColor().FromHSL2(ColorHue, ColorSat, 65 + ColorLightAdjust)
            Color5 = New MyColor().FromHSL2(ColorHue, ColorSat, 80 + ColorLightAdjust * 0.4)
            Color6 = New MyColor().FromHSL2(ColorHue, ColorSat, 91 + ColorLightAdjust * 0.1)
            Color7 = New MyColor().FromHSL2(ColorHue, ColorSat, 95)
            Color8 = New MyColor().FromHSL2(ColorHue, ColorSat, 97)
            '铁杆粉：浅色系换成更淡的粉色（以 #FFCEE2 为基准逐级变浅）
            If ThemeNow = 6 Then
                Color5 = New MyColor(255, 206, 226) ' #FFCEE2
                Color6 = New MyColor(255, 227, 237)
                Color7 = New MyColor(255, 241, 247)
                Color8 = New MyColor(255, 249, 252)
            End If
            ColorBg0 = Color4 * 0.4 + Color5 * 0.4 + ColorGray4 * 0.2
            ColorBg1 = New MyColor(190, Color7)

            ColorSemiTransparent = New MyColor(1, Color8)
            Application.Current.Resources("ColorBrush1") = New SolidColorBrush(Color1)
            Application.Current.Resources("ColorBrush2") = New SolidColorBrush(Color2)
            Application.Current.Resources("ColorBrush3") = New SolidColorBrush(Color3)
            Application.Current.Resources("ColorBrush4") = New SolidColorBrush(Color4)
            Application.Current.Resources("ColorBrush5") = New SolidColorBrush(Color5)
            Application.Current.Resources("ColorBrush6") = New SolidColorBrush(Color6)
            Application.Current.Resources("ColorBrush7") = New SolidColorBrush(Color7)
            Application.Current.Resources("ColorBrush8") = New SolidColorBrush(Color8)
            Application.Current.Resources("ColorBrushBg0") = New SolidColorBrush(ColorBg0)
            Application.Current.Resources("ColorBrushBg1") = New SolidColorBrush(ColorBg1)
            Application.Current.Resources("ColorObject1") = CType(Color1, Color)
            Application.Current.Resources("ColorObject2") = CType(Color2, Color)
            Application.Current.Resources("ColorObject3") = CType(Color3, Color)
            Application.Current.Resources("ColorObject4") = CType(Color4, Color)
            Application.Current.Resources("ColorObject5") = CType(Color5, Color)
            Application.Current.Resources("ColorObject6") = CType(Color6, Color)
            Application.Current.Resources("ColorObject7") = CType(Color7, Color)
            Application.Current.Resources("ColorObject8") = CType(Color8, Color)
            Application.Current.Resources("ColorObjectBg0") = CType(ColorBg0, Color)
            Application.Current.Resources("ColorObjectBg1") = CType(ColorBg1, Color)
            ThemeRefreshMain()
        Catch ex As Exception
            Logger.Error(ex, "刷新主题颜色失败", LogBehavior.Toast)
        End Try
    End Sub
    Public Sub ThemeRefreshMain()
        RunInUi(
        Sub()
            If Not FrmMain.IsLoaded Then Return
            '顶部条背景
            Dim Brush = New LinearGradientBrush With {.EndPoint = New Point(1, 0), .StartPoint = New Point(0, 0)}
            Dim Deltas = ColorHueTopbarDelta.Switch(Function(d) New Integer() {-d, 0, d}, Function(d) d)
            Brush.GradientStops.Add(New GradientStop With {.Offset = 0, .Color = New MyColor().FromHSL2(ColorHue + Deltas(0), ColorSat, 48 + ColorLightAdjust)})
            Brush.GradientStops.Add(New GradientStop With {.Offset = 0.5, .Color = New MyColor().FromHSL2(ColorHue + Deltas(1), ColorSat, 54 + ColorLightAdjust)})
            Brush.GradientStops.Add(New GradientStop With {.Offset = 1, .Color = New MyColor().FromHSL2(ColorHue + Deltas(2), ColorSat, 48 + ColorLightAdjust)})
            FrmMain.PanTitle.Background = Brush
            FrmMain.PanTitle.Background.Freeze()
            '主页面背景
            If Settings.Get(Of Boolean)("UiBackgroundColorful") Then
                Brush = New LinearGradientBrush With {.EndPoint = New Point(0.1, 1), .StartPoint = New Point(0.9, 0)}
                Brush.GradientStops.Add(New GradientStop With {.Offset = -0.1, .Color = New MyColor().FromHSL2(ColorHue - 15, ColorSat * 0.8, 91)})
                Brush.GradientStops.Add(New GradientStop With {.Offset = 0.4, .Color = New MyColor().FromHSL2(ColorHue, ColorSat * 0.8, 91)})
                Brush.GradientStops.Add(New GradientStop With {.Offset = 1.1, .Color = New MyColor().FromHSL2(ColorHue + 15, ColorSat * 0.8, 91)})
                FrmMain.PanForm.Background = Brush
            Else
                FrmMain.PanForm.Background = New MyColor(245, 245, 245)
            End If
            FrmMain.PanForm.Background.Freeze()
        End Sub)
    End Sub
    Friend Sub ThemeCheckAll(EffectSetup As Boolean)
    End Sub
    Friend Function ThemeCheckOne(Id As Integer) As Boolean
        Return True
    End Function
    Friend Function ThemeUnlock(Id As Integer, Optional ShowDoubleHint As Boolean = True, Optional UnlockHint As String = Nothing) As Boolean
        Return False
    End Function

#End Region

#Region "联网配置"

    ''' <summary>
    ''' 联网获取的配置信息。
    ''' 若获取失败或仍在获取中，可能为 Nothing。
    ''' </summary>
    Public ServerConfig As JObject

    Public ServerLoader As New LoaderTask(Of Integer, Integer)("XingYue Launcher 配置更新", Sub() Logger.Info("该版本中不包含更新通知功能……"), Priority:=ThreadPriority.BelowNormal) With
        {.ReloadTimeout = 1000 * 60 * 60} '超时 1 小时

#End Region

#Region "设备识别码"

    ''' <summary>
    ''' 获取设备识别码。
    ''' </summary>
    Friend Function GetIdentify() As String
        Return "0000-0000-0000-0000"
    End Function

#End Region

End Module
