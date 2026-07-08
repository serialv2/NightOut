Option Strict Off
Option Explicit On

Imports System.Collections.Generic
Imports System.Drawing
Imports System.Drawing.Drawing2D
Imports System.Windows.Forms
Imports NightOutAdmin.Theme

Namespace Controls

    ''' <summary>Histogramme horizontal dessiné en GDI+ (thème NightOut).</summary>
    Public Class BarChartControl
        Inherits Control

        Private _data As New List(Of KeyValuePair(Of String, Integer))()
        Public Property BarColor As Color = NightOutTheme.Gold
        Public Property TitleText As String = ""

        Public Sub New()
            Me.DoubleBuffered = True
            Me.BackColor = NightOutTheme.BgPanel
            Me.ForeColor = NightOutTheme.Cream
        End Sub

        Public Sub SetData(data As List(Of KeyValuePair(Of String, Integer)))
            _data = If(data, New List(Of KeyValuePair(Of String, Integer))())
            Me.Invalidate()
        End Sub

        Protected Overrides Sub OnPaint(e As PaintEventArgs)
            MyBase.OnPaint(e)
            Dim g = e.Graphics
            g.SmoothingMode = SmoothingMode.AntiAlias
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit

            Dim padTop = 10
            If Not String.IsNullOrEmpty(TitleText) Then
                Using f = NightOutTheme.FontTitle(11.0F), br As New SolidBrush(NightOutTheme.Gold)
                    g.DrawString(TitleText, f, br, New PointF(12, 8))
                End Using
                padTop = 38
            End If

            If _data Is Nothing OrElse _data.Count = 0 Then
                Using f = NightOutTheme.FontBody(9.5F), br As New SolidBrush(NightOutTheme.Muted)
                    g.DrawString("Aucune donnée", f, br, New PointF(12, padTop + 4))
                End Using
                Return
            End If

            Dim maxVal = 1
            For Each kv In _data
                If kv.Value > maxVal Then maxVal = kv.Value
            Next

            Dim labelW = 130
            Dim valueW = 44
            Dim left = 12 + labelW
            Dim availW = Me.Width - left - valueW - 12
            If availW < 30 Then availW = 30

            Dim rowH = 30
            Dim y = padTop
            Using lblFont = NightOutTheme.FontBody(9.0F),
                  valFont = NightOutTheme.FontTitle(9.0F),
                  lblBrush As New SolidBrush(NightOutTheme.Cream),
                  valBrush As New SolidBrush(NightOutTheme.Gold),
                  trackBrush As New SolidBrush(NightOutTheme.BgPanel3),
                  barBrush As New SolidBrush(BarColor)

                For Each kv In _data
                    ' Libellé
                    Dim lbl = kv.Key
                    If lbl.Length > 20 Then lbl = lbl.Substring(0, 19) & "…"
                    Dim lblRect As New RectangleF(12, y, labelW - 6, rowH)
                    Dim sf As New StringFormat With {.LineAlignment = StringAlignment.Center, .Trimming = StringTrimming.EllipsisCharacter}
                    g.DrawString(lbl, lblFont, lblBrush, lblRect, sf)

                    ' Piste
                    Dim trackRect As New Rectangle(left, y + 6, availW, 16)
                    FillRounded(g, trackBrush, trackRect, 8)

                    ' Barre
                    Dim w = CInt(availW * (kv.Value / maxVal))
                    If w < 4 AndAlso kv.Value > 0 Then w = 4
                    If w > 0 Then
                        Dim barRect As New Rectangle(left, y + 6, w, 16)
                        FillRounded(g, barBrush, barRect, 8)
                    End If

                    ' Valeur
                    Dim valRect As New RectangleF(left + availW + 6, y, valueW, rowH)
                    Dim vsf As New StringFormat With {.LineAlignment = StringAlignment.Center, .Alignment = StringAlignment.Far}
                    g.DrawString(kv.Value.ToString(), valFont, valBrush, valRect, vsf)

                    y += rowH
                Next
            End Using
        End Sub

        Private Shared Sub FillRounded(g As Graphics, br As Brush, r As Rectangle, radius As Integer)
            Using path = RoundedRect(r, radius)
                g.FillPath(br, path)
            End Using
        End Sub

        Private Shared Function RoundedRect(r As Rectangle, radius As Integer) As GraphicsPath
            Dim d = radius * 2
            Dim path As New GraphicsPath()
            If r.Width < d Then d = r.Width
            If d < 2 Then d = 2
            path.AddArc(r.X, r.Y, d, d, 180, 90)
            path.AddArc(r.Right - d, r.Y, d, d, 270, 90)
            path.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90)
            path.AddArc(r.X, r.Bottom - d, d, d, 90, 90)
            path.CloseFigure()
            Return path
        End Function
    End Class

    ''' <summary>Donut (camembert évidé) dessiné en GDI+.</summary>
    Public Class DonutChartControl
        Inherits Control

        Private _slices As New List(Of (Label As String, Value As Integer, Col As Color))()
        Public Property TitleText As String = ""

        Public Sub New()
            Me.DoubleBuffered = True
            Me.BackColor = NightOutTheme.BgPanel
            Me.ForeColor = NightOutTheme.Cream
        End Sub

        Public Sub SetSlices(slices As List(Of (Label As String, Value As Integer, Col As Color)))
            _slices = If(slices, New List(Of (Label As String, Value As Integer, Col As Color))())
            Me.Invalidate()
        End Sub

        Protected Overrides Sub OnPaint(e As PaintEventArgs)
            MyBase.OnPaint(e)
            Dim g = e.Graphics
            g.SmoothingMode = SmoothingMode.AntiAlias
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit

            Dim top = 10
            If Not String.IsNullOrEmpty(TitleText) Then
                Using f = NightOutTheme.FontTitle(11.0F), br As New SolidBrush(NightOutTheme.Gold)
                    g.DrawString(TitleText, f, br, New PointF(12, 8))
                End Using
                top = 38
            End If

            Dim total = 0
            For Each s In _slices
                total += s.Value
            Next

            If total = 0 Then
                Using f = NightOutTheme.FontBody(9.5F), br As New SolidBrush(NightOutTheme.Muted)
                    g.DrawString("Aucune donnée", f, br, New PointF(12, top + 4))
                End Using
                Return
            End If

            ' Cercle à gauche
            Dim size = Math.Min(Me.Height - top - 16, 150)
            If size < 60 Then size = 60
            Dim circleRect As New Rectangle(16, top + 4, size, size)

            Dim startAngle As Single = -90
            For Each s In _slices
                Dim sweep As Single = CSng(360.0 * s.Value / total)
                Using br As New SolidBrush(s.Col)
                    g.FillPie(br, circleRect, startAngle, sweep)
                End Using
                startAngle += sweep
            Next

            ' Trou central
            Dim holeR = CInt(size * 0.55)
            Dim holeRect As New Rectangle(
                circleRect.X + (size - holeR) \ 2,
                circleRect.Y + (size - holeR) \ 2,
                holeR, holeR)
            Using br As New SolidBrush(NightOutTheme.BgPanel)
                g.FillEllipse(br, holeRect)
            End Using
            ' Total au centre
            Using f = NightOutTheme.FontTitle(15.0F), br As New SolidBrush(NightOutTheme.Cream)
                Dim sf As New StringFormat With {.Alignment = StringAlignment.Center, .LineAlignment = StringAlignment.Center}
                Dim holeF As New RectangleF(holeRect.X, holeRect.Y, holeRect.Width, holeRect.Height)
                g.DrawString(total.ToString(), f, br, holeF, sf)
            End Using

            ' Légende à droite
            Dim lx = circleRect.Right + 24
            Dim ly = top + 8
            Using lf = NightOutTheme.FontBody(9.5F), lb As New SolidBrush(NightOutTheme.Cream)
                For Each s In _slices
                    Using dot As New SolidBrush(s.Col)
                        g.FillEllipse(dot, New Rectangle(lx, ly + 3, 12, 12))
                    End Using
                    Dim pct = CInt(Math.Round(100.0 * s.Value / total))
                    g.DrawString($"{s.Label} — {s.Value} ({pct}%)", lf, lb, New PointF(lx + 20, ly))
                    ly += 26
                Next
            End Using
        End Sub
    End Class

End Namespace
