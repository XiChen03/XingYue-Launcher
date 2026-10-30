Imports Newtonsoft.Json.Linq

''' <summary>
''' 主页「常去小站」和「常用指令」的可编辑配置。
''' 保存在 启动器目录\XingYue\homepage.json，主页读取它来生成对应的按钮与列表。
''' </summary>
Public Class HomepageEdit

    Public Class SiteEntry
        Public Title As String = ""
        Public Info As String = ""
        Public Url As String = ""
        Public Logo As String = ""
    End Class

    Public Class CommandEntry
        Public Text As String = ""
        ''' <summary>复制文本 / 执行命令 / 打开网页</summary>
        Public Type As String = "复制文本"
        Public Data As String = ""
        Public Tip As String = ""
    End Class

    ''' <summary>没登录时给个默认图标用，避免主页出现空白方块。</summary>
    Public Const DefaultLogo As String = "pack://application:,,,/images/Icons/XingYue.png"

    Public Property Sites As New List(Of SiteEntry)
    Public Property Commands As New List(Of CommandEntry)

    ''' <summary>配置文件路径（必须写成 Property，否则会在 Paths 初始化之前求值）。</summary>
    Public Shared ReadOnly Property FilePath As String
        Get
            Return Paths.Base & "XingYue\homepage.json"
        End Get
    End Property

#Region "默认内容"

    ''' <summary>内置的默认内容，和旧版本主页里写死的内容一致。</summary>
    Public Shared Function Defaults() As HomepageEdit
        Dim Result As New HomepageEdit
        Result.Sites.Add(New SiteEntry With {.Title = "哔哩哔哩", .Info = "看 MC 视频", .Url = "https://www.bilibili.com/", .Logo = "pack://application:,,,/images/Blocks/RedstoneLampOn.png"})
        Result.Sites.Add(New SiteEntry With {.Title = "MC 百科", .Info = "模组资料", .Url = "https://www.mcmod.cn/", .Logo = "pack://application:,,,/images/Blocks/CommandBlock.png"})
        Result.Sites.Add(New SiteEntry With {.Title = "Minecraft Wiki", .Info = "最全的百科", .Url = "https://zh.minecraft.wiki/", .Logo = "pack://application:,,,/images/Heads/wiki.png"})
        Result.Sites.Add(New SiteEntry With {.Title = "LittleSkin", .Info = "第三方皮肤站", .Url = "https://littleskin.cn/", .Logo = "pack://application:,,,/images/Skins/Alex.png"})
        Result.Sites.Add(New SiteEntry With {.Title = "CurseForge", .Info = "模组整合包", .Url = "https://www.curseforge.com/minecraft", .Logo = "pack://application:,,,/images/Blocks/Anvil.png"})
        Result.Sites.Add(New SiteEntry With {.Title = "Modrinth", .Info = "一个MC资源站", .Url = "https://modrinth.com/", .Logo = "pack://application:,,,/images/Icons/Modrinth.png"})
        Result.Sites.Add(New SiteEntry With {.Title = "BBSMC", .Info = "我的世界中国论坛", .Url = "https://bbsmc.net/", .Logo = "pack://application:,,,/images/Icons/BBSMC.png"})
        Result.Sites.Add(New SiteEntry With {.Title = "星玥未眠", .Info = "多功能小站", .Url = "https://xywm.org/", .Logo = "pack://application:,,,/images/Icons/XingYue.png"})
        Result.Sites.Add(New SiteEntry With {.Title = "DeepSeek", .Info = "AI 推荐", .Url = "https://www.deepseek.com/", .Logo = "pack://application:,,,/images/Icons/DeepSeek.png"})
        Result.Sites.Add(New SiteEntry With {.Title = "曦辰の主页", .Info = "作者小站", .Url = "https://bio.xywm.org/xichen", .Logo = "pack://application:,,,/images/Icons/XingChen.png"})

        Result.Commands.Add(New CommandEntry With {.Text = "关闭防火墙", .Type = "执行命令", .Data = "netsh|advfirewall set allprofiles state off", .Tip = "联机失败时可以试试关闭 Windows 防火墙（需要管理员权限）"})
        Result.Commands.Add(New CommandEntry With {.Text = "死亡不掉落", .Type = "复制文本", .Data = "/gamerule keepInventory true", .Tip = "复制到游戏内聊天栏执行，死亡后保留物品"})
        Result.Commands.Add(New CommandEntry With {.Text = "无限岩浆", .Type = "复制文本", .Data = "/gamerule lavaSourceConversion true", .Tip = "复制到游戏内聊天栏执行，让岩浆源可以被无限复制"})
        Result.Commands.Add(New CommandEntry With {.Text = "防止生物破坏", .Type = "复制文本", .Data = "/gamerule mobGriefing false", .Tip = "复制到游戏内聊天栏执行，禁止生物破坏方块"})
        Result.Commands.Add(New CommandEntry With {.Text = "入睡玩家比例", .Type = "复制文本", .Data = "/gamerule playersSleepingPercentage <数值>", .Tip = "复制到游戏内聊天栏执行，把 <数值> 换成 1~100（默认 100，即需要所有人都睡觉）"})
        Return Result
    End Function

