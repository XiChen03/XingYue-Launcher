Imports System.IO
Imports System.Text.RegularExpressions

''' <summary>服务端核心的在线下载：原版、Paper、Purpur、Fabric、NeoForge、Forge。</summary>
Public Class ServerCoreDownloader

    Public Enum CoreSource
        Vanilla
        Paper
        Purpur
        Fabric
        NeoForge
        Forge
    End Enum

    Public Shared ReadOnly SourceNames As String() = {"原版 (Vanilla)", "Paper", "Purpur", "Fabric", "NeoForge", "Forge"}

    Public Shared Function SourceFromIndex(Index As Integer) As CoreSource
        If Index < 0 OrElse Index > 5 Then Return CoreSource.Vanilla
        Return CType(Index, CoreSource)
    End Function

    ''' <summary>
    ''' 取文本（JSON / XML）：先走启动器自己的网络请求（含代理设置），失败时用镜像站和 DoH 兜底。
    ''' </summary>
    Private Shared Function FetchText(Url As String) As String
        Try
            Return NetRequestByClient(Url, RequireJson:=False)
        Catch ex As Exception
            Logger.Warn(ex, "官方地址请求失败，尝试镜像：" & Url)
            Return DownloadHelper.GetString(Url)
        End Try
    End Function

    Public Shared Function GetVersions(Source As CoreSource) As List(Of String)
        Return GetGameVersions(Source)
    End Function

    ''' <summary>这个来源要不要选加载器版本（Forge / NeoForge / Fabric 需要）。</summary>
    Public Shared Function NeedsLoader(Source As CoreSource) As Boolean
        Return Source = CoreSource.Fabric OrElse Source = CoreSource.NeoForge OrElse Source = CoreSource.Forge
    End Function

    ''' <summary>取某个来源的游戏版本列表（原版 = 正式版，其它 = 该服务端支持的游戏版本）。</summary>
    Public Shared Function GetGameVersions(Source As CoreSource) As List(Of String)
        Dim Result As New List(Of String)
        Select Case Source
            Case CoreSource.Vanilla
                Dim Json As Newtonsoft.Json.Linq.JObject = Newtonsoft.Json.Linq.JObject.Parse(
                    FetchText("https://launchermeta.mojang.com/mc/game/version_manifest_v2.json"))
                For Each Item As Newtonsoft.Json.Linq.JToken In Json("versions")
                    If Item("type")?.ToString() = "release" Then Result.Add(Item("id").ToString())
                Next

            Case CoreSource.Paper
                'PaperMC 已停用 v2 API，改用 Fill v3
                Dim Json As Newtonsoft.Json.Linq.JObject = Newtonsoft.Json.Linq.JObject.Parse(
                    FetchText("https://fill.papermc.io/v3/projects/paper"))
                Dim Versions As Newtonsoft.Json.Linq.JObject = TryCast(Json("versions"), Newtonsoft.Json.Linq.JObject)
                If Versions IsNot Nothing Then
                    For Each Group As Newtonsoft.Json.Linq.JProperty In Versions.Properties()
                        For Each Item As Newtonsoft.Json.Linq.JToken In Group.Value
                            Dim Name As String = Item.ToString()
                            If Not Result.Contains(Name) Then Result.Add(Name)
                        Next
                    Next
                End If

            Case CoreSource.Purpur
                Dim Json As Newtonsoft.Json.Linq.JObject = Newtonsoft.Json.Linq.JObject.Parse(
                    FetchText("https://api.purpurmc.org/v2/purpur"))
                For i As Integer = Json("versions").Count - 1 To 0 Step -1
                    Result.Add(Json("versions")(i).ToString())
                Next

            Case CoreSource.Fabric
                Dim Json As Newtonsoft.Json.Linq.JArray = Newtonsoft.Json.Linq.JArray.Parse(
                    FetchText("https://meta.fabricmc.net/v2/versions/game"))
                For Each Item As Newtonsoft.Json.Linq.JToken In Json
                    If Item("stable") IsNot Nothing AndAlso Item("stable").ToObject(Of Boolean)() Then
                        Result.Add(Item("version").ToString())
                    End If
                Next

            Case CoreSource.NeoForge
                'NeoForge 没有全局列表接口，逐个已知游戏版本问 BMCLAPI，有数据的才算支持
                For Each McVer As String In {"1.21.4", "1.21.3", "1.21.2", "1.21.1", "1.21",
                                             "1.20.6", "1.20.5", "1.20.4", "1.20.3", "1.20.2"}
                    Try
                        Dim Arr As Newtonsoft.Json.Linq.JArray = Newtonsoft.Json.Linq.JArray.Parse(
                            FetchText($"https://bmclapi2.bangbang93.com/neoforge/list/{McVer}"))
                        If Arr.Count > 0 Then Result.Add(McVer)
                    Catch ex As Exception
                        Logger.Warn(ex, "获取 NeoForge 版本列表失败：" & McVer)
                    End Try
                Next

            Case CoreSource.Forge
                Dim Xml As String = FetchText("https://maven.minecraftforge.net/net/minecraftforge/forge/maven-metadata.xml")
                For Each Version As String In ParseMavenVersions(Xml)
                    Dim Split As String() = Version.Split({"-"c}, 2)
                    If Split.Length = 2 AndAlso Not Result.Contains(Split(0)) Then Result.Add(Split(0))
                Next
        End Select

        '只保留前面一部分，避免下拉列表过长
        If Result.Count > 80 Then Result = Result.GetRange(0, 80)
        Return Result
    End Function

    ''' <summary>取某游戏版本下可用的加载器版本（Fabric / NeoForge / Forge）。</summary>
    Public Shared Function GetLoaderVersions(Source As CoreSource, GameVersion As String) As List(Of String)
        Dim Result As New List(Of String)
        If Not NeedsLoader(Source) OrElse GameVersion = "" Then Return Result
        Select Case Source
            Case CoreSource.Fabric
                Dim Json As Newtonsoft.Json.Linq.JArray = Newtonsoft.Json.Linq.JArray.Parse(
                    FetchText($"https://meta.fabricmc.net/v2/versions/loader/{GameVersion}"))
                For Each Item As Newtonsoft.Json.Linq.JToken In Json
                    Dim Loader As Newtonsoft.Json.Linq.JToken = Item("loader")
                    If Loader IsNot Nothing Then Result.Add(Loader("version").ToString())
                Next

            Case CoreSource.NeoForge
                Dim Arr As Newtonsoft.Json.Linq.JArray = Newtonsoft.Json.Linq.JArray.Parse(
                    FetchText($"https://bmclapi2.bangbang93.com/neoforge/list/{GameVersion}"))
                For i As Integer = Arr.Count - 1 To 0 Step -1
                    Dim Ver As Newtonsoft.Json.Linq.JToken = Arr(i)("version")
                    If Ver IsNot Nothing Then Result.Add(Ver.ToString())
                Next

            Case CoreSource.Forge
                Dim Xml As String = FetchText("https://maven.minecraftforge.net/net/minecraftforge/forge/maven-metadata.xml")
                For Each Version As String In ParseMavenVersions(Xml)
                    If Version.StartsWith(GameVersion & "-", StringComparison.OrdinalIgnoreCase) Then Result.Add(Version)
                Next
        End Select
        Return Result
    End Function

    Private Shared Function ParseMavenVersions(Xml As String) As List(Of String)
        Dim Result As New List(Of String)
        For Each Match As Match In Regex.Matches(Xml, "<version>([^<]+)</version>")
            Dim Version As String = Match.Groups(1).Value.Trim()
            If Version <> "" AndAlso Not Version.EndsWith("-SNAPSHOT", StringComparison.OrdinalIgnoreCase) Then Result.Add(Version)
        Next
        Result.Reverse()
        Return Result
    End Function

    ''' <summary>下载指定版本的服务端核心，并自动写回实例配置。返回核心文件名或 @args 路径。</summary>
    ''' <param name="GameVersion">游戏版本，例如 1.21.1。</param>
    ''' <param name="LoaderVersion">加载器版本（Fabric / NeoForge / Forge 用；留空则取最新）。</param>
    Public Shared Sub Download(Source As CoreSource, GameVersion As String, LoaderVersion As String, Dir As String, Progress As Action(Of String))
        If String.IsNullOrEmpty(Dir) Then Throw New Exception("请先在实例设置里选择服务端目录。")
        If String.IsNullOrEmpty(GameVersion) Then Throw New Exception("请先选择游戏版本。")
        Directory.CreateDirectory(Dir)
        Dim Version As String = GameVersion
        Progress?.Invoke($"正在准备下载 {GameVersion} {If(LoaderVersion = "", "", "· " & LoaderVersion)} ……")

        Select Case Source
            Case CoreSource.Vanilla
                Dim Json As Newtonsoft.Json.Linq.JObject = Newtonsoft.Json.Linq.JObject.Parse(
                    FetchText("https://launchermeta.mojang.com/mc/game/version_manifest_v2.json"))
                Dim VersionUrl As String = ""
                For Each Item As Newtonsoft.Json.Linq.JToken In Json("versions")
                    If Item("id")?.ToString() = Version Then
                        VersionUrl = Item("url").ToString()
                        Exit For
                    End If
                Next
                If VersionUrl = "" Then Throw New Exception("找不到这个原版版本：" & Version)
                Dim Detail As Newtonsoft.Json.Linq.JObject = Newtonsoft.Json.Linq.JObject.Parse(
                    FetchText(VersionUrl))
                Dim ServerUrl As String = Detail("downloads")("server")("url")?.ToString()
                If String.IsNullOrEmpty(ServerUrl) Then Throw New Exception("这个版本没有服务端下载：" & Version)
                Dim FileName As String = $"server-{Version}.jar"
                '官方 piston-data 地址 + BMCLAPI 镜像
                DownloadFirst(New List(Of String) From {
                    ServerUrl,
                    $"https://bmclapi2.bangbang93.com/version/{Version}/server"}, Path.Combine(Dir, FileName), Progress)
                ApplyCore(FileName)

            Case CoreSource.Paper
                Dim Build As Newtonsoft.Json.Linq.JObject = Newtonsoft.Json.Linq.JObject.Parse(
                    FetchText($"https://fill.papermc.io/v3/projects/paper/versions/{Version}/builds/latest"))
                Dim Download As Newtonsoft.Json.Linq.JToken = Build("downloads")("server:default")
                If Download Is Nothing Then Throw New Exception("这个版本没有可用的 Paper 构建：" & Version)
                Dim FileName As String = Download("name").ToString()
                DownloadFile(Download("url").ToString(), Path.Combine(Dir, FileName), Progress)
                ApplyCore(FileName)

            Case CoreSource.Purpur
                Dim FileName As String = $"purpur-{Version}.jar"
                DownloadFile($"https://api.purpurmc.org/v2/purpur/{Version}/latest/download", Path.Combine(Dir, FileName), Progress)
                ApplyCore(FileName)

            Case CoreSource.Fabric
                'Fabric 已不再提供 server/jar 直链，改用官方安装器生成服务端
                Dim Loader As String = LoaderVersion
                If Loader = "" Then
                    Dim Loaders As Newtonsoft.Json.Linq.JArray = Newtonsoft.Json.Linq.JArray.Parse(
                        FetchText($"https://meta.fabricmc.net/v2/versions/loader/{GameVersion}"))
                    If Loaders.Count = 0 Then Throw New Exception("这个版本还没有可用的 Fabric Loader：" & GameVersion)
                    Loader = Loaders(0)("loader")("version").ToString()
                End If
                Dim InstallerVersion As String = "1.1.2"
                Dim InstallerXml As String = FetchText("https://maven.fabricmc.net/net/fabricmc/fabric-installer/maven-metadata.xml")
                Dim Versions As List(Of String) = ParseMavenVersions(InstallerXml)
                If Versions.Count > 0 Then InstallerVersion = Versions(0)
                Dim InstallerPath As String = Path.Combine(Dir, $"fabric-installer-{InstallerVersion}.jar")
                DownloadFile($"https://maven.fabricmc.net/net/fabricmc/fabric-installer/{InstallerVersion}/fabric-installer-{InstallerVersion}.jar",
                             InstallerPath, Progress)
                RunJava(InstallerPath, $"server -mcversion {GameVersion} -loader {Loader} -downloadMinecraft", Dir, Progress)
                If IO.File.Exists(Path.Combine(Dir, "fabric-server-launch.jar")) Then
                    ApplyCore("fabric-server-launch.jar")
                ElseIf IO.File.Exists(Path.Combine(Dir, "fabric-server-launcher.jar")) Then
                    ApplyCore("fabric-server-launcher.jar")
                Else
                    Throw New Exception("Fabric 安装器没有生成启动文件，请看看控制台输出。")
                End If

            Case CoreSource.NeoForge
                Dim NeoVersion As String = If(LoaderVersion, "")
                If NeoVersion = "" Then
                    Dim List As List(Of String) = GetLoaderVersions(CoreSource.NeoForge, GameVersion)
                    If List.Count = 0 Then Throw New Exception("这个版本没有可用的 NeoForge：" & GameVersion)
                    NeoVersion = List(0)
                End If
                Dim FileName As String = $"neoforge-{NeoVersion}-installer.jar"
                '官方 maven 在国内经常连不上，BMCLAPI 的 /maven 有同一份文件
                DownloadFirst(New List(Of String) From {
                    $"https://maven.neoforged.net/releases/net/neoforged/neoforge/{NeoVersion}/neoforge-{NeoVersion}-installer.jar",
                    $"https://bmclapi2.bangbang93.com/maven/net/neoforged/neoforge/{NeoVersion}/neoforge-{NeoVersion}-installer.jar"},
                    Path.Combine(Dir, FileName), Progress)
                RunInstaller(Path.Combine(Dir, FileName), Dir, Progress)
                Dim Args As String = Path.Combine("libraries", "net", "neoforged", "neoforge", NeoVersion, "win_args.txt")
                If Not IO.File.Exists(Path.Combine(Dir, Args)) Then Args = Path.Combine("libraries", "net", "neoforged", "neoforge", NeoVersion, "unix_args.txt")
                If Not IO.File.Exists(Path.Combine(Dir, Args)) Then Throw New Exception("安装器没有生成启动参数文件，请看看控制台输出。")
                ApplyCore("@" & Args.Replace("\", "/"))

            Case CoreSource.Forge
                Dim ForgeVersion As String = LoaderVersion
                If ForgeVersion = "" Then
                    ForgeVersion = GameVersion
                ElseIf Not ForgeVersion.StartsWith(GameVersion & "-", StringComparison.OrdinalIgnoreCase) Then
                    ForgeVersion = GameVersion & "-" & ForgeVersion
                End If
                Dim FileName As String = $"forge-{ForgeVersion}-installer.jar"
                '国内镜像（BMCLAPI）按 mcversion / version 拆分参数，官方地址失败时用它兜底
                Dim Split As String() = ForgeVersion.Split({"-"c}, 2)
                Dim Mirrors As New List(Of String) From {
                    $"https://maven.minecraftforge.net/net/minecraftforge/forge/{ForgeVersion}/forge-{ForgeVersion}-installer.jar"}
                If Split.Length = 2 Then
                    Mirrors.Add($"https://bmclapi2.bangbang93.com/forge/download?mcversion={Split(0)}&version={Split(1)}&category=installer&format=jar")
                End If
                DownloadFirst(Mirrors, Path.Combine(Dir, FileName), Progress)
                RunInstaller(Path.Combine(Dir, FileName), Dir, Progress)
                Dim Args As String = Path.Combine("libraries", "net", "minecraftforge", "forge", ForgeVersion, "win_args.txt")
                If Not IO.File.Exists(Path.Combine(Dir, Args)) Then Args = Path.Combine("libraries", "net", "minecraftforge", "forge", ForgeVersion, "unix_args.txt")
                If Not IO.File.Exists(Path.Combine(Dir, Args)) Then Throw New Exception("安装器没有生成启动参数文件，请看看控制台输出。")
                ApplyCore("@" & Args.Replace("\", "/"))
        End Select
        Progress?.Invoke("完成，已把核心文件设为 " & ServerManager.Core)
    End Sub

    Private Shared Sub ApplyCore(CoreName As String)
        ServerManager.Core = CoreName
        ServerManager.StartMode = "java"
        ServerManager.WriteConfig()
    End Sub

    Private Shared Sub DownloadFile(Url As String, LocalPath As String, Progress As Action(Of String))
        '官方地址、镜像站、DoH 直连依次尝试，全部由 DownloadHelper 处理
        DownloadHelper.DownloadFile(Url, LocalPath, Progress)
    End Sub

    ''' <summary>按顺序尝试多个地址，第一个成功的留下文件。</summary>
    Private Shared Sub DownloadFirst(Urls As List(Of String), LocalPath As String, Progress As Action(Of String))
        Dim LastError As Exception = Nothing
        For Each Url As String In Urls
            Try
                DownloadHelper.DownloadFile(Url, LocalPath, Progress)
                Return
            Catch ex As Exception
                LastError = ex
                Logger.Warn(ex, "核心下载失败，尝试下一个地址：" & Url)
            End Try
        Next
        Throw New Exception("所有下载地址都失败了：" & If(LastError Is Nothing, "未知错误", LastError.Message))
    End Sub

    ''' <summary>运行 Forge / NeoForge 安装器（--installServer，顺带指定国内镜像）。</summary>
    Private Shared Sub RunInstaller(InstallerPath As String, Dir As String, Progress As Action(Of String))
        RunJava(InstallerPath, "--installServer --mirror https://bmclapi2.bangbang93.com/maven", Dir, Progress)
    End Sub

    ''' <summary>用实例配置里的 Java 运行一个 jar。</summary>
    Private Shared Sub RunJava(JarPath As String, Arguments As String, Dir As String, Progress As Action(Of String))
        Dim Java As String = ServerManager.FindJava()
        If String.IsNullOrEmpty(Java) Then Throw New Exception("没有找到 Java，无法运行安装器。")
        Progress?.Invoke("正在运行安装器，这一步可能需要几分钟……")
        Dim StartInfo As New ProcessStartInfo(Java, $"-jar ""{JarPath}"" {Arguments}") With {
            .WorkingDirectory = Dir,
            .UseShellExecute = False,
            .CreateNoWindow = True,
            .RedirectStandardOutput = True,
            .RedirectStandardError = True,
            .StandardOutputEncoding = Text.Encoding.UTF8,
            .StandardErrorEncoding = Text.Encoding.UTF8
        }
        Using Proc As Process = Process.Start(StartInfo)
            Dim LastOutput As New Threading.ManualResetEventSlim(True)
            AddHandler Proc.OutputDataReceived,
                Sub(sender, e)
                    If Not String.IsNullOrEmpty(e.Data) Then
                        LastOutput.Set()
                        Progress?.Invoke(e.Data)
                    End If
                End Sub
            AddHandler Proc.ErrorDataReceived,
                Sub(sender, e)
                    If Not String.IsNullOrEmpty(e.Data) Then
                        LastOutput.Set()
                        Progress?.Invoke(e.Data)
                    End If
                End Sub
            Proc.BeginOutputReadLine()
            Proc.BeginErrorReadLine()
            '安装器卡在下载上时不会退出，这里做超时保护
            Dim Total As Integer = 0
            Dim Idle As Integer = 0
            While Not Proc.WaitForExit(2000)
                Total += 2
                If LastOutput.Wait(0) Then
                    LastOutput.Reset()
                    Idle = 0
                Else
                    Idle += 2
                End If
                If Idle > 240 Then
                    Proc.Kill()
                    Throw New Exception("安装器已经 4 分钟没有任何输出，多半是连不上下载服务器（Forge / NeoForge 的 maven），已中止。开代理后可以重试。")
                End If
                If Total > 900 Then
                    Proc.Kill()
                    Throw New Exception("安装器运行超过 15 分钟，已中止。")
                End If
            End While
            If Proc.ExitCode <> 0 Then Throw New Exception($"安装器退出码 {Proc.ExitCode}，安装可能失败。")
        End Using
    End Sub

End Class
