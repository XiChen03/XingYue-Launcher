Imports System.Net

''' <summary>
''' 下载辅助：官方地址失败时依次尝试镜像站，最后再用 DoH 解析出的 IP 直连。
''' 目前内置两个镜像站：BMCLAPI（Mojang / Fabric 元数据）与 MCIMirror（Modrinth 文件）。
''' </summary>
Public Class DownloadHelper

    Public Const UserAgent As String = "XingYueLauncher/1.0 (Minecraft launcher)"

    ''' <summary>把官方地址转换成各镜像站的等价地址（不含原始地址本身）。</summary>
    Public Shared Function MirrorUrls(Url As String) As List(Of String)
        Dim Result As New List(Of String)
        Dim Uri As New Uri(Url)
        Dim Host As String = Uri.Host.ToLowerInvariant()
        Dim PathAndQuery As String = Uri.PathAndQuery

        '镜像站 1：BMCLAPI（Mojang 系 + Fabric 元数据 + Forge / NeoForge 的 Maven）
        Select Case Host
            Case "launchermeta.mojang.com", "piston-meta.mojang.com", "piston-data.mojang.com", "libraries.minecraft.net"
                Result.Add("https://bmclapi2.bangbang93.com" & PathAndQuery)
            Case "meta.fabricmc.net"
                Result.Add("https://bmclapi2.bangbang93.com/fabric-meta" & PathAndQuery)
            Case "maven.minecraftforge.net"
                Result.Add("https://bmclapi2.bangbang93.com/maven" & PathAndQuery)
            Case "maven.neoforged.net"
                '官方是 /releases/net/neoforged/...，BMCLAPI 是 /maven/net/neoforged/...
                Dim MirrorPath As String = PathAndQuery.Replace("/releases/", "/")
                Result.Add("https://bmclapi2.bangbang93.com/maven" & MirrorPath)
        End Select

        '镜像站 2：MCIMirror（Modrinth 文件）
        If Host = "cdn.modrinth.com" Then
            Result.Add("https://mod.mcimirror.top" & PathAndQuery)
        End If

        Return Result
    End Function

    ''' <summary>官方地址 + 镜像站地址（按尝试顺序）。</summary>
    Public Shared Function Candidates(Url As String) As List(Of String)
        Dim Result As New List(Of String) From {Url}
        Try
            Result.AddRange(MirrorUrls(Url))
        Catch ex As Exception
            Logger.Warn(ex, "生成镜像地址失败")
        End Try
        Return Result
    End Function

    ''' <summary>下载文件：官方 → 镜像 → DoH IP 直连。</summary>
    Public Shared Sub DownloadFile(Url As String, LocalPath As String, Progress As Action(Of String))
        Dim Errors As New List(Of String)
        For Each Candidate As String In Candidates(Url)
            Try
                DownloadOnce(Candidate, LocalPath, "", Progress)
                Return
            Catch ex As Exception
                Errors.Add(Candidate & " → " & ex.Message)
                Logger.Warn(ex, "下载失败：" & Candidate)
            End Try
        Next

        '官方和镜像都失败时，换成 DoH 解析出来的 IP 再试一遍
        Dim Host As String
        Try
            Host = New Uri(Url).Host
        Catch
            Host = ""
        End Try
        If Host <> "" Then
            For Each Ip As String In ResolveDoHIps(Host)
                Try
                    DownloadOnce(Url, LocalPath, Ip, Progress)
                    Return
                Catch ex As Exception
                    Errors.Add(Ip & " → " & ex.Message)
                    Logger.Warn(ex, "下载失败（IP " & Ip & "）：" & Url)
                End Try
            Next
        End If

        Throw New Exception("下载失败：" & String.Join("；", Errors))
    End Sub

    ''' <summary>以文本方式获取（JSON / XML），同样带镜像与 DoH 兜底。</summary>
    Public Shared Function GetString(Url As String) As String
        Dim Errors As New List(Of String)
        For Each Candidate As String In Candidates(Url)
            Try
                Return GetStringOnce(Candidate, "")
            Catch ex As Exception
                Errors.Add(Candidate & " → " & ex.Message)
            End Try
        Next

        Dim Host As String = ""
        Try
            Host = New Uri(Url).Host
        Catch
        End Try
        If Host <> "" Then
            For Each Ip As String In ResolveDoHIps(Host)
                Try
                    Return GetStringOnce(Url, Ip)
                Catch ex As Exception
                    Errors.Add(Ip & " → " & ex.Message)
                End Try
            Next
        End If
        Throw New Exception("请求失败：" & String.Join("；", Errors))
    End Function

    '===============================
    '  内部实现
    '===============================

    Private Shared Sub DownloadOnce(Url As String, LocalPath As String, Address As String, Progress As Action(Of String))
        Dim FileName As String = IO.Path.GetFileName(LocalPath)
        If Progress IsNot Nothing Then Progress("正在下载 " & FileName)
        Dim Temp As String = LocalPath & ".download"
        If IO.File.Exists(Temp) Then IO.File.Delete(Temp)
        IO.Directory.CreateDirectory(IO.Path.GetDirectoryName(LocalPath))

        Using Resp As HttpWebResponse = Open(Url, Address, 8000, 30000)
            Dim Total As Long = Resp.ContentLength
            Using Src As IO.Stream = Resp.GetResponseStream()
                Using Dst As New IO.FileStream(Temp, IO.FileMode.Create, IO.FileAccess.Write)
                    Dim Buffer(65535) As Byte
                    Dim Read As Integer
                    Dim Written As Long = 0
                    Dim LastPercent As Integer = -1
                    Do
                        Read = Src.Read(Buffer, 0, Buffer.Length)
                        If Read <= 0 Then Exit Do
                        Dst.Write(Buffer, 0, Read)
                        Written += Read
                        If Progress IsNot Nothing AndAlso Total > 0 Then
                            Dim Percent As Integer = CInt(Written * 100 \ Total)
                            If Percent <> LastPercent Then
                                LastPercent = Percent
                                Progress($"正在下载 {FileName} …… {Percent}%")
                            End If
                        End If
                    Loop
                End Using
            End Using
        End Using

        If Not IO.File.Exists(Temp) OrElse New IO.FileInfo(Temp).Length = 0 Then Throw New Exception("下载结果为空")
        If IO.File.Exists(LocalPath) Then IO.File.Delete(LocalPath)
        IO.File.Move(Temp, LocalPath)
        If Progress IsNot Nothing Then Progress($"下载完成（{New IO.FileInfo(LocalPath).Length \ 1024 \ 1024} MB）")
    End Sub

    Private Shared Function GetStringOnce(Url As String, Address As String) As String
        Using Resp As HttpWebResponse = Open(Url, Address, 10000, 20000)
            Using Reader As New IO.StreamReader(Resp.GetResponseStream(), Text.Encoding.UTF8)
                Return Reader.ReadToEnd()
            End Using
        End Using
    End Function

    Private Shared Function Open(Url As String, Address As String, TimeoutMs As Integer, ReadWriteTimeoutMs As Integer) As HttpWebResponse
        Dim Uri As New Uri(Url)
        Dim RequestUrl As String = If(Address = "", Url, "https://" & Address & Uri.PathAndQuery)
        ServicePointManager.SecurityProtocol = ServicePointManager.SecurityProtocol Or SecurityProtocolType.Tls12
        Dim Req As HttpWebRequest = DirectCast(WebRequest.Create(RequestUrl), HttpWebRequest)
        Req.UserAgent = UserAgent
        Req.Timeout = TimeoutMs
        Req.ReadWriteTimeout = ReadWriteTimeoutMs
        Req.AllowAutoRedirect = True
        Req.AutomaticDecompression = Net.DecompressionMethods.GZip Or Net.DecompressionMethods.Deflate
        If Address <> "" Then
            Req.Host = Uri.Host
            ServicePointManager.ServerCertificateValidationCallback =
                Function(sender, Cert, Chain, Errors)
                    If Errors = Net.Security.SslPolicyErrors.None Then Return True
                    If Errors = Net.Security.SslPolicyErrors.RemoteCertificateNameMismatch AndAlso Cert IsNot Nothing Then
                        Return Cert.Subject.Contains(Uri.Host)
                    End If
                    Return False
                End Function
        End If
        Try
            Return DirectCast(Req.GetResponse(), HttpWebResponse)
        Catch
            If Address <> "" Then ServicePointManager.ServerCertificateValidationCallback = Nothing
            Throw
        End Try
    End Function

    ''' <summary>用多个 DoH 服务解析域名，绕过可能被污染的本地 DNS。</summary>
    Public Shared Function ResolveDoHIps(Host As String) As List(Of String)
        Dim Result As New List(Of String)
        Dim Providers As String() = {
            "https://doh.pub/dns-query?name=",
            "https://doh.360.cn/resolve?name=",
            "https://dns.alidns.com/resolve?name="}
        For Each Base As String In Providers
            Try
                Dim Json As Newtonsoft.Json.Linq.JObject = Newtonsoft.Json.Linq.JObject.Parse(
                    GetStringDirect(Base & Uri.EscapeDataString(Host) & "&type=A"))
                Dim Answers As Newtonsoft.Json.Linq.JToken = Json("Answer")
                If Answers Is Nothing Then Continue For
                For Each Item As Newtonsoft.Json.Linq.JToken In Answers
                    If Item("type") Is Nothing OrElse Item("type").ToObject(Of Integer)() <> 1 Then Continue For
                    Dim Ip As String = Item("data").ToString()
                    If Not Result.Contains(Ip) Then Result.Add(Ip)
                Next
            Catch ex As Exception
                Logger.Warn(ex, "DoH 解析失败（" & Base & "）")
            End Try
        Next
        '146.75.* 这段 Fastly 节点在部分网络下不通，放到最后再试
        Return Result.OrderBy(Function(Ip) If(Ip.StartsWith("146.75."), 1, 0)).ToList()
    End Function

    ''' <summary>只走直连（给 DoH 查询自己用，避免递归）。</summary>
    Private Shared Function GetStringDirect(Url As String) As String
        ServicePointManager.SecurityProtocol = ServicePointManager.SecurityProtocol Or SecurityProtocolType.Tls12
        Using WC As New WebClient()
            WC.Headers.Add("User-Agent", UserAgent)
            WC.Encoding = Text.Encoding.UTF8
            Return WC.DownloadString(Url)
        End Using
    End Function

End Class