#End Region

#Region "读写"

    ''' <summary>读取配置，文件不存在或损坏时返回默认内容。</summary>
    Public Shared Function Load() As HomepageEdit
        Try
            If Not FileUtils.Exists(FilePath) Then Return Defaults()
            Dim Json As JObject = JObject.Parse(FileUtils.ReadAsString(FilePath))
            Dim Result As New HomepageEdit
            Dim SitesToken As JArray = TryCast(Json("sites"), JArray)
            If SitesToken IsNot Nothing Then
                For Each Item As JToken In SitesToken
                    Result.Sites.Add(New SiteEntry With {
                        .Title = If(Item("title")?.ToString(), ""),
                        .Info = If(Item("info")?.ToString(), ""),
                        .Url = If(Item("url")?.ToString(), ""),
                        .Logo = If(Item("logo")?.ToString(), "")})
                Next
            Else
                Result.Sites = Defaults().Sites
            End If
            Dim CommandsToken As JArray = TryCast(Json("commands"), JArray)
            If CommandsToken IsNot Nothing Then
                For Each Item As JToken In CommandsToken
                    Result.Commands.Add(New CommandEntry With {
                        .Text = If(Item("text")?.ToString(), ""),
                        .Type = If(Item("type")?.ToString(), "复制文本"),
                        .Data = If(Item("data")?.ToString(), ""),
                        .Tip = If(Item("tip")?.ToString(), "")})
                Next
            Else
                Result.Commands = Defaults().Commands
            End If
            Return Result
        Catch ex As Exception
            Logger.Warn(ex, "读取主页小站 / 指令配置失败，已使用默认内容")
            Return Defaults()
        End Try
    End Function

    ''' <summary>保存配置。</summary>
    Public Sub Save()
        Dim SitesJson As New JArray
        For Each Entry As SiteEntry In Sites
            If String.IsNullOrWhiteSpace(Entry.Url) Then Continue For
            SitesJson.Add(New JObject From {
                {"title", Entry.Title}, {"info", Entry.Info}, {"url", Entry.Url}, {"logo", Entry.Logo}})
        Next
        Dim CommandsJson As New JArray
        For Each Entry As CommandEntry In Commands
            If String.IsNullOrWhiteSpace(Entry.Text) OrElse String.IsNullOrWhiteSpace(Entry.Data) Then Continue For
            CommandsJson.Add(New JObject From {
                {"text", Entry.Text}, {"type", Entry.Type}, {"data", Entry.Data}, {"tip", Entry.Tip}})
        Next
        Dim Json As New JObject From {{"sites", SitesJson}, {"commands", CommandsJson}}
        DirectoryUtils.Create(Paths.Base & "XingYue\")
        FileUtils.Write(FilePath, Json.ToString(Newtonsoft.Json.Formatting.Indented))
    End Sub

#End Region

#Region "生成主页 XAML"

    ''' <summary>把文本转义成可以安全放进单引号 XAML 属性的形式。</summary>
    Private Shared Function Esc(Text As String) As String
        Dim Result As String = If(Text, "")
        Result = Result.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;").Replace("'", "&apos;")
        '以 { 开头会被 WPF 当成标记扩展，前面加个空转义
        If Result.StartsWith("{") Then Result = "{}" & Result
        Return Result
    End Function

    ''' <summary>生成「常去小站」的 MyListItem 列表。</summary>
    Public Function BuildSitesXaml() As String
        Dim Sb As New Text.StringBuilder
        For Each Entry As SiteEntry In Sites
            If String.IsNullOrWhiteSpace(Entry.Url) Then Continue For
            Dim Logo As String = If(String.IsNullOrWhiteSpace(Entry.Logo), DefaultLogo, Entry.Logo)
            Sb.Append("<local:MyListItem Margin='-4,0,-4,2' Logo='").Append(Esc(Logo)) _
              .Append("' Title='").Append(Esc(Entry.Title)) _
              .Append("' Info='").Append(Esc(Entry.Info)) _
              .Append("' EventType='打开网页' EventData='").Append(Esc(Entry.Url)) _
              .Append("' Type='Clickable' />").Append(vbCrLf)
        Next
        Return Sb.ToString()
    End Function

    ''' <summary>生成「常用指令」的 MyButton 列表。</summary>
    Public Function BuildCommandsXaml() As String
        Dim Sb As New Text.StringBuilder
        For Each Entry As CommandEntry In Commands
            If String.IsNullOrWhiteSpace(Entry.Text) OrElse String.IsNullOrWhiteSpace(Entry.Data) Then Continue For
            Sb.Append("<local:MyButton Height='30' Padding='12,0,12,0' Margin='0,0,8,8' Text='").Append(Esc(Entry.Text)) _
              .Append("' EventType='").Append(Esc(Entry.Type)) _
              .Append("' EventData='").Append(Esc(Entry.Data)).Append("'")
            If Not String.IsNullOrWhiteSpace(Entry.Tip) Then Sb.Append(" ToolTip='").Append(Esc(Entry.Tip)).Append("'")
            Sb.Append(" />").Append(vbCrLf)
        Next
        Return Sb.ToString()
    End Function

#End Region

End Class
