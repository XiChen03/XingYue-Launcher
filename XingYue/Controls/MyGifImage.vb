Imports System.Runtime.InteropServices
Imports System.Windows.Media.Imaging
Imports System.Windows.Threading

''' <summary>
''' 支持 GIF 动图的图片控件。
''' WPF 自带的 Image 只显示第一帧；而 WPF 的 GifBitmapDecoder 解出来的是「增量帧」
''' （只有变化区域，带偏移与处置方式），直接逐帧显示就是一片花屏。
''' 这里用 GDI+ 解码（它会把增量帧正确合成），并把近黑背景抠成透明，
''' 这样叠在浅色卡片上不会有黑底块。
''' </summary>
Public Class MyGifImage
    Inherits Image

    ''' <summary>
    ''' 低于这个亮度视为背景，完全透明。
    ''' </summary>
    Private Const KeyLow As Integer = 12
    ''' <summary>
    ''' 高于这个亮度视为画面内容，完全不透明；中间按亮度渐变，保住发光边缘。
    ''' </summary>
    Private Const KeyHigh As Integer = 52
    ''' <summary>
    ''' 解码时先缩到这个宽度，既省内存也省抠图时间（实际显示尺寸更小）。
    ''' </summary>
    Private Const DecodeWidth As Integer = 256

    Public Shared ReadOnly GifSourceProperty As DependencyProperty = DependencyProperty.Register(
        "GifSource", GetType(String), GetType(MyGifImage),
        New PropertyMetadata(Nothing, AddressOf OnGifSourceChanged))

    ''' <summary>
    ''' 动图路径，支持 pack:// 资源、本地文件与网络地址。
    ''' </summary>
    Public Property GifSource As String
        Get
            Return TryCast(GetValue(GifSourceProperty), String)
        End Get
        Set(value As String)
            SetValue(GifSourceProperty, value)
        End Set
    End Property

    Private Frames As BitmapSource()
    Private Delays As Integer()
    Private FrameIndex As Integer
    Private Timer As DispatcherTimer

    '按地址缓存解码结果，避免同一张动图被反复解码
    Private Shared ReadOnly FrameCache As New System.Collections.Concurrent.ConcurrentDictionary(Of String, Tuple(Of BitmapSource(), Integer()))

    Public Sub New()
        AddHandler Unloaded, Sub() StopTimer()
        AddHandler Loaded, Sub()
                               If Frames IsNot Nothing AndAlso Frames.Length > 1 Then StartTimer()
                           End Sub
    End Sub

    Private Shared Sub OnGifSourceChanged(D As DependencyObject, E As DependencyPropertyChangedEventArgs)
        DirectCast(D, MyGifImage).LoadGif()
    End Sub

    Private Sub LoadGif()
        StopTimer()
        FrameIndex = 0
        Frames = Nothing
        Delays = Nothing
        Source = Nothing
        Dim Raw = GifSource
        If String.IsNullOrWhiteSpace(Raw) Then Return

        Dim Cached As Tuple(Of BitmapSource(), Integer()) = Nothing
        If FrameCache.TryGetValue(Raw, Cached) Then
            ApplyFrames(Cached)
            Return
        End If

        '解码 + 抠图比较耗时，放到后台线程，做完再回 UI 线程替换
        Dim Worker As New Threading.Thread(
            Sub()
                Try
                    Dim Result = DecodeGif(Raw)
                    FrameCache(Raw) = Result
                    Dispatcher.BeginInvoke(New Action(
                        Sub()
                            If GifSource = Raw Then ApplyFrames(Result)
                        End Sub))
                Catch ex As Exception
                    Dispatcher.BeginInvoke(New Action(
                        Sub() Logger.Warn(ex, $"加载动图失败：{Raw}")))
                End Try
            End Sub) With {.IsBackground = True, .Name = "MyGifImage 解码"}
        Worker.Start()
    End Sub

    ''' <summary>
    ''' 把解码结果接上：建来来回复用的播放序列并开始轮播。
    ''' </summary>
    Private Sub ApplyFrames(Cached As Tuple(Of BitmapSource(), Integer()))
        Dim PlayFrames = Cached.Item1
        Dim PlayDelays = Cached.Item2
        If PlayFrames.Length = 0 Then Return
        If PlayFrames.Length > 2 Then
            '来回播放：先正放一遍，再倒放回去，首尾都不重复，循环时看不出接缝
            Dim SeqFrames As New List(Of BitmapSource)
            Dim SeqDelays As New List(Of Integer)
            For Index = 0 To PlayFrames.Length - 1
                SeqFrames.Add(PlayFrames(Index))
                SeqDelays.Add(If(Index < PlayDelays.Length, PlayDelays(Index), 100))
            Next
            For Index = PlayFrames.Length - 2 To 1 Step -1
                SeqFrames.Add(PlayFrames(Index))
                SeqDelays.Add(If(Index < PlayDelays.Length, PlayDelays(Index), 100))
            Next
            PlayFrames = SeqFrames.ToArray()
            PlayDelays = SeqDelays.ToArray()
        End If
        Frames = PlayFrames
        Delays = PlayDelays
        FrameIndex = 0
        Source = Frames(0)
        If Frames.Length > 1 Then StartTimer()
    End Sub

    ''' <summary>
    ''' 用 GDI+ 把动图逐帧合成、抠掉近黑背景后转成 WPF 位图。
    ''' </summary>
    Private Shared Function DecodeGif(Raw As String) As Tuple(Of BitmapSource(), Integer())
        '取出原始字节（pack:// 资源、本地文件、网络地址都先变成可随机读取的流）
        Dim Bytes As Byte()
        If Raw.StartsWith("pack://", StringComparison.OrdinalIgnoreCase) Then
            Dim Info = Application.GetResourceStream(New Uri(Raw, UriKind.Absolute))
            Using Info.Stream
                Bytes = ReadAllBytes(Info.Stream)
            End Using
        ElseIf Raw.StartsWith("http://", StringComparison.OrdinalIgnoreCase) OrElse Raw.StartsWith("https://", StringComparison.OrdinalIgnoreCase) Then
            Using Client As New Net.WebClient()
                Bytes = Client.DownloadData(Raw)
            End Using
        Else
            Bytes = IO.File.ReadAllBytes(Raw)
        End If

        Using Stream As New IO.MemoryStream(Bytes)
            Using Img As System.Drawing.Image = System.Drawing.Image.FromStream(Stream)
                Dim FrameDimension = New System.Drawing.Imaging.FrameDimension(Img.FrameDimensionsList(0))
                Dim Count = Img.GetFrameCount(FrameDimension)

                '帧延迟存在 PropertyTagFrameDelay（&H5100），单位 1/100 秒
                Dim DelayList As New List(Of Integer)
                Try
                    Dim Prop = Img.GetPropertyItem(&H5100)
                    For Index = 0 To Count - 1
                        Dim Offset = Index * 4
                        If Offset + 4 <= Prop.Len Then
                            Dim Delay = BitConverter.ToInt32(Prop.Value, Offset) * 10
                            DelayList.Add(If(Delay > 0, Delay, 100))
                        Else
                            DelayList.Add(100)
                        End If
                    Next
                Catch
                    DelayList.Clear()
                    For Index = 0 To Count - 1
                        DelayList.Add(100)
                    Next
                End Try

                '按显示宽度等比缩到 DecodeWidth
                Dim Scale = Math.Min(1.0, DecodeWidth / Img.Width)
                Dim W = Math.Max(1, CInt(Img.Width * Scale))
                Dim HH = Math.Max(1, CInt(Img.Height * Scale))

                Dim FrameList As New List(Of BitmapSource)
                '所有帧里非透明区域的总包围盒，用来裁掉四周的空白，让画面在卡片里占得更大
                Dim MinX = W, MinY = HH, MaxX = -1, MaxY = -1
                Using Canvas As New System.Drawing.Bitmap(W, HH, System.Drawing.Imaging.PixelFormat.Format32bppArgb)
                    Using CanvasGraphics = System.Drawing.Graphics.FromImage(Canvas)
                        CanvasGraphics.CompositingMode = System.Drawing.Drawing2D.CompositingMode.SourceCopy
                        CanvasGraphics.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic
                        For Index = 0 To Count - 1
                            Img.SelectActiveFrame(FrameDimension, Index)
                            CanvasGraphics.DrawImage(Img, New System.Drawing.Rectangle(0, 0, W, HH))
                            Dim Bounds As New System.Drawing.Rectangle(0, 0, W, HH)
                            FrameList.Add(KeyAndConvert(Canvas, Bounds))
                            MinX = Math.Min(MinX, Bounds.Left)
                            MinY = Math.Min(MinY, Bounds.Top)
                            MaxX = Math.Max(MaxX, Bounds.Right - 1)
                            MaxY = Math.Max(MaxY, Bounds.Bottom - 1)
                        Next
                    End Using
                End Using

                '按包围盒统一裁剪，保证每帧尺寸一致（否则播放时画面会跳）
                If MaxX >= MinX AndAlso MaxY >= MinY Then
                    Dim CropWidth = MaxX - MinX + 1
                    Dim CropHeight = MaxY - MinY + 1
                    If CropWidth < W OrElse CropHeight < HH Then
                        Dim Cropped As New List(Of BitmapSource)
                        For Each Frame In FrameList
                            Dim Piece = New CroppedBitmap(Frame, New Int32Rect(MinX, MinY, CropWidth, CropHeight))
                            Piece.Freeze()
                            Cropped.Add(Piece)
                        Next
                        FrameList = Cropped
                    End If
                End If
                Return New Tuple(Of BitmapSource(), Integer())(FrameList.ToArray(), DelayList.ToArray())
            End Using
        End Using
    End Function

    ''' <summary>
    ''' 把近黑像素抠成透明（边缘按亮度渐变），并转成预乘 Alpha 的 WPF 位图。
    ''' </summary>
    Private Shared Function KeyAndConvert(Bmp As System.Drawing.Bitmap, ByRef ContentBounds As System.Drawing.Rectangle) As BitmapSource
        Dim W = Bmp.Width
        Dim H = Bmp.Height
        Dim Rect As New System.Drawing.Rectangle(0, 0, W, H)
        Dim Data = Bmp.LockBits(Rect, System.Drawing.Imaging.ImageLockMode.ReadOnly, System.Drawing.Imaging.PixelFormat.Format32bppArgb)
        Try
            Dim Stride = Data.Stride
            Dim Src(Stride * H - 1) As Byte
            Marshal.Copy(Data.Scan0, Src, 0, Src.Length)
            Dim Dst(Src.Length - 1) As Byte
            Dim Span = KeyHigh - KeyLow
            Dim MinX = W, MinY = H, MaxX = -1, MaxY = -1
            For Y = 0 To H - 1
                Dim Row = Y * Stride
                For X = 0 To W - 1
                    Dim P = Row + X * 4
                    Dim B = CInt(Src(P))
                    Dim G = CInt(Src(P + 1))
                    Dim R = CInt(Src(P + 2))
                    Dim Lum = (R * 299 + G * 587 + B * 114) \ 1000
                    Dim Alpha As Integer
                    If Lum <= KeyLow Then
                        Alpha = 0
                    ElseIf Lum >= KeyHigh Then
                        Alpha = 255
                    Else
                        Alpha = (Lum - KeyLow) * 255 \ Span
                    End If
                    'Pbgra32 要求颜色预先乘上 Alpha，否则边缘会发黑
                    Dst(P) = CByte(B * Alpha \ 255)
                    Dst(P + 1) = CByte(G * Alpha \ 255)
                    Dst(P + 2) = CByte(R * Alpha \ 255)
                    Dst(P + 3) = CByte(Alpha)
                    '记录有内容的范围（忽略几乎透明的噪点）
                    If Alpha >= 24 Then
                        If X < MinX Then MinX = X
                        If X > MaxX Then MaxX = X
                        If Y < MinY Then MinY = Y
                        If Y > MaxY Then MaxY = Y
                    End If
                Next
            Next
            If MaxX >= MinX AndAlso MaxY >= MinY Then
                ContentBounds = New System.Drawing.Rectangle(MinX, MinY, MaxX - MinX + 1, MaxY - MinY + 1)
            Else
                ContentBounds = System.Drawing.Rectangle.Empty
            End If
            Dim Result = BitmapSource.Create(W, H, 96, 96, PixelFormats.Pbgra32, Nothing, Dst, Stride)
            Result.Freeze()
            Return Result
        Finally
            Bmp.UnlockBits(Data)
        End Try
    End Function

    Private Shared Function ReadAllBytes(Stream As IO.Stream) As Byte()
        Using MS As New IO.MemoryStream()
            Stream.CopyTo(MS)
            Return MS.ToArray()
        End Using
    End Function

    Private Sub StartTimer()
        If Timer Is Nothing Then
            Timer = New DispatcherTimer With {
                .Interval = TimeSpan.FromMilliseconds(If(Delays IsNot Nothing AndAlso Delays.Length > 0 AndAlso Delays(0) > 0, Delays(0), 100))
            }
            AddHandler Timer.Tick, AddressOf OnTick
        End If
        If Not Timer.IsEnabled Then Timer.Start()
    End Sub

    Private Sub StopTimer()
        If Timer IsNot Nothing Then Timer.Stop()
    End Sub

    Private Sub OnTick(sender As Object, e As EventArgs)
        If Frames Is Nothing OrElse Frames.Length = 0 Then
            StopTimer()
            Return
        End If
        FrameIndex = (FrameIndex + 1) Mod Frames.Length
        Source = Frames(FrameIndex)
        If Delays IsNot Nothing AndAlso FrameIndex < Delays.Length AndAlso Delays(FrameIndex) > 0 Then
            Timer.Interval = TimeSpan.FromMilliseconds(Delays(FrameIndex))
        End If
    End Sub
End Class
