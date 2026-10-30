Imports Newtonsoft.Json.Linq

''' <summary>
''' 联机模块（EasyTier）的配置来源。
''' 开源版不再从官方服务器拉取配置，这里内置一份默认值，并允许用 XingYue\Link.json 覆盖。
''' 想换成自己的中转/发现节点，只要在 设置 → 联机 → 自定义节点 里填 tcp://你的服务器:11010 即可，
''' 那份配置里的 Peers 可以留空。
''' </summary>
Friend Module ModLinkConfig

    ''' <summary>
    ''' 用户自定义联机配置文件（UTF-8 JSON，结构与官方 ServerConfig 里的 Link 节点一致）。
    ''' </summary>
    Public ReadOnly Property ConfigPath As String
        Get
            Return Paths.Base & "XingYue\Link.json"
        End Get
    End Property

    ''' <summary>
    ''' 内置默认配置，下载地址指向 EasyTier 官方 GitHub Release。
    ''' </summary>
    Private Function BuildDefault() As JObject
        Return New JObject From {
            {"EasyTierVersion", 2},
            {"Downloads", New JArray(
                "https://github.com/EasyTier/EasyTier/releases/download/v2.4.5/easytier-windows-{arch}-v2.4.5.zip",
                "https://ghfast.top/https://github.com/EasyTier/EasyTier/releases/download/v2.4.5/easytier-windows-{arch}-v2.4.5.zip")},
            {"Argument", "--enable-kcp-proxy --use-smoltcp --encryption-algorithm=chacha20 --no-tun --compression=zstd --multi-thread"},
            {"PeersBlackList", New JArray()},
            {"RandomPeer", 3},
            {"MandatoryPeers", New JArray()},
            {"Peers", New JArray()},
            {"DiscoverPeer", ""},
            {"DiscoverPeerId", 0}}
    End Function

    ''' <summary>
    ''' 内置联机模块对应的 EasyTierVersion 标记，与内置配置里的 EasyTierVersion 一致。
    ''' </summary>
    Public Const BuiltInVersion As Integer = 2

    ''' <summary>
    ''' EasyTier 默认的监听端口。中转节点也使用这个端口。
    ''' </summary>
    Public Const DefaultPort As Integer = 11010

    ''' <summary>
    ''' 程序内是否内置了 x86_64 的联机模块（EasyTier 压缩包）。
    ''' </summary>
    Public Function HasBuiltInModule() As Boolean
        Try
            Return TypeOf My.Resources.ResourceManager.GetObject("EasyTierX86") Is Byte()
        Catch ex As Exception
            Logger.Warn(ex, "读取内置联机模块失败")
            Return False
        End Try
    End Function

    ''' <summary>
    ''' 把内置的联机模块压缩包释放到指定文件。
    ''' </summary>
    Public Sub ExtractBuiltInModule(FilePath As String)
        ExtractResources(FilePath, "EasyTierX86")
    End Sub

    ''' <summary>
    ''' 取得联机配置：优先使用 XingYue\Link.json，文件缺失或读取失败时回退到内置默认值。
    ''' </summary>
    Public Function GetConfig() As JObject
        Dim Result As JObject = BuildDefault()
        Dim Path As String = ConfigPath
        If Not IO.File.Exists(Path) Then Return Result
        Try
            Dim Custom As JObject = JObject.Parse(IO.File.ReadAllText(Path, Text.Encoding.UTF8))
            For Each Pair As KeyValuePair(Of String, JToken) In Custom
                Result(Pair.Key) = Pair.Value
            Next
            Logger.Info($"已读取自定义联机配置：{Path}")
        Catch ex As Exception
            Logger.Warn(ex, $"读取自定义联机配置失败，已回退到内置配置：{Path}")
        End Try
        Return Result
    End Function

End Module
