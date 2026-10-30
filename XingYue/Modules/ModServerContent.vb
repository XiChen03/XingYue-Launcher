Imports Newtonsoft.Json.Linq
Imports System.Net

''' <summary>
''' 模组 / 插件的在线搜索与下载（数据来自 Modrinth 开放 API）。
''' </summary>
Public Class ServerContent

    Public Enum ContentKind
        Mods = 0
        Plugins = 1
    End Enum

    ''' <summary>搜索结果中的一项。</summary>
    Public Class Hit
        Public Property Slug As String = ""
        Public Property Title As String = ""
        Public Property Description As String = ""
        Public Property Downloads As Long
        Public Property IconUrl As String = ""
    End Class

    ''' <summary>某个项目下可用于当前实例的某个文件。</summary>
    Public Class ContentFile
        Public Property Url As String = ""
        Public Property FileName As String = ""
        Public Property Size As Long
        Public Property VersionName As String = ""
        Public Property Loaders As String = ""
        Public Property GameVersions As String = ""
    End Class

    Private Const ApiBase As String = "https://api.modrinth.com/v2"
    Private Const UserAgent As String = "XingYueLauncher/1.0 (Minecraft server manager)"

    '===============================
    '  加载器 / 版本
    '===============================

    ''' <summary>当前实例能用的模组加载器（Modrinth 的 loader 名）。</summary>
    Public Shared Function DetectModLoaders(Dir As String) As List(Of String)
        Dim Result As New List(Of String)
        If Dir = "" Then Return Result
        Try
            If IO.Directory.Exists(IO.Path.Combine(Dir, "libraries", "net", "neoforged")) Then Result.Add("neoforge")
            If IO.Directory.Exists(IO.Path.Combine(Dir, "libraries", "net", "minecraftforge")) Then Result.Add("forge")
            If IO.File.Exists(IO.Path.Combine(Dir, "fabric-server-launch.jar")) OrElse
               IO.Directory.Exists(IO.Path.Combine(Dir, "libraries", "net", "fabricmc")) Then Result.Add("fabric")
            If IO.Directory.Exists(IO.Path.Combine(Dir, "libraries", "org", "quiltmc")) Then Result.Add("quilt")
        Catch ex As Exception
            Logger.Warn(ex, "检测模组加载器失败")
        End Try
        Return Result
    End Function

    ''' <summary>当前实例能用的插件加载器（Modrinth 的 loader 名）。</summary>
    Public Shared Function DetectPluginLoaders(Dir As String, Core As String) As List(Of String)
        Dim Result As New List(Of String)
        Dim Name As String = If(Core, "").ToLowerInvariant()
        If Name.Contains("purpur") Then
            Result.Add("purpur")
        ElseIf Name.Contains("paper") Then
            Result.Add("paper")
        End If
        Result.Add("spigot")
        Result.Add("bukkit")
        Return Result
    End Function

    ''' <summary>从核心文件名里猜一个 Minecraft 版本，猜不到返回空串。</summary>
    Public Shared Function GuessGameVersion(Core As String) As String
        If String.IsNullOrEmpty(Core) Then Return ""
        Dim M As Text.RegularExpressions.Match = Text.RegularExpressions.Regex.Match(Core, "(\d+\.\d+(?:\.\d+)?)")
        If M.Success Then Return M.Groups(1).Value
        Return ""
    End Function

    '===============================
    '  搜索
    '===============================

    ''' <summary>在 Modrinth 上搜索模组/插件。</summary>
    ''' <param name="Kind">模组还是插件。</param>
    ''' <param name="GameVersion">游戏版本，空则不限。</param>
    ''' <param name="Loaders">加载器候选，命中任意一个即可；空则不限。</param>
    Public Shared Function Search(Query As String, Kind As ContentKind, GameVersion As String, Loaders As List(Of String)) As List(Of Hit)
        Dim Result As New List(Of Hit)
        Dim Facets As New List(Of String)
        Facets.Add("[""project_type:" & If(Kind = ContentKind.Mods, "mod", "plugin") & """]")
        If GameVersion <> "" Then Facets.Add("[""versions:" & GameVersion & """]")
        If Loaders IsNot Nothing AndAlso Loaders.Count > 0 Then
            Dim Ors As New List(Of String)
            For Each L As String In Loaders
                Ors.Add("""categories:" & L & """")
            Next
            Facets.Add("[" & String.Join(",", Ors) & "]")
        End If

        Dim Url As String = ApiBase & "/search?limit=15&index=relevance" &
            "&query=" & Uri.EscapeDataString(If(Query, "")) &
            "&facets=" & Uri.EscapeDataString("[" & String.Join(",", Facets) & "]")

        Dim Json As JObject = GetJson(Url)
        For Each Item As JToken In Json("hits")
            Result.Add(New Hit With {
                .Slug = TokenText(Item, "slug"),
                .Title = TokenText(Item, "title"),
                .Description = TokenText(Item, "description"),
                .Downloads = If(Item("downloads") Is Nothing, 0L, Item("downloads").ToObject(Of Long)()),
                .IconUrl = TokenText(Item, "icon_url")})
        Next
        Return Result
    End Function

    '===============================
    '  文件列表
    '===============================

    ''' <summary>列出项目下适配当前版本/加载器的文件，最新的排前面。</summary>
    Public Shared Function GetFiles(Slug As String, GameVersion As String, Loaders As List(Of String)) As List(Of ContentFile)
        Dim Result As New List(Of ContentFile)
        Dim Url As String = ApiBase & "/project/" & Uri.EscapeDataString(Slug) & "/version"
        Dim Args As New List(Of String)
        If GameVersion <> "" Then Args.Add("game_versions=" & Uri.EscapeDataString("[""" & GameVersion & """]"))
        If Loaders IsNot Nothing AndAlso Loaders.Count > 0 Then
            Dim Ors As New List(Of String)
            For Each L As String In Loaders
                Ors.Add("""" & L & """")
            Next
            Args.Add("loaders=" & Uri.EscapeDataString("[" & String.Join(",", Ors) & "]"))
        End If
        If Args.Count > 0 Then Url &= "?" & String.Join("&", Args)

        Dim Json As JArray = JArray.Parse(GetRaw(Url))
        For Each Version As JToken In Json
            Dim Files As JToken = Version("files")
            If Files Is Nothing Then Continue For
            For Each F As JToken In Files
                If Not (F("primary") Is Nothing OrElse F("primary").ToObject(Of Boolean)()) Then Continue For
                Result.Add(New ContentFile With {
                    .Url = TokenText(F, "url"),
                    .FileName = TokenText(F, "filename"),
                    .Size = If(F("size") Is Nothing, 0L, F("size").ToObject(Of Long)()),
                    .VersionName = TokenText(Version, "version_number"),
                    .Loaders = JoinTokens(Version("loaders")),
                    .GameVersions = JoinTokens(Version("game_versions"))})
            Next
        Next
        Return Result
    End Function

    '===============================
    '  下载
    '===============================

    ''' <summary>把文件下载到目标文件夹（不存在会自动创建），返回完整路径。</summary>
    Public Shared Function DownloadTo(Url As String, FileName As String, TargetFolder As String, Progress As Action(Of String)) As String
        IO.Directory.CreateDirectory(TargetFolder)
        Dim Dest As String = IO.Path.Combine(TargetFolder, FileName)
        DownloadHelper.DownloadFile(Url, Dest, Progress)
        Return Dest
    End Function

    Private Shared Function GetRaw(Url As String) As String
        Return DownloadHelper.GetString(Url)
    End Function

    Private Shared Function GetJson(Url As String) As JObject
        Return JObject.Parse(GetRaw(Url))
    End Function

    Private Shared Function TokenText(Token As JToken, Name As String) As String
        If Token Is Nothing OrElse Token(Name) Is Nothing OrElse Token(Name).Type = JTokenType.Null Then Return ""
        Return Token(Name).ToString()
    End Function

    Private Shared Function JoinTokens(Token As JToken) As String
        If Token Is Nothing Then Return ""
        Return String.Join(",", Token.Select(Function(T) T.ToString()))
    End Function

End Class
