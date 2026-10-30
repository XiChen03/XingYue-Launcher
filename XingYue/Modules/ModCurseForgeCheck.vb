''' <summary>
''' CurseForge 官方源（API Key）的可用性检测。
''' 当用户选择「尽量使用官方源」时，进入下载页面会先检测当前使用的 Key（用户自填的，或启动器内置的）是否可用；
''' 一旦不可用，就自动把下载源切换回镜像源，并弹窗提示用户去配置自己的 API Key。
''' </summary>
Public Class CurseForgeCheck

    Private Const ProbeUrl As String = "https://api.curseforge.com/v1/mods/search?gameId=432&pageSize=1"

    '本次启动内是否已经检测过，避免每次进出下载页面都联网检测
    Private Shared _Checked As Boolean = False
    Private Shared _Busy As Boolean = False

    ''' <summary>
    ''' 用户更换或清空了自己的 Key 后调用，让下次进入下载页面时重新检测。
    ''' </summary>
    Public Shared Sub ResetCache()
        _Checked = False
    End Sub

    ''' <summary>
    ''' 用官方接口验证一个 Key。返回 1 有效，0 无效（被官方拒绝），-1 无法验证（网络问题）。
    ''' </summary>
    Public Shared Function VerifyKey(Key As String) As Integer
        If String.IsNullOrWhiteSpace(Key) Then Return 0
        Try
            NetRequestByClient(ProbeUrl, Timeout:=10000, Headers:=New String(,) {{"x-api-key", Key.Trim()}},
                               RequireJson:=True, MakeLog:=False)
            Return 1
        Catch ex As Exception
            Dim StatusCode As Integer = ExtractStatusCode(ex)
            If StatusCode = 401 OrElse StatusCode = 403 Then Return 0
            Logger.Warn(ex, $"校验 CurseForge API Key 失败（判定结果 {StatusCode}）")
            Return -1
        End Try
    End Function

    ''' <summary>
    ''' 从异常（可能被层层包装）里找出 HTTP 状态码。找不到返回 -1。
    ''' </summary>
    Private Shared Function ExtractStatusCode(ex As Exception) As Integer
        Dim Current As Exception = ex
        While Current IsNot Nothing
            If TypeOf Current Is HttpRequestCodeException Then Return CInt(CType(Current, HttpRequestCodeException).StatusCode)
            If Current.Message.Contains("403") OrElse Current.Message.Contains("Forbidden") Then Return 403
            If Current.Message.Contains("401") OrElse Current.Message.Contains("Unauthorized") Then Return 401
            Current = Current.InnerException
        End While
        Return -1
    End Function

    ''' <summary>
    ''' 进入下载页面时调用（必须在 UI 线程）。仅当设置为「尽量使用官方源」时才检测。
    ''' 检测在后台进行，不阻塞界面；失效时会自动切换镜像源并弹窗提示。
    ''' </summary>
    Public Shared Sub EnsureOnDownloadPage()
        If _Checked OrElse _Busy Then Return
        Try
            If Settings.Get(Of Integer)("ToolDownloadMod") <> 2 Then Return
        Catch ex As Exception
            Return
        End Try
        Dim Key As String = ModSecret.CurseForgeAPIKey
        If String.IsNullOrWhiteSpace(Key) Then Return '没有配置任何 Key，交给设置页面处理

        _Busy = True
        Threading.Tasks.Task.Run(
            Sub()
                Dim Result As Integer = -1
                Try
                    Result = VerifyKey(Key)
                Catch ex As Exception
                    Result = -1
                End Try
                RunInUi(
                    Sub()
                        _Busy = False
                        _Checked = True
                        If Result = 1 Then
                            Logger.Info("CurseForge 官方源可用（用户 Key）")
                        ElseIf Result = 0 Then
                            Logger.Warn("CurseForge 官方源已失效（用户 Key），将自动切换为镜像源")
                            OnKeyUnusable()
                        Else
                            Logger.Info("暂时无法确认 CurseForge 官方源是否可用（网络问题），保持当前设置")
                        End If
                    End Sub)
            End Sub)
    End Sub

    ''' <summary>
    ''' Key 不可用：切回镜像源 + 提示用户配置自己的 Key（UI 线程）。
    ''' </summary>
    Private Shared Sub OnKeyUnusable()
        Try
            Settings.Set("ToolDownloadMod", 0)
            If FrmSetupSystem IsNot Nothing Then FrmSetupSystem.SyncDownloadModCombo()
        Catch ex As Exception
            Logger.Error(ex, "切换下载源失败")
        End Try

        Dim Text As String = "你填写的 CurseForge API Key 没有通过官方接口验证（可能填错了，或者已经被吊销）。" & vbCrLf & vbCrLf &
                "为了避免下载失败，已经自动切换为镜像下载源。镜像源不用 Key，但可能缺少刚更新的版本。" & vbCrLf & vbCrLf &
                "想继续用官方源的话，可以到 设置 → 其他 → 下载源 里重新填写一个可用的 Key。"

        Try
            If MyMsgBox(Text, "CurseForge 官方源不可用", "去设置", "知道了", IsWarn:=True) = 1 Then
                FrmMain.PageChange(New FormMain.PageStackData With {.Page = FormMain.PageType.Setup}, FormMain.PageSubType.SetupSystem)
                If FrmSetupSystem IsNot Nothing Then FrmSetupSystem.FocusDownloadModCombo()
            End If
        Catch ex As Exception
            Logger.Error(ex, "提示 CurseForge API 失效失败")
        End Try
    End Sub

End Class
