Public Class MyScrollViewer
    Inherits ScrollViewer

    Public Property DeltaMult As Double = 1

    Private RealOffset As Double
    Private Sub MyScrollViewer_PreviewMouseWheel(sender As Object, e As MouseWheelEventArgs) Handles Me.PreviewMouseWheel
        If e.Delta = 0 OrElse ActualHeight = 0 OrElse ScrollableHeight = 0 Then Return
        Dim SourceType = e.Source.GetType
        If Content.TemplatedParent Is Nothing AndAlso (
                (GetType(ComboBox).IsAssignableFrom(SourceType) AndAlso CType(e.Source, ComboBox).IsDropDownOpen) OrElse
                (GetType(TextBox).IsAssignableFrom(SourceType) AndAlso CType(e.Source, TextBox).AcceptsReturn) OrElse
                GetType(ComboBoxItem).IsAssignableFrom(SourceType) OrElse
                (TypeOf e.Source Is Visual AndAlso
                    PresentationSource.FromVisual(e.Source)?.RootVisual?.GetType().FullName = "System.Windows.Controls.Primitives.PopupRoot")) Then
            '如果当前是在对有滚动条的下拉框或文本框执行，则不接管操作
            Return
        End If
        '如果鼠标下面还有一层可以滚动的 MyScrollViewer，就让那一层来滚，外层不接管
        If HasInnerScrollable(e.Source, -e.Delta) Then Return
        e.Handled = True
        PerformVerticalOffsetDelta(-e.Delta)
    End Sub

    ''' <summary>
    ''' 判断 Source 到本控件之间是否夹着另一层「能滚动」的 MyScrollViewer。
    ''' 用于嵌套滚动时把滚轮交给内层（外层不再抢）。
    ''' </summary>
    Private Function HasInnerScrollable(Source As Object, Delta As Double) As Boolean
        Dim Cur = TryCast(Source, DependencyObject)
        Dim Count As Integer = 0
        While Cur IsNot Nothing AndAlso Not ReferenceEquals(Cur, Me) AndAlso Count < 200
            Count += 1
            Dim SV = TryCast(Cur, MyScrollViewer)
            If SV IsNot Nothing AndAlso Not ReferenceEquals(SV, Me) AndAlso SV.ScrollableHeight > 0 Then
                'Delta > 0 表示要往下滚；内层还能往这个方向滚才交给它，滚到头了就继续滚外层
                Dim CanGo As Boolean
                If Delta > 0 Then
                    CanGo = SV.VerticalOffset < SV.ScrollableHeight - 0.5
                Else
                    CanGo = SV.VerticalOffset > 0.5
                End If
                If CanGo Then Return True
            End If
            Dim V = TryCast(Cur, Visual)
            If V IsNot Nothing Then
                Cur = VisualTreeHelper.GetParent(V)
            Else
                Cur = LogicalTreeHelper.GetParent(Cur)
            End If
        End While
        Return False
    End Function
    Public Sub PerformVerticalOffsetDelta(Delta As Double)
        AniStart(
            AaDouble(
            Sub(AnimDelta As Double)
                RealOffset = (RealOffset + AnimDelta).Clamp(0, ExtentHeight - ActualHeight)
                ScrollToVerticalOffset(RealOffset)
            End Sub, Delta * DeltaMult, 300,, New AniEaseOutFluent(6)))
    End Sub
    Private Sub MyScrollViewer_ScrollChanged(sender As Object, e As ScrollChangedEventArgs) Handles Me.ScrollChanged
        RealOffset = VerticalOffset
        If FrmMain IsNot Nothing AndAlso (e.VerticalChange OrElse e.ViewportHeightChange) Then FrmMain.BtnExtraBack.ShowRefresh()
    End Sub
    Private Sub MyScrollViewer_IsVisibleChanged(sender As Object, e As DependencyPropertyChangedEventArgs) Handles Me.IsVisibleChanged
        FrmMain.BtnExtraBack.ShowRefresh()
    End Sub

    Public ScrollBar As MyScrollBar
    Private Sub Load() Handles Me.Loaded
        ScrollBar = GetTemplateChild("PART_VerticalScrollBar")
    End Sub

    Private Sub MyScrollViewer_PreviewGotKeyboardFocus(sender As Object, e As KeyboardFocusChangedEventArgs) Handles Me.PreviewGotKeyboardFocus
        If e.NewFocus IsNot Nothing AndAlso TypeOf e.NewFocus Is MySlider Then e.Handled = True '#3854，阻止获得焦点时自动滚动
    End Sub
End Class
