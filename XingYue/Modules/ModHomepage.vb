'XingYue Launcher 默认主页（预设）
'内容为 XAML 文本，最终会被 PageLaunchRight 拼接到 StackPanel 中后实例化
Friend Module ModHomepage

    '该预设的编号，与 PageSetupUI 中的下拉框顺序对应
    Public Const DefaultPresetId As Integer = 25

    ''' <summary>
    ''' 默认主页的 XAML 内容。
    ''' 注意：属性值统一使用单引号，避免与 VB 的字符串引号冲突。
    ''' </summary>
    Public ReadOnly Property DefaultHomepage As String
        Get
            '「常去小站」和「常用指令」由用户在 设置 → 个性化 里编辑，保存到 XingYue\homepage.json
            Dim Edit As HomepageEdit = HomepageEdit.Load()
            Dim Content As String = "
<Grid>
    <Grid.ColumnDefinitions>
        <ColumnDefinition Width='2*' />
        <ColumnDefinition Width='14' />
        <ColumnDefinition Width='1*' MinWidth='170' />
    </Grid.ColumnDefinitions>

    <StackPanel Grid.Column='0'>
        <!-- 欢迎回来 -->
        <local:MyCard Margin='0,0,0,14' CanSwap='False'>
            <Grid Margin='18,18,18,18'>
                <Grid.ColumnDefinitions>
                    <ColumnDefinition Width='Auto' />
                    <ColumnDefinition Width='1*' />
                </Grid.ColumnDefinitions>
                <Grid.RowDefinitions>
                    <RowDefinition Height='Auto' />
                    <RowDefinition Height='Auto' />
                </Grid.RowDefinitions>
                <Border Width='124' Height='88' CornerRadius='7' ClipToBounds='True' Background='Transparent'>
                    <local:MyGifImage GifSource='pack://application:,,,/images/Themes/1.gif' Stretch='Uniform' RenderOptions.BitmapScalingMode='HighQuality' />
                </Border>
                <StackPanel Grid.Column='1' Margin='15,0,0,0' VerticalAlignment='Top'>
                    <TextBlock FontSize='15.5' FontWeight='Bold' Foreground='{DynamicResource ColorBrush3}' Text='欢迎回来 👑' />
                    <TextBlock Margin='0,7,0,0' FontSize='12.5' TextWrapping='Wrap' Foreground='{DynamicResource ColorBrush1}' Text='{hint}' />
                </StackPanel>
                <local:MyButton Grid.Row='1' Grid.ColumnSpan='2' Margin='0,12,0,0' HorizontalAlignment='Right' Height='30' Padding='18,0,18,0'
                                Text='启动游戏' ColorType='Highlight' EventType='启动游戏' EventData='\current'
                                ToolTip='启动当前选中的游戏版本' />
            </Grid>
        </local:MyCard>

        <!-- 回声洞 -->
        <local:MyCard Margin='0,0,0,14' CanSwap='False'>
            <StackPanel Margin='18,15,18,15'>
                <StackPanel Orientation='Horizontal'>
                    <TextBlock FontSize='13' FontWeight='Bold' Foreground='{DynamicResource ColorBrush3}' Text='回声洞' />
                    <TextBlock Margin='9,2,0,0' FontSize='11' Foreground='{DynamicResource ColorBrush4}' Text='随便看看，换一句也行' />
                </StackPanel>
                <Grid Margin='0,8,0,0'>
                    <Grid.ColumnDefinitions>
                        <ColumnDefinition Width='1*' />
                        <ColumnDefinition Width='Auto' />
                    </Grid.ColumnDefinitions>
                    <TextBlock FontSize='12.5' TextWrapping='Wrap' VerticalAlignment='Center'
                               Foreground='{DynamicResource ColorBrush1}' Text='{cave}' />
                    <local:MyButton Grid.Column='1' Margin='12,0,0,0' Height='27' Padding='15,0,15,0' VerticalAlignment='Center'
                                    Text='换一句' EventType='刷新主页' ToolTip='随机换一条回声洞' />
                </Grid>
            </StackPanel>
        </local:MyCard>

        <!-- 快捷操作 -->
        <Grid Margin='0,0,0,14'>
            <Grid.ColumnDefinitions>
                <ColumnDefinition Width='1*' />
                <ColumnDefinition Width='10' />
                <ColumnDefinition Width='1*' />
                <ColumnDefinition Width='10' />
                <ColumnDefinition Width='1*' />
            </Grid.ColumnDefinitions>
            <local:MyButton Grid.Column='0' Height='34' Text='清理垃圾' EventType='清理垃圾' ToolTip='清理 XingYue Launcher 文件夹中的临时文件' />
            <local:MyButton Grid.Column='2' Height='34' Text='内存优化' EventType='内存优化' ToolTip='降低当前内存占用（需管理员权限）' />
            <local:MyButton Grid.Column='4' Height='34' Text='刷新主页' EventType='刷新主页' ToolTip='重新加载主页内容' />
        </Grid>

        <!-- 常用指令 -->
        <local:MyCard Margin='0,0,0,14' CanSwap='False'>
            <StackPanel Margin='18,15,18,15'>
                <TextBlock FontSize='13' FontWeight='Bold' Foreground='{DynamicResource ColorBrush3}' Text='常用指令' />
                <WrapPanel Margin='0,10,0,0'>
                    {commands}
                </WrapPanel>
            </StackPanel>
        </local:MyCard>

        <!-- 底部引文 -->
        <local:MyCard CanSwap='False'>
            <StackPanel Margin='18,15,18,15'>
                <TextBlock FontSize='12.5' TextWrapping='Wrap' Foreground='{DynamicResource ColorBrush2}'
                           Text='花が散るのは、終わりじゃなくて、次の季節への旅立ち。' />
                <TextBlock Margin='0,6,0,0' FontSize='11.5' TextWrapping='Wrap' Foreground='{DynamicResource ColorBrush4}'
                           Text='✿ 樱花飘落不是结束，而是奔赴下一个季节。' />
            </StackPanel>
        </local:MyCard>
    </StackPanel>

    <!-- 右侧：常去小站 -->
    <StackPanel Grid.Column='2'>
        <local:MyCard CanSwap='False'>
            <StackPanel Margin='12,15,12,10'>
                <TextBlock FontSize='13' FontWeight='Bold' Margin='2,0,0,6' Foreground='{DynamicResource ColorBrush3}' Text='常去小站' />
                {sites}
            </StackPanel>
        </local:MyCard>
    </StackPanel>
</Grid>"
            Content = Content.Replace("{commands}", Edit.BuildCommandsXaml())
            Content = Content.Replace("{sites}", Edit.BuildSitesXaml())
            Return Content
        End Get
    End Property

End Module
